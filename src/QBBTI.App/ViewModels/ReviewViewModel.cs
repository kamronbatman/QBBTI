using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QBBTI.App.Services;
using QBBTI.Core.Mapping;
using QBBTI.Core.Models;
using QBBTI.Core.QuickBooks;

namespace QBBTI.App.ViewModels;

public class PreviewItem
{
    public string Summary { get; init; } = "";
    public string Xml { get; init; } = "";
    public BankTransaction Transaction { get; init; } = null!;
}

public partial class ReviewViewModel : ObservableObject
{
    private readonly List<BankTransaction> _allTransactions;
    private readonly MappingEngine _engine;
    private readonly QBConnectionManager _qb;
    private readonly List<QBAccount> _accounts;
    private readonly QBAccount _selectedBank;
    private readonly HashSet<string> _entities;
    private readonly IDialogService _dialogService;
    private readonly Action _onBack;
    private readonly TransactionGrouper _grouper = new();

    [ObservableProperty]
    private ObservableCollection<TransactionGroupViewModel> _groups = new();

    [ObservableProperty]
    private double _importProgress;

    [ObservableProperty]
    private string _importStatusText = "";

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private ObservableCollection<PreviewItem> _previewItems = new();

    [ObservableProperty]
    private bool _isPreviewVisible;

    public int TotalTransactions => _allTransactions.Count;
    public int AutoMappedCount => _allTransactions.Count(t => t.IsAutoMapped);
    public int UnmatchedCount => _allTransactions.Count(t => !t.IsAutoMapped);

    public ReviewViewModel(
        List<BankTransaction> transactions,
        List<TransactionGroup> groups,
        MappingEngine engine,
        QBConnectionManager qb,
        List<QBAccount> accounts,
        QBAccount selectedBank,
        HashSet<string> entities,
        IDialogService dialogService,
        Action onBack)
    {
        _allTransactions = transactions;
        _engine = engine;
        _qb = qb;
        _accounts = accounts;
        _selectedBank = selectedBank;
        _entities = entities;
        _dialogService = dialogService;
        _onBack = onBack;

        BuildGroupViewModels(groups);
    }

    private void BuildGroupViewModels(List<TransactionGroup> groups)
    {
        Groups.Clear();
        foreach (var group in groups)
        {
            Groups.Add(new TransactionGroupViewModel(group, _accounts, _entities, OnUngroup, OnUngroupMultiple));
        }
    }

    private void OnUngroup(TransactionGroupViewModel groupVm, BankTransactionViewModel txnVm)
    {
        // Remove from current group
        groupVm.Transactions.Remove(txnVm);
        groupVm.Model.Transactions.Remove(txnVm.Model);

        if (groupVm.Transactions.Count == 0)
            Groups.Remove(groupVm);
        else
            groupVm.RefreshComputedProperties();

        // Try to match the transaction against mapping rules
        var rule = _engine.FindMatchingRule(txnVm.Model);
        if (rule != null)
        {
            txnVm.Model.Payee = rule.PayeeName;
            txnVm.Model.MappedAccountName = rule.AccountName;
            txnVm.Model.EntityType = rule.EntityType;
            txnVm.Model.MatchedRuleId = rule.Id;
            txnVm.Model.IsAutoMapped = true;
            if (rule.Memo != null)
                txnVm.Model.Memo = rule.Memo;
        }

        // Create standalone group with matched rule if found
        var standaloneGroup = new TransactionGroup
        {
            GroupKey = rule != null ? $"rule:{rule.Id}" : $"payee:{txnVm.Model.Payee}",
            MatchedRule = rule,
            Transactions = { txnVm.Model }
        };

        var standaloneVm = new TransactionGroupViewModel(standaloneGroup, _accounts, _entities, OnUngroup, OnUngroupMultiple);
        Groups.Add(standaloneVm);
    }

    private void OnUngroupMultiple(TransactionGroupViewModel groupVm, List<BankTransactionViewModel> txnVms)
    {
        // Try to find a common matching rule for all transactions
        MappingRule? commonRule = null;
        var allMatchSameRule = true;

        foreach (var txnVm in txnVms)
        {
            var rule = _engine.FindMatchingRule(txnVm.Model);
            if (rule != null)
            {
                txnVm.Model.Payee = rule.PayeeName;
                txnVm.Model.MappedAccountName = rule.AccountName;
                txnVm.Model.EntityType = rule.EntityType;
                txnVm.Model.MatchedRuleId = rule.Id;
                txnVm.Model.IsAutoMapped = true;
                if (rule.Memo != null)
                    txnVm.Model.Memo = rule.Memo;
            }

            if (commonRule == null && rule != null)
                commonRule = rule;
            else if (rule?.Id != commonRule?.Id)
                allMatchSameRule = false;
        }

        if (!allMatchSameRule)
            commonRule = null;

        var newGroup = new TransactionGroup
        {
            GroupKey = commonRule != null ? $"rule:{commonRule.Id}" : $"manual:{Guid.NewGuid():N}",
            MatchedRule = commonRule,
            Transactions = txnVms.Select(t => t.Model).ToList()
        };

        foreach (var txnVm in txnVms)
        {
            txnVm.IsCheckedForGrouping = false;
            groupVm.Transactions.Remove(txnVm);
            groupVm.Model.Transactions.Remove(txnVm.Model);
        }

        if (groupVm.Transactions.Count == 0)
            Groups.Remove(groupVm);
        else
            groupVm.RefreshComputedProperties();

        var newGroupVm = new TransactionGroupViewModel(newGroup, _accounts, _entities, OnUngroup, OnUngroupMultiple);
        Groups.Add(newGroupVm);
    }

    public void MergeGroups(TransactionGroupViewModel source, TransactionGroupViewModel target)
    {
        if (source == target) return;

        foreach (var txnVm in source.Transactions)
        {
            target.Transactions.Add(txnVm);
            target.Model.Transactions.Add(txnVm.Model);
            target.SubscribeTransaction(txnVm);
        }

        // Clear matched rule if merging auto-mapped with manual
        if (source.IsAutoMapped != target.IsAutoMapped)
            target.Model.MatchedRule = null;

        Groups.Remove(source);
        target.RefreshComputedProperties();
    }

    [RelayCommand]
    private void SaveMapping(TransactionGroupViewModel groupVm)
    {
        if (string.IsNullOrWhiteSpace(groupVm.PayeeName) || string.IsNullOrWhiteSpace(groupVm.AccountName))
        {
            _dialogService.ShowError("Payee and account are required to save a mapping rule.");
            return;
        }

        // Build a pattern from the first transaction's raw description
        var sampleDesc = groupVm.Transactions.FirstOrDefault()?.Model.RawDescription ?? "";
        var pattern = BuildPatternFromDescription(sampleDesc);

        var rule = new MappingRule
        {
            Pattern = pattern,
            IsRegex = false,
            PayeeName = groupVm.PayeeName,
            AccountName = groupVm.AccountName,
            EntityType = groupVm.EntityType,
            Memo = groupVm.Transactions.FirstOrDefault()?.Memo
        };

        _engine.SaveRule(rule);

        // Mark as auto-mapped
        foreach (var txn in groupVm.Transactions)
        {
            txn.Model.IsAutoMapped = true;
            txn.Model.MatchedRuleId = rule.Id;
        }

        groupVm.Model.MatchedRule = rule;
        OnPropertyChanged(nameof(AutoMappedCount));
        OnPropertyChanged(nameof(UnmatchedCount));
    }

    private static string BuildPatternFromDescription(string description)
    {
        // Use a distinctive portion of the description for matching
        if (description.Length > 30)
        {
            return description[..30];
        }

        return description;
    }

    [RelayCommand]
    private void ImportSelected()
    {
        var selectedTxns = Groups
            .Where(g => g.IsSelected)
            .SelectMany(g => g.Transactions)
            .Where(t => t.IsSelected)
            .Select(t => t.Model)
            .ToList();

        if (selectedTxns.Count == 0)
        {
            _dialogService.ShowError("No transactions selected for import.");
            return;
        }

        // Handle Quick Add prompts and build QBXML preview for each transaction
        PreviewItems.Clear();
        foreach (var txn in selectedTxns)
        {
            if (!string.IsNullOrEmpty(txn.Payee) && !_entities.Contains(txn.Payee))
            {
                var entityType = _dialogService.ShowQuickAddDialog(txn.Payee);
                if (entityType != null)
                {
                    var (addOk, _) = _qb.AddEntity(txn.Payee, entityType);
                    if (addOk)
                    {
                        _entities.Add(txn.Payee);
                    }
                }
                else
                {
                    txn.Payee = "";
                }
            }

            var xml = QBXmlBuilder.BuildTransactionRequest(txn, _selectedBank.FullName);
            var type = txn.IsDebit ? "CHECK" : "DEPOSIT";
            var summary = $"{type}  {txn.Date:MM/dd/yyyy}  {txn.Payee}  ${txn.Amount:N2}";

            PreviewItems.Add(new PreviewItem
            {
                Summary = summary,
                Xml = xml,
                Transaction = txn
            });
        }

        IsPreviewVisible = true;
    }

    [RelayCommand]
    private async Task ConfirmImport()
    {
        IsImporting = true;
        ImportProgress = 0;

        var success = 0;
        var failed = 0;

        for (var i = 0; i < PreviewItems.Count; i++)
        {
            var item = PreviewItems[i];
            ImportStatusText = $"Importing {i + 1} of {PreviewItems.Count}: {item.Transaction.Payee}...";
            ImportProgress = (double)(i + 1) / PreviewItems.Count * 100;

            try
            {
                var (ok, msg) = _qb.SendTransaction(item.Xml);

                if (ok)
                {
                    success++;
                }
                else
                {
                    failed++;
                }
            }
            catch
            {
                failed++;
            }

            await Task.Delay(50);
        }

        IsImporting = false;
        IsPreviewVisible = false;
        PreviewItems.Clear();
        ImportStatusText = $"Import complete: {success} succeeded, {failed} failed";
        ImportProgress = 100;
    }

    [RelayCommand]
    private void CancelPreview()
    {
        IsPreviewVisible = false;
        PreviewItems.Clear();
    }

    [RelayCommand]
    private void GoBack()
    {
        _onBack();
    }
}
