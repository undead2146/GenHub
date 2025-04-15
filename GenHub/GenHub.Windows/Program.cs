using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Avalonia;

namespace GenHub.Windows;

class Program
{
    private static Mutex s_Mutex;
    private const string MutexName = "Global\\GenHub";
    private const int SW_RESTORE = 9; // Windows API constant to restore a window

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (IsAnotherInstanceRunning())
        {
            FocusRunningInstance();
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static bool IsAnotherInstanceRunning()
    {
        s_Mutex = new Mutex(true, MutexName, out bool createdNew);
        return !createdNew;
    }

    private static void FocusRunningInstance()
    {
        var process = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName).FirstOrDefault();

        if(process == null)
            return;

        var windowHandle = process.MainWindowHandle;
        ShowWindow(windowHandle, SW_RESTORE);
        SetForegroundWindow(windowHandle);
    }
}