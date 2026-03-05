using System.Text.RegularExpressions;
using System.Windows;
using QBBTI.App.ViewModels;

namespace QBBTI.App.Views;

public partial class EditRuleDialog : Window
{
    public EditRuleDialogViewModel ViewModel { get; }

    public EditRuleDialog(EditRuleDialogViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ViewModel.Pattern))
        {
            MessageBox.Show("Pattern cannot be empty.", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (ViewModel.IsRegex)
        {
            try
            {
                _ = new Regex(ViewModel.Pattern);
            }
            catch (RegexParseException ex)
            {
                MessageBox.Show($"Invalid regex pattern: {ex.Message}", "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        DialogResult = true;
        Close();
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
