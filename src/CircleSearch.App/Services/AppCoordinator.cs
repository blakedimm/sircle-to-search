using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows;
using CircleSearch.App.Config;
using CircleSearch.Core.Math;
using CircleSearch.Core.Models;
using CircleSearch.Native.Capture;
using CircleSearch.Native.Hooks;
using CircleSearch.UI.Overlay;
using CircleSearch.UI.Sidebar;

namespace CircleSearch.App.Services;

public sealed class AppCoordinator : IDisposable
{
    private readonly HotkeyManager _searchHotkey;
    private readonly HotkeyManager _exitHotkey;
    private readonly IScreenCapture _screenCapture;
    private readonly SettingsManager _settingsManager;

    private OverlayWindow? _activeOverlay;
    private LensSidebarWindow? _sidebarWindow;
    private SettingsWindow? _activeSettingsWindow;

    public AppSettings Settings => _settingsManager.CurrentSettings;

    public AppCoordinator()
    {
        _settingsManager = new SettingsManager();
        _settingsManager.Load();

        _screenCapture = new GdiScreenCapture();
        _searchHotkey = new HotkeyManager();
        _exitHotkey = new HotkeyManager();
    }

    public void Start()
    {
        _searchHotkey.HotkeyPressed += TriggerSearch;
        _exitHotkey.HotkeyPressed += ShutdownApp;

        _searchHotkey.Register(Settings.Modifiers, Settings.VirtualKey);
        _exitHotkey.Register(0x0002 | 0x0004, 0x51); // Ctrl + Shift + Q
    }

    public void OpenSettings()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_activeSettingsWindow != null && _activeSettingsWindow.IsLoaded)
            {
                _activeSettingsWindow.Activate();
                return;
            }

            _activeSettingsWindow = new SettingsWindow(_settingsManager);
            _activeSettingsWindow.SettingsSaved += OnSettingsSaved;
            _activeSettingsWindow.Closed += (_, _) =>
            {
                _activeSettingsWindow = null;
                App.TrimMemory();
            };
            _activeSettingsWindow.Show();
            _activeSettingsWindow.Activate();
        });
    }

    private void OnSettingsSaved()
    {
        _searchHotkey.Register(Settings.Modifiers, Settings.VirtualKey);

        if (_sidebarWindow != null)
        {
            _sidebarWindow.DefaultMode = Settings.DefaultDisplayMode;
            _sidebarWindow.AiOnlyMode = Settings.AiOnlyMode;
        }
    }

    public void TriggerSearch()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_activeOverlay != null)
                return;

            using var frame = _screenCapture.CaptureVirtualScreen();
            if (frame == null || frame.Data == null || frame.Data.Length == 0)
                return;

            var fullScreenshot = CreateBitmapFromFrame(frame);

            _activeOverlay = new OverlayWindow(
                fullScreenshot, 
                Settings.AccentColorHex, 
                Settings.SelectionThickness, 
                Settings.DimmingOpacity);

            _activeOverlay.SelectionCompleted += OnSelectionCompleted;
            _activeOverlay.SelectionCanceled += OnSelectionCanceled;
            _activeOverlay.Show();
        });
    }

    private static Bitmap CreateBitmapFromFrame(CapturedFrame frame)
    {
        var bitmap = new Bitmap(frame.Width, frame.Height, PixelFormat.Format32bppArgb);
        var bmpData = bitmap.LockBits(
            new Rectangle(0, 0, frame.Width, frame.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            Marshal.Copy(frame.Data, 0, bmpData.Scan0, frame.Data.Length);
        }
        finally
        {
            bitmap.UnlockBits(bmpData);
        }

        return bitmap;
    }

    private async void OnSelectionCompleted(byte[] imageBytes, SelectionBounds bounds, Point2D originPoint)
    {
        _activeOverlay = null;

        if (_sidebarWindow == null || !_sidebarWindow.IsLoaded)
        {
            _sidebarWindow = new LensSidebarWindow();
            _sidebarWindow.SettingsRequested += OpenSettings;
        }

        _sidebarWindow.DefaultMode = Settings.DefaultDisplayMode;
        _sidebarWindow.AiOnlyMode = Settings.AiOnlyMode;
        await _sidebarWindow.ProcessImageAsync(imageBytes, bounds);
    }

    private void OnSelectionCanceled()
    {
        _activeOverlay = null;
        App.TrimMemory();
    }

    public void ShutdownApp()
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            try
            {
                Dispose();
            }
            catch { }
            finally
            {
                Application.Current.Shutdown();
                Environment.Exit(0);
            }
        });
    }

    public void Dispose()
    {
        _searchHotkey.Dispose();
        _exitHotkey.Dispose();
        _screenCapture.Dispose();

        if (_sidebarWindow != null)
        {
            _sidebarWindow.Cleanup();
            _sidebarWindow.Close();
            _sidebarWindow = null;
        }

        if (_activeOverlay != null)
        {
            _activeOverlay.Close();
            _activeOverlay = null;
        }

        if (_activeSettingsWindow != null)
        {
            _activeSettingsWindow.Close();
            _activeSettingsWindow = null;
        }
    }
}