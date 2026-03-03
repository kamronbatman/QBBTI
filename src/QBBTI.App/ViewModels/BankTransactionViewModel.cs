using CommunityToolkit.Mvvm.ComponentModel;
using QBBTI.Core.Models;

namespace QBBTI.App.ViewModels;

public partial class BankTransactionViewModel : ObservableObject
{
    public BankTransaction Model { get; }

    public DateTime Date => Model.Date;
    public string RawDescription => Model.RawDescription;
    public decimal Amount => Model.Amount;
    public bool IsDebit => Model.IsDebit;
    public string? CheckNumber => Model.CheckNumber;
    public string? Memo => Model.Memo;
    public string TransactionType => Model.TransactionType;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isCheckedForGrouping;

    public string AmountDisplay => IsDebit ? $"-{Amount:N2}" : $"+{Amount:N2}";

    public BankTransactionViewModel(BankTransaction model)
    {
        Model = model;
        _isSelected = model.IsSelected;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        Model.IsSelected = value;
    }
}
