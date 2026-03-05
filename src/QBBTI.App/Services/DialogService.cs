using System.Windows;
using Microsoft.Win32;
using QBBTI.App.ViewModels;
using QBBTI.App.Views;

namespace QBBTI.App.Services;

public class DialogService : IDialogService
{
    public string? ShowOpenFileDialog(string filter, string title = "Open File")
    {
        var dialog = new OpenFileDialog
        {
            Filter = filter,
            Title = title
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    /// <summary>
    /// Shows Quick Add dialog. Returns "Vendor", "Customer", "Other", or null if skipped.
    /// </summary>
    public string? ShowQuickAddDialog(string payeeName)
    {
        var dialog = new QuickAddDialog(payeeName)
        {
            Owner = Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.SelectedEntityType : null;
    }

    public EditRuleDialogViewModel? ShowEditRuleDialog(EditRuleDialogViewModel viewModel)
    {
        var dialog = new EditRuleDialog(viewModel)
        {
            Owner = Application.Current.MainWindow
        };

        return dialog.ShowDialog() == true ? dialog.ViewModel : null;
    }

    public void ShowError(string message, string title = "Error")
    {
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
    }

    public bool ShowConfirmation(string message, string title = "Confirm")
    {
        return MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
    }
}
