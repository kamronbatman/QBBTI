using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QBBTI.App.Services;
using QBBTI.Core.Mapping;
using QBBTI.Core.Models;
using QBBTI.Core.Parsing;
using QBBTI.Core.QuickBooks;

namespace QBBTI.App.ViewModels;

public partial class ImportViewModel : ObservableObject
{
    private readonly IDialogService _dialogService;
    private readonly Action<List<BankTransaction>, List<TransactionGroup>, MappingEngine, QBConnectionManager, List<QBAccount>, QBAccount, HashSet<string>> _onLoaded;
    private readonly BankFileParserFactory _parserFactory = new();

    private QBConnectionManager? _qb;
    private List<QBAccount> _allAccounts = new();
    private HashSet<string> _entities = new();

    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private string? _detectedParserName;

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionStatus = "Not connected";

    [ObservableProperty]
    private ObservableCollection<QBAccount> _bankAccounts = new();

    [ObservableProperty]
    private QBAccount? _selectedBankAccount;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    public bool CanLoadTransactions =>
        !string.IsNullOrEmpty(FilePath) && IsConnected && SelectedBankAccount != null && !IsLoading;

    public ImportViewModel(
        IDialogService dialogService,
        Action<List<BankTransaction>, List<TransactionGroup>, MappingEngine, QBConnectionManager, List<QBAccount>, QBAccount, HashSet<string>> onLoaded)
    {
        _dialogService = dialogService;
        _onLoaded = onLoaded;
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var path = _dialogService.ShowOpenFileDialog(
            "Bank Files (*.csv;*.ofx;*.qfx;*.qbo)|*.csv;*.ofx;*.qfx;*.qbo|All Files (*.*)|*.*",
            "Select Bank File");

        if (path == null)
        {
            return;
        }

        FilePath = path;
        var parser = _parserFactory.DetectParser(path);
        DetectedParserName = parser?.DisplayName ?? "Unknown format";
        ErrorMessage = parser == null ? "Could not detect file format." : null;

        OnPropertyChanged(nameof(CanLoadTransactions));
    }

    [RelayCommand]
    private void ConnectToQB()
    {
        try
        {
            ErrorMessage = null;
            _qb = new QBConnectionManager();
            _qb.Connect();
            IsConnected = true;
            ConnectionStatus = $"Connected to QuickBooks — {_qb.CompanyName}";

            _allAccounts = _qb.QueryAccounts();
            _entities = _qb.QueryAllEntityNames();

            BankAccounts.Clear();
            foreach (var acct in _allAccounts.Where(a => a.IsBankAccount).OrderBy(a => a.FullName))
                BankAccounts.Add(acct);

            if (BankAccounts.Count > 0)
            {
                SelectedBankAccount = BankAccounts[0];
            }

            OnPropertyChanged(nameof(CanLoadTransactions));
        }
        catch (Exception ex)
        {
            IsConnected = false;
            ConnectionStatus = "Connection failed";
            ErrorMessage = $"Could not connect to QuickBooks: {ex.Message}\n\nMake sure QuickBooks Desktop is running with a company file open.";
        }
    }

    [RelayCommand]
    private void LoadTransactions()
    {
        if (FilePath == null || _qb == null || SelectedBankAccount == null)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = null;

            var parser = _parserFactory.DetectParser(FilePath);
            if (parser == null)
            {
                ErrorMessage = "Could not detect bank file format.";
                return;
            }

            var transactions = parser.Parse(FilePath);

            // Apply mappings
            var mappingsPath = Path.Combine(AppContext.BaseDirectory, "mappings.json");
            if (!File.Exists(mappingsPath))
            {
                mappingsPath = Path.Combine(Directory.GetCurrentDirectory(), "mappings.json");
            }

            var engine = new MappingEngine(mappingsPath, _qb.CompanyName!);
            engine.ApplyMappings(transactions);

            var grouper = new TransactionGrouper();
            var groups = grouper.Group(transactions, engine);

            _onLoaded(transactions, groups, engine, _qb, _allAccounts, SelectedBankAccount, _entities);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to load transactions: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedBankAccountChanged(QBAccount? value)
    {
        OnPropertyChanged(nameof(CanLoadTransactions));
    }
}
