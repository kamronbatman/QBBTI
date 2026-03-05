using CommunityToolkit.Mvvm.ComponentModel;

namespace QBBTI.App.ViewModels;

public partial class EditRuleDialogViewModel : ObservableObject
{
    // Context (read-only display)
    public string SampleDescription { get; init; } = "";
    public decimal SampleAmount { get; init; }
    public int TransactionCount { get; init; }

    // Pass-through from group (not editable in dialog)
    public string PayeeName { get; init; } = "";
    public string AccountName { get; init; } = "";
    public string EntityType { get; init; } = "Vendor";

    // Editable fields
    [ObservableProperty]
    private string _pattern = "";

    [ObservableProperty]
    private bool _isRegex = true;

    public bool IsContains
    {
        get => !IsRegex;
        set => IsRegex = !value;
    }

    partial void OnIsRegexChanged(bool value) => OnPropertyChanged(nameof(IsContains));

    [ObservableProperty]
    private bool _useAmountRange;

    [ObservableProperty]
    private decimal? _minAmount;

    [ObservableProperty]
    private decimal? _maxAmount;

    [ObservableProperty]
    private string? _memo;
}
