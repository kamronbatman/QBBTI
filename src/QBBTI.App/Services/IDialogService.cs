using QBBTI.App.ViewModels;

namespace QBBTI.App.Services;

public interface IDialogService
{
    string? ShowOpenFileDialog(string filter, string title = "Open File");
    string? ShowQuickAddDialog(string payeeName);
    EditRuleDialogViewModel? ShowEditRuleDialog(EditRuleDialogViewModel viewModel);
    void ShowError(string message, string title = "Error");
    bool ShowConfirmation(string message, string title = "Confirm");
}
