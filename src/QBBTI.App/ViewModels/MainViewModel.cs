using CommunityToolkit.Mvvm.ComponentModel;
using QBBTI.App.Services;
using QBBTI.Core.Mapping;
using QBBTI.Core.Models;
using QBBTI.Core.QuickBooks;

namespace QBBTI.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _statusText = "Ready";

    private readonly IDialogService _dialogService;
    private readonly ImportViewModel _importViewModel;
    private ReviewViewModel? _reviewViewModel;

    public MainViewModel(IDialogService dialogService)
    {
        _dialogService = dialogService;
        _importViewModel = new ImportViewModel(dialogService, OnTransactionsLoaded);
        CurrentView = _importViewModel;
    }

    private void OnTransactionsLoaded(
        List<BankTransaction> transactions,
        List<TransactionGroup> groups,
        MappingEngine engine,
        QBConnectionManager qb,
        List<QBAccount> accounts,
        QBAccount selectedBank,
        HashSet<string> entities)
    {
        _reviewViewModel = new ReviewViewModel(
            transactions, groups, engine, qb, accounts,
            selectedBank, entities, _dialogService, OnBackToImport);
        CurrentView = _reviewViewModel;
        StatusText = $"Reviewing {transactions.Count} transactions in {groups.Count} groups";
    }

    private void OnBackToImport()
    {
        CurrentView = _importViewModel;
        StatusText = "Ready";
    }

    public void Disconnect()
    {
        _importViewModel.Disconnect();
    }
}
