using CommunityToolkit.Mvvm.ComponentModel;

namespace QBBTI.App.ViewModels;

public partial class QuickAddDialogViewModel : ObservableObject
{
    public string PayeeName { get; }

    [ObservableProperty]
    private string _selectedEntityType = "Vendor";

    public QuickAddDialogViewModel(string payeeName)
    {
        PayeeName = payeeName;
    }
}
