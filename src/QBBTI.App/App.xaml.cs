using System.Windows;
using QBBTI.App.ViewModels;

namespace QBBTI.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Safety net: disconnect QB on unhandled exceptions so sessions don't ghost
        DispatcherUnhandledException += (_, args) =>
        {
            DisconnectQuickBooks();
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{args.Exception.Message}",
                "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DisconnectQuickBooks();
        base.OnExit(e);
    }

    private void DisconnectQuickBooks()
    {
        (MainWindow?.DataContext as MainViewModel)?.Disconnect();
    }
}
