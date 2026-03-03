using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QBBTI.Core.Mapping;
using QBBTI.Core.Models;

namespace QBBTI.App.ViewModels;

public partial class TransactionGroupViewModel : ObservableObject
{
    public TransactionGroup Model { get; }

    [ObservableProperty]
    private string _payeeName;

    [ObservableProperty]
    private string? _accountName;

    [ObservableProperty]
    private string _entityType;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _hasCheckedTransactions;

    [ObservableProperty]
    private int _checkedTransactionCount;

    public bool IsAutoMapped => Model.IsAutoMapped;
    public int TransactionCount => Transactions.Count;
    public string? GroupDescription => !IsAutoMapped
        ? Model.Transactions.FirstOrDefault()?.RawDescription
        : null;

    public string AmountSummary
    {
        get
        {
            if (Transactions.Count == 0)
                return "";

            var amounts = Transactions.Select(t => t.IsDebit ? -t.Amount : t.Amount).Distinct().ToList();
            if (amounts.Count == 1)
                return $"@ {FormatAmount(amounts[0])}";

            var min = amounts.Min();
            var max = amounts.Max();
            return $"{FormatAmount(min)} to {FormatAmount(max)}";
        }
    }

    private static string FormatAmount(decimal amount) =>
        amount >= 0 ? $"+{amount:N2}" : $"{amount:N2}";

    public ObservableCollection<BankTransactionViewModel> Transactions { get; } = [];

    public List<string> AvailablePayees { get; set; } = [];
    public List<string> AvailableAccounts { get; set; } = [];
    public List<string> EntityTypes { get; } = ["Vendor", "Customer", "Other"];

    private readonly Action<TransactionGroupViewModel, BankTransactionViewModel>? _onUngroup;
    private readonly Action<TransactionGroupViewModel, List<BankTransactionViewModel>>? _onUngroupMultiple;

    public TransactionGroupViewModel(TransactionGroup group,
        List<QBAccount> accounts,
        HashSet<string> entityNames,
        Action<TransactionGroupViewModel, BankTransactionViewModel>? onUngroup = null,
        Action<TransactionGroupViewModel, List<BankTransactionViewModel>>? onUngroupMultiple = null)
    {
        Model = group;
        _onUngroup = onUngroup;
        _onUngroupMultiple = onUngroupMultiple;

        _payeeName = group.MatchedRule?.PayeeName ?? "";
        _accountName = group.MatchedRule?.AccountName ?? group.Transactions.FirstOrDefault()?.MappedAccountName;
        _entityType = group.MatchedRule?.EntityType ?? group.Transactions.FirstOrDefault()?.EntityType ?? "Vendor";

        AvailablePayees = entityNames.OrderBy(n => n).ToList();

        AvailableAccounts = accounts
            .Where(a => !a.IsBankAccount)
            .Select(a => a.FullName)
            .OrderBy(n => n)
            .ToList();

        foreach (var txn in group.Transactions.OrderBy(t => t.Date))
        {
            var vm = new BankTransactionViewModel(txn);
            vm.PropertyChanged += OnTransactionPropertyChanged;
            Transactions.Add(vm);
        }
    }

    private void OnTransactionPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(BankTransactionViewModel.IsCheckedForGrouping))
            UpdateCheckedState();
    }

    private void UpdateCheckedState()
    {
        CheckedTransactionCount = Transactions.Count(t => t.IsCheckedForGrouping);
        HasCheckedTransactions = CheckedTransactionCount > 0;
    }

    public void SubscribeTransaction(BankTransactionViewModel txn)
    {
        txn.PropertyChanged += OnTransactionPropertyChanged;
    }

    partial void OnPayeeNameChanged(string value)
    {
        foreach (var txn in Transactions)
            txn.Model.Payee = value;
    }

    partial void OnAccountNameChanged(string? value)
    {
        foreach (var txn in Transactions)
            txn.Model.MappedAccountName = value;
    }

    partial void OnEntityTypeChanged(string value)
    {
        foreach (var txn in Transactions)
            txn.Model.EntityType = value;
    }

    partial void OnIsSelectedChanged(bool value)
    {
        foreach (var txn in Transactions)
            txn.IsSelected = value;
    }

    [RelayCommand]
    private void Ungroup(BankTransactionViewModel txnVm)
    {
        _onUngroup?.Invoke(this, txnVm);
    }

    [RelayCommand]
    private void UngroupChecked()
    {
        var checked_ = Transactions.Where(t => t.IsCheckedForGrouping).ToList();
        if (checked_.Count == 0) return;
        _onUngroupMultiple?.Invoke(this, checked_);
    }

    public void RefreshComputedProperties()
    {
        OnPropertyChanged(nameof(TransactionCount));
        OnPropertyChanged(nameof(AmountSummary));
        OnPropertyChanged(nameof(GroupDescription));
        OnPropertyChanged(nameof(IsAutoMapped));
    }
}
