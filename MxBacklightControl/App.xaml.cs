using System.Threading;
using System.Windows;

namespace MxBacklightControl;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _singleInstanceMutex = new Mutex(true, "Global\\MxBacklightControl_SingleInstance", out bool createdNew);
        if (!createdNew)
        {
            System.Windows.MessageBox.Show(
                "MX Backlight Control zaten arka planda çalışıyor.\nSistem tepsisindeki (saat yanı) simgeden açabilirsiniz.",
                "MX Backlight Control",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        bool startMinimizedArg = e.Args.Any(a => a.Equals("--minimized", StringComparison.OrdinalIgnoreCase));
        var mainWindow = new MainWindow(startMinimizedArg);
        MainWindow = mainWindow;

        if (!startMinimizedArg)
        {
            mainWindow.Show();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
