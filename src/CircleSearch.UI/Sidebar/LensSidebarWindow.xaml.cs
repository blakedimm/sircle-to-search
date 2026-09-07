using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using CircleSearch.Core.Models;
using Microsoft.Web.WebView2.Core;

namespace CircleSearch.UI.Sidebar;

public partial class LensSidebarWindow : Window
{
    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    private static CoreWebView2Environment? _sharedEnvironment;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private WindowDisplayMode _currentMode = WindowDisplayMode.Compact;
    private SelectionBounds _lastBounds;
    private string _lastResultUrl = "https://lens.google.com";
    private CancellationTokenSource? _extractionCts;

    public event Action? SettingsRequested;
    public WindowDisplayMode DefaultMode { get; set; } = WindowDisplayMode.Compact;
    public bool AiOnlyMode { get; set; } = true;

    public LensSidebarWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => LoadHeaderIcon();
    }

    private void LoadHeaderIcon()
    {
        try
        {
            Stream? stream = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                stream = asm.GetManifestResourceStream("CircleSearch.UI.Assets.icon.png")
                      ?? asm.GetManifestResourceStream("CircleSearch.App.Assets.icon.png");
                if (stream != null) break;
            }

            if (stream == null)
            {
                string diskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.png");
                if (File.Exists(diskPath))
                    stream = File.OpenRead(diskPath);
            }

            if (stream != null)
            {
                using (stream)
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.StreamSource = stream;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();
                    AppHeaderIconBrush.ImageSource = bmp;
                }
            }
        }
        catch { }
    }

    public void Cleanup()
    {
        try { Browser.Dispose(); } catch { }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        HideAndSuspend();
    }

    protected override void OnClosed(EventArgs e)
    {
        Cleanup();
        base.OnClosed(e);
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            HideAndSuspend();
        }
    }

    private async void HideAndSuspend()
    {
        _extractionCts?.Cancel();
        Hide();

        try
        {
            if (Browser.CoreWebView2 != null)
            {
                await Browser.CoreWebView2.TrySuspendAsync();
            }
        }
        catch { }

        TrimMemory();
    }

    private static void TrimMemory()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            GC.WaitForPendingFinalizers();
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }
        catch { }
    }

    private async Task EnsureBrowserReadyAsync()
    {
        if (Browser.CoreWebView2 != null)
        {
            try { Browser.CoreWebView2.Resume(); } catch { }
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (Browser.CoreWebView2 != null)
            {
                try { Browser.CoreWebView2.Resume(); } catch { }
                return;
            }

            if (_sharedEnvironment == null)
            {
                string userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CircleSearch", "Profile");

                Directory.CreateDirectory(userDataFolder);

                var options = new CoreWebView2EnvironmentOptions(
                    additionalBrowserArguments:
                    "--lang=ru-RU --accept-lang=ru-RU,ru " +
                    "--force-dark-mode " +
                    "--renderer-process-limit=1 " +
                    "--disable-background-networking " +
                    "--disable-sync " +
                    "--disable-extensions " +
                    "--disable-default-apps " +
                    "--js-flags=\"--max-old-space-size=128\"");

                _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, userDataFolder, options);
            }

            await Browser.EnsureCoreWebView2Async(_sharedEnvironment);

            if (Browser.CoreWebView2 != null)
            {
                Browser.CoreWebView2.Settings.IsStatusBarEnabled = false;
                Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                Browser.CoreWebView2.Settings.UserAgent =
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

                Browser.SourceChanged += (_, _) =>
                {
                    if (Browser.Source != null && Browser.Source.ToString().StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        _lastResultUrl = Browser.Source.ToString();
                    }
                };

                Browser.NavigationCompleted += async (_, _) =>
                {
                    string url = Browser.Source?.ToString() ?? string.Empty;
                    if (url.Contains("/search") || url.Contains("lens.google"))
                    {
                        if (AiOnlyMode)
                        {
                            StartExtractionPipeline();
                        }
                        else
                        {
                            Dispatcher.Invoke(() =>
                            {
                                Browser.Visibility = Visibility.Visible;
                                AiResultContainer.Visibility = Visibility.Collapsed;
                                AiFallbackCard.Visibility = Visibility.Collapsed;
                                LoadingOverlay.Visibility = Visibility.Collapsed;
                            });
                        }
                    }

                    await UpdateLoginStatusAsync();
                };

                await UpdateLoginStatusAsync();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ошибка WebView2: {ex.Message}", "CircleSearch", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private void StartExtractionPipeline()
    {
        _extractionCts?.Cancel();
        _extractionCts = new CancellationTokenSource();
        var token = _extractionCts.Token;

        Task.Run(async () =>
        {
            await ExtractAndRenderAiResponseAsync(token);
        }, token);
    }

    private async Task UpdateLoginStatusAsync()
    {
        if (Browser.CoreWebView2 == null) return;

        try
        {
            bool isLoggedIn = false;
            string[] domains = ["https://google.com", "https://accounts.google.com", "https://lens.google.com", "https://www.google.com"];
            foreach (var d in domains)
            {
                var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync(d);
                if (cookies.Exists(c => c.Name is "SID" or "SAPISID" or "SSID" or "__Secure-1PSID" or "OSID"))
                {
                    isLoggedIn = true;
                    break;
                }
            }

            if (!isLoggedIn)
            {
                string domCheckScript = @"
                (function() {
                    return Boolean(
                        document.querySelector('header a[href*=""SignOutOptions""], img[src*=""googleusercontent.com""], a[aria-label*=""Google: ""], a[aria-label*=""Аккаунт Google""]')
                    );
                })();";
                string raw = await Browser.CoreWebView2.ExecuteScriptAsync(domCheckScript);
                isLoggedIn = raw.Equals("true", StringComparison.OrdinalIgnoreCase);
            }

            Dispatcher.Invoke(() =>
            {
                if (isLoggedIn)
                {
                    BtnGoogleLogin.Visibility = Visibility.Collapsed;
                    AccountBadge.Visibility = Visibility.Visible;
                }
                else
                {
                    BtnGoogleLogin.Visibility = Visibility.Visible;
                    AccountBadge.Visibility = Visibility.Collapsed;
                }
            });

            if (isLoggedIn)
            {
                await TryFetchUserAvatarAsync();
            }
        }
        catch { }
    }

    private async Task ExtractAndRenderAiResponseAsync(CancellationToken token)
    {
        string extractScript = @"
        (function() {
            // 1. Ищем заголовок 'Обзор от ИИ'
            const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
            let headerNode = null;
            while (walker.nextNode()) {
                const val = (walker.currentNode.nodeValue || '').trim().toLowerCase();
                if (val === 'обзор от ии' || val === 'ai overview' || val.includes('обзор от ии') || val.includes('ai overview')) {
                    if (val.length < 40) {
                        headerNode = walker.currentNode.parentElement;
                        break;
                    }
                }
            }

            if (!headerNode) return '';

            // Раскрываем спойлеры 'Развернуть'
            document.querySelectorAll('button, div[role=""button""]').forEach(b => {
                const bt = (b.textContent || '').trim().toLowerCase();
                if (bt.includes('развернуть') || bt.includes('ещё') || bt.includes('more')) {
                    try { b.click(); } catch(e) {}
                }
            });

            // 2. Находим контейнер карточки
            let card = headerNode;
            for (let i = 0; i < 5 && card.parentElement && card.parentElement !== document.body; i++) {
                card = card.parentElement;
                if (card.getAttribute('data-attrid') || (card.offsetHeight > 80 && (card.innerText || '').length > 90)) {
                    break;
                }
            }
            if (!card) card = headerNode.parentElement;

            const clone = card.cloneNode(true);

            // 3. Вырезаем все ссылки (источники, сайты, сноски) полностью
            clone.querySelectorAll('a').forEach(el => el.remove());

            // 4. Вычищаем интерактивные элементы, вкладки, формы и мусор
            clone.querySelectorAll('script, style, svg, img, noscript, input, textarea, form, nav, footer, header, [role=""tab""], [role=""tablist""], [role=""navigation""], [role=""toolbar""]').forEach(el => el.remove());

            // 5. Удаляем служебные кнопки
            clone.querySelectorAll('button, [role=""button""], div, span, p').forEach(el => {
                const t = (el.innerText || '').trim().toLowerCase();
                if (/^(?:показать все|свернуть|общее|источники|sources)$/i.test(t)) {
                    el.remove();
                }
            });

            // 6. Форматируем списки и абзацы без гигантских дыр
            clone.querySelectorAll('li').forEach(li => {
                li.prepend(document.createTextNode('\n• '));
            });

            clone.querySelectorAll('p, h1, h2, h3, h4').forEach(el => {
                el.prepend(document.createTextNode('\n\n'));
            });

            let text = clone.innerText || clone.textContent || '';

            // 7. Срезаем всё, начиная со слов источников, вкладок, файлов и шеринга
            const tailCutoff = /(?:\n|^)\s*(?:Общее\s*\d*\s*файлов|Общее\b|Источники\b|Sources\b|Показать все|Свернуть|Визуальные совпадения|Похожие изображения|\d+\s*файлов|Поделиться|Share).*/gis;
            text = text.replace(tailCutoff, '');

            // 8. Чистка системных фраз
            text = text.replace(/^[✦\s*]*обзор от ии[^\n]*/gi, '');
            text = text.replace(/^[✦\s*]*ai overview[^\n]*/gi, '');
            text = text.replace(/Используйте код с осторожностью\.?/gi, '');
            text = text.replace(/Use code with caution\.?/gi, '');
            text = text.replace(/Ответ режима ИИ:\s*/gi, '');
            text = text.replace(/Задайте вопрос.*/gis, '');
            text = text.replace(/Спасибо!\s*Ваши отзывы.*/gis, '');
            text = text.replace(/Ознакомьтесь с Политикой конфиденциальности[^\n]*/gi, '');
            text = text.replace(/Для этого запроса обзор от ИИ недоступен[^\n]*/gi, '');
            text = text.replace(/Не удалось сгенерировать обзор[^\n]*/gi, '');
            text = text.replace(/Повторите попытку позже[^\n]*/gi, '');
            text = text.replace(/\[\d+\]/g, ''); // сноски [1], [2]

            // 9. Нормализация пробелов и переносов строк
            text = text.replace(/\r/g, '');
            text = text.replace(/([.!?])([А-ЯA-Z])/g, '$1 $2');
            text = text.replace(/[ \t]+\n/g, '\n').replace(/\n[ \t]+/g, '\n');
            text = text.replace(/•\s+/g, '• ');
            text = text.replace(/\n{3,}/g, '\n\n').trim();

            return text.length > 20 ? text : '';
        })();";

        string extractedAiText = string.Empty;

        for (int i = 0; i < 50; i++)
        {
            if (token.IsCancellationRequested) return;

            if (i == 6) Dispatcher.Invoke(() => TxtStatus.Text = "ИИ формирует ответ...");

            try
            {
                await Dispatcher.InvokeAsync(async () =>
                {
                    if (Browser.CoreWebView2 != null)
                    {
                        string raw = await Browser.CoreWebView2.ExecuteScriptAsync(extractScript);
                        extractedAiText = JsonSerializer.Deserialize<string>(raw) ?? string.Empty;
                    }
                });

                if (!string.IsNullOrWhiteSpace(extractedAiText))
                {
                    break;
                }
            }
            catch { }

            await Task.Delay(350, token);
        }

        if (token.IsCancellationRequested) return;

        Dispatcher.Invoke(() =>
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;

            if (!string.IsNullOrWhiteSpace(extractedAiText))
            {
                TxtAiContent.Text = extractedAiText;
                AiResultContainer.Visibility = Visibility.Visible;
                AiFallbackCard.Visibility = Visibility.Collapsed;
                Browser.Visibility = Visibility.Collapsed;
            }
            else
            {
                AiResultContainer.Visibility = Visibility.Collapsed;
                AiFallbackCard.Visibility = Visibility.Visible;
                Browser.Visibility = Visibility.Collapsed;
            }
        });
    }
    private async Task TryFetchUserAvatarAsync()
    {
        if (Browser.CoreWebView2 == null) return;

        try
        {
            string script = @"
            (function() {
                const selectors = [
                    'header a[href*=""SignOutOptions""] img',
                    'header a[aria-label*=""Google""] img',
                    'a[href*=""accounts.google.com""] img',
                    'img.gb_k', 'img.gb_m', 'img.gb_n', 'img.gb_A',
                    'img[src*=""googleusercontent.com""]',
                    'button[aria-label*=""Google""] img'
                ];
                for (const sel of selectors) {
                    const img = document.querySelector(sel);
                    if (img && img.src && img.src.includes('googleusercontent.com')) {
                        let url = img.src;
                        return url.replace(/=s\d+[^=]*$/, '=s192-c');
                    }
                }
                return '';
            })();";

            string raw = await Browser.CoreWebView2.ExecuteScriptAsync(script);
            string avatarUrl = JsonSerializer.Deserialize<string>(raw) ?? "";

            if (!string.IsNullOrWhiteSpace(avatarUrl) && avatarUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                Dispatcher.Invoke(() =>
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(avatarUrl);
                    bmp.DecodePixelWidth = 96;
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.EndInit();
                    bmp.Freeze();

                    UserAvatarBrush.ImageSource = bmp;
                    UserAvatarEllipse.Visibility = Visibility.Visible;
                });
            }
        }
        catch { }
    }

    private async void BtnGoogleLogin_Click(object sender, RoutedEventArgs e)
    {
        _extractionCts?.Cancel();
        AiResultContainer.Visibility = Visibility.Collapsed;
        AiFallbackCard.Visibility = Visibility.Collapsed;
        LoadingOverlay.Visibility = Visibility.Visible;
        TxtStatus.Text = "Подключение к Google...";

        await EnsureBrowserReadyAsync();

        if (Browser.CoreWebView2 != null)
        {
            Browser.Visibility = Visibility.Visible;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            Browser.CoreWebView2.Navigate("https://accounts.google.com/signin/v2/identifier?hl=ru");
        }
    }

    public async Task ProcessImageAsync(byte[] pngBytes, SelectionBounds bounds)
    {
        _lastBounds = bounds;
        _extractionCts?.Cancel();

        Dispatcher.Invoke(() =>
        {
            TxtStatus.Text = "Поиск ответа от ИИ...";
            LoadingOverlay.Visibility = Visibility.Visible;
            AiResultContainer.Visibility = Visibility.Collapsed;
            AiFallbackCard.Visibility = Visibility.Collapsed;
            Browser.Visibility = Visibility.Collapsed;

            if (!IsVisible)
            {
                _currentMode = DefaultMode;
            }
            ApplyDisplayMode(_currentMode);
            Show();
            Activate();
        });

        await EnsureBrowserReadyAsync();

        if (Browser.CoreWebView2 == null)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            TxtStatus.Text = "Загрузка в Google Lens...";

            var cookies = await Browser.CoreWebView2.CookieManager.GetCookiesAsync("https://lens.google.com");
            var cookieContainer = new CookieContainer();
            foreach (var c in cookies)
            {
                cookieContainer.Add(new Cookie(c.Name, c.Value, c.Path, c.Domain));
            }

            using var handler = new HttpClientHandler
            {
                CookieContainer = cookieContainer,
                AllowAutoRedirect = false
            };

            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(15)
            };

            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36");

            using var form = new MultipartFormDataContent($"----CircleSearchBoundary{Guid.NewGuid():N}");
            var byteContent = new ByteArrayContent(pngBytes);
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            form.Add(byteContent, "encoded_image", "capture.png");

            using var response = await httpClient.PostAsync("https://lens.google.com/v3/upload", form);

            string? targetUrl = null;
            if (response.Headers.Location != null)
            {
                targetUrl = response.Headers.Location.IsAbsoluteUri
                    ? response.Headers.Location.AbsoluteUri
                    : new Uri(new Uri("https://lens.google.com"), response.Headers.Location).AbsoluteUri;
            }
            else if (response.IsSuccessStatusCode)
            {
                string html = await response.Content.ReadAsStringAsync();
                var match = Regex.Match(html, @"https?://lens\.google\.com/search\?p=[a-zA-Z0-9_\-]+");
                if (match.Success)
                {
                    targetUrl = match.Value.Replace("\\u0026", "&");
                }
            }

            if (!string.IsNullOrEmpty(targetUrl))
            {
                _lastResultUrl = targetUrl;
                TxtStatus.Text = "Анализ изображения от ИИ...";
                Browser.CoreWebView2.Navigate(targetUrl);
            }
            else
            {
                TxtStatus.Text = "Сервис Google временно недоступен.";
                await Task.Delay(1200);
                LoadingOverlay.Visibility = Visibility.Collapsed;
                AiFallbackCard.Visibility = Visibility.Visible;
            }
        }
        catch (Exception ex)
        {
            TxtStatus.Text = $"Ошибка: {ex.Message}";
            await Task.Delay(1200);
            LoadingOverlay.Visibility = Visibility.Collapsed;
            AiFallbackCard.Visibility = Visibility.Visible;
        }
    }

    public void ApplyDisplayMode(WindowDisplayMode mode)
    {
        _currentMode = mode;
        ModeLabel.Text = mode.ToString();

        double screenLeft = SystemParameters.WorkArea.Left;
        double screenRight = SystemParameters.WorkArea.Right;
        double screenTop = SystemParameters.WorkArea.Top;
        double screenBottom = SystemParameters.WorkArea.Bottom;

        switch (mode)
        {
            case WindowDisplayMode.Compact:
                Width = 840;
                Height = 440;

                double targetX = _lastBounds.Right + 16;
                double targetY = _lastBounds.Y;

                if (targetX + Width > screenRight)
                {
                    targetX = _lastBounds.X - Width - 16;
                }

                if (targetX < screenLeft || targetX + Width > screenRight)
                {
                    targetX = Math.Clamp((screenRight - Width) / 2, screenLeft + 16, screenRight - Width - 16);
                }

                if (targetY + Height > screenBottom)
                    targetY = screenBottom - Height - 16;
                if (targetY < screenTop)
                    targetY = screenTop + 16;

                Left = targetX;
                Top = targetY;
                break;

            case WindowDisplayMode.Sidebar:
                Width = 620;
                Height = screenBottom - screenTop;
                Left = screenRight - Width;
                Top = screenTop;
                break;

            case WindowDisplayMode.Expanded:
                Width = 1100;
                Height = screenBottom - screenTop - 60;
                Left = Math.Max(screenLeft + 10, (screenRight - Width) / 2);
                Top = screenTop + 30;
                break;
        }
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TxtAiContent.Text))
        {
            Clipboard.SetText(TxtAiContent.Text);
            BtnCopy.Content = "✓ Скопировано";
            Task.Delay(1800).ContinueWith(_ => Dispatcher.Invoke(() => BtnCopy.Content = "📋 Копировать"));
        }
    }

    private void BtnShowWebResults_Click(object sender, RoutedEventArgs e)
    {
        AiFallbackCard.Visibility = Visibility.Collapsed;
        AiResultContainer.Visibility = Visibility.Collapsed;
        Browser.Visibility = Visibility.Visible;
    }

    private void Header_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            DragMove();
    }

    private void BtnSettings_Click(object sender, RoutedEventArgs e) => SettingsRequested?.Invoke();

    private void BtnCompact_Click(object sender, RoutedEventArgs e) => ApplyDisplayMode(WindowDisplayMode.Compact);
    private void BtnSidebar_Click(object sender, RoutedEventArgs e) => ApplyDisplayMode(WindowDisplayMode.Sidebar);
    private void BtnExpanded_Click(object sender, RoutedEventArgs e) => ApplyDisplayMode(WindowDisplayMode.Expanded);

    private void BtnOpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = _lastResultUrl,
            UseShellExecute = true
        });
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => HideAndSuspend();
}