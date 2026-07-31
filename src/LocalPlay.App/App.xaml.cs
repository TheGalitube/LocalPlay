using System.Runtime.InteropServices;
using System.Windows;
using WpfApplication = System.Windows.Application;

namespace LocalPlay;

public partial class App : WpfApplication
{
    private const string SingleInstanceMutexName = @"Local\LocalPlay.App.SingleInstance";
    private static readonly IntPtr BroadcastWindow = new(0xffff);
    private Mutex? _singleInstanceMutex;
    private bool _ownsSingleInstanceMutex;

    internal static readonly int ActivateWindowMessage =
        RegisterWindowMessage("LocalPlay.ActivateExistingWindow");

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            SingleInstanceMutexName,
            out _ownsSingleInstanceMutex);
        if (!_ownsSingleInstanceMutex)
        {
            PostMessage(BroadcastWindow, ActivateWindowMessage, IntPtr.Zero, IntPtr.Zero);
            Shutdown();
            return;
        }

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_ownsSingleInstanceMutex)
        {
            _singleInstanceMutex?.ReleaseMutex();
        }

        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int RegisterWindowMessage(string messageName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PostMessage(
        IntPtr window,
        int message,
        IntPtr wordParameter,
        IntPtr longParameter);
}
