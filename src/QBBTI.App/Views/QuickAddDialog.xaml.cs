using System.Windows;
using System.Windows.Controls;
using QBBTI.App.ViewModels;

namespace QBBTI.App.Views;

public partial class QuickAddDialog : Window
{
    private readonly QuickAddDialogViewModel _vm;

    public string? SelectedEntityType { get; private set; }

    public QuickAddDialog(string payeeName)
    {
        _vm = new QuickAddDialogViewModel(payeeName);
        DataContext = _vm;
        InitializeComponent();
    }

    private void OnEntityTypeChanged(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string tag)
        {
            _vm.SelectedEntityType = tag;
        }
    }

    private void OnAdd(object sender, RoutedEventArgs e)
    {
        SelectedEntityType = _vm.SelectedEntityType;
        DialogResult = true;
        Close();
    }

    private void OnSkip(object sender, RoutedEventArgs e)
    {
        SelectedEntityType = null;
        DialogResult = false;
        Close();
    }
}
