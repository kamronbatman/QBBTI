using System.Windows;
using QBBTI.App.Services;
using QBBTI.App.ViewModels;

namespace QBBTI.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new DialogService());
        Closing += (_, _) => (DataContext as MainViewModel)?.Disconnect();
    }
}
