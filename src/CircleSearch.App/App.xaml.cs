using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using CircleSearch.App.Services;
using CircleSearch.App.Tray;
using CircleSearch.Native.Win32;

namespace CircleSearch.App;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private AppCoordinator? _coordinator;
    private TrayIconManager? _trayManager;

    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    /// <summary>
    /// Принудительно очищает неиспользуемую память процесса и сбрасывает Working Set
    /// </summary>
    public static void TrimMemory()
    {
        try
        {
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Aggressive, true, true);
            EmptyWorkingSet(Process.GetCurrentProcess().Handle);
        }
        catch { }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        const string appGuid = "CircleSearch_SingleInstance_Mutex_9A7D";
        _singleInstanceMutex = new Mutex(true, appGuid, out bool isNewInstance);

        if (!isNewInstance)
        {
            Current.Shutdown();
            return;
        }

        base.OnStartup(e);

        DpiHelper.EnablePerMonitorDpiAwareness();

        _coordinator = new AppCoordinator();
        _coordinator.Start();

        _trayManager = new TrayIconManager();
        _trayManager.Initialize();

        _trayManager.TriggerSearchRequested += () => _coordinator.TriggerSearch();
        _trayManager.SettingsRequested += () => _coordinator.OpenSettings();
        _trayManager.ExitRequested += () => _coordinator.ShutdownApp();

        // Сбрасываем лишний стартовый оверхед WPF
        TrimMemory();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _coordinator?.Dispose();
        _trayManager?.Dispose();
        
        if (_singleInstanceMutex != null)
        {
            try { _singleInstanceMutex.ReleaseMutex(); } catch { }
            _singleInstanceMutex.Dispose();
        }

        base.OnExit(e);
        Environment.Exit(0);
    }
}