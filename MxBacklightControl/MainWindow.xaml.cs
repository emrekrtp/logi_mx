using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using MxBacklightControl.ViewModels;

namespace MxBacklightControl;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private bool _isExplicitExit;

    public MainWindow(bool startMinimized)
    {
        InitializeComponent();

        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        _viewModel.ShowWindowRequested += ShowFromTray;
        _viewModel.ExitRequested += ExitApplication;

        // Attach native Win32 hotkey hook once window handle exists
        _viewModel.AttachWindow(this);

        if (startMinimized || _viewModel.StartMinimized)
        {
            Hide();
        }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            DragMove();
        }
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.MinimizeToTray)
        {
            Hide();
        }
        else
        {
            ExitApplication();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitExit && _viewModel.MinimizeToTray)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        _viewModel.Dispose();
        base.OnClosing(e);
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApplication()
    {
        _isExplicitExit = true;
        _viewModel.Dispose();
        Close();
        System.Windows.Application.Current.Shutdown();
    }
}
