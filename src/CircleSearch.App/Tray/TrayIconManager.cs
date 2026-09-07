using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace CircleSearch.App.Tray;

public sealed class TrayIconManager : IDisposable
{
    private const int WM_USER = 0x0400;
    private const int WM_TRAYICON = WM_USER + 1;
    private const int WM_RBUTTONUP = 0x0205;
    private const int WM_LBUTTONUP = 0x0202;

    private const int NIM_ADD = 0x00000000;
    private const int NIM_MODIFY = 0x00000001;
    private const int NIM_DELETE = 0x00000002;
    private const int NIF_MESSAGE = 0x00000001;
    private const int NIF_ICON = 0x00000002;
    private const int NIF_TIP = 0x00000004;

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private readonly WndProcDelegate _wndProc;
    private IntPtr _hwnd = IntPtr.Zero;

    public event Action? TriggerSearchRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct NOTIFYICONDATA
    {
        public int cbSize;
        public IntPtr hWnd;
        public int uID;
        public int uFlags;
        public int uCallbackMessage;
        public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szTip;
        public int dwState;
        public int dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szInfo;
        public int uVersion;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string szInfoTitle;
        public int dwInfoFlags;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct WNDCLASSEX
    {
        public uint cbSize;
        public uint style;
        public IntPtr lpfnWndProc;
        public int cbClsExtra;
        public int cbWndExtra;
        public IntPtr hInstance;
        public IntPtr hIcon;
        public IntPtr hCursor;
        public IntPtr hbrBackground;
        public string? lpszMenuName;
        public string lpszClassName;
        public IntPtr hIconSm;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern bool Shell_NotifyIcon(int dwMessage, ref NOTIFYICONDATA lpdata);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern ushort RegisterClassEx(ref WNDCLASSEX lpwcx);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateWindowEx(
        uint dwExStyle, string lpClassName, string lpWindowName,
        uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll")]
    private static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    public TrayIconManager()
    {
        _wndProc = CustomWndProc;
    }

    public void Initialize()
    {
        string className = "CircleSearch_TrayHost_" + Guid.NewGuid().ToString("N");
        var hInstance = GetModuleHandle(null);

        var wc = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<WNDCLASSEX>(),
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            hInstance = hInstance,
            lpszClassName = className
        };

        RegisterClassEx(ref wc);
        _hwnd = CreateWindowEx(0, className, "TrayHost", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, hInstance, IntPtr.Zero);

        IntPtr hIcon = CreateCircularTrayIcon();

        var data = new NOTIFYICONDATA
        {
            cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
            hWnd = _hwnd,
            uID = 1001,
            uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP,
            uCallbackMessage = WM_TRAYICON,
            hIcon = hIcon,
            szTip = "CircleSearch (Ctrl + Shift + S)"
        };

        Shell_NotifyIcon(NIM_ADD, ref data);
    }

    private static IntPtr CreateCircularTrayIcon()
    {
        try
        {
            Stream? stream = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                stream = asm.GetManifestResourceStream("CircleSearch.App.Assets.icon.png")
                      ?? asm.GetManifestResourceStream("CircleSearch.UI.Assets.icon.png");
                if (stream != null) break;
            }

            if (stream == null)
            {
                string diskPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.png");
                if (File.Exists(diskPath))
                    stream = File.OpenRead(diskPath);
            }

            if (stream == null)
                return SystemIcons.Application.Handle;

            using (stream)
            using (var srcBmp = new Bitmap(stream))
            {
                int size = 32;
                using var roundedBmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g = Graphics.FromImage(roundedBmp);

                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;

                using var path = new System.Drawing.Drawing2D.GraphicsPath();
                path.AddEllipse(0, 0, size - 1, size - 1);
                g.SetClip(path);
                g.DrawImage(srcBmp, 0, 0, size, size);

                return roundedBmp.GetHicon();
            }
        }
        catch
        {
            return SystemIcons.Application.Handle;
        }
    }

    private IntPtr CustomWndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_TRAYICON)
        {
            int action = lParam.ToInt32();
            if (action == WM_LBUTTONUP)
            {
                TriggerSearchRequested?.Invoke();
            }
            else if (action == WM_RBUTTONUP)
            {
                ShowContextMenu();
            }
            return IntPtr.Zero;
        }

        return DefWindowProc(hWnd, msg, wParam, lParam);
    }

    private void ShowContextMenu()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var menu = new ContextMenu();

            var searchItem = new MenuItem { Header = "🔍 Поиск" };
            searchItem.Click += (_, _) => TriggerSearchRequested?.Invoke();

            var settingsItem = new MenuItem { Header = "⚙ Настройки" };
            settingsItem.Click += (_, _) => SettingsRequested?.Invoke();

            var exitItem = new MenuItem { Header = "✕ Закрыть CircleSearch" };
            exitItem.Click += (_, _) =>
            {
                Dispose();
                ExitRequested?.Invoke();
            };

            menu.Items.Add(searchItem);
            menu.Items.Add(settingsItem);
            menu.Items.Add(new Separator());
            menu.Items.Add(exitItem);

            menu.IsOpen = true;
        });
    }

    public void Dispose()
    {
        if (_hwnd != IntPtr.Zero)
        {
            var data = new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _hwnd,
                uID = 1001
            };
            Shell_NotifyIcon(NIM_DELETE, ref data);

            DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }
}