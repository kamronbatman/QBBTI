using System.Runtime.InteropServices;
using System.Xml.Linq;
using Interop.QBXMLRP2;
using QBBTI.Core.Models;

namespace QBBTI.Core.QuickBooks;

/// <summary>
/// Manages COM interop connection to QuickBooks Desktop via QBXMLRP2.
/// </summary>
public class QBConnectionManager : IDisposable
{
    private RequestProcessor2? _requestProcessor;
    private string? _ticket;
    private bool _disposed;

    public bool IsConnected => _ticket != null;
    public string? CompanyName { get; private set; }

    /// <summary>
    /// Opens a connection and begins a session with QB Desktop.
    /// QuickBooks must be running with a company file open.
    /// </summary>
    public void Connect()
    {
        _requestProcessor = new RequestProcessor2();
        _requestProcessor.OpenConnection2("", "QBBTI", QBXMLRPConnectionType.localQBD);
        _ticket = _requestProcessor.BeginSession("", QBFileMode.qbFileOpenDoNotCare);
        QueryCompanyName();
    }

    private void QueryCompanyName()
    {
        var request = QBXmlBuilder.BuildCompanyQuery();
        var response = ProcessRequest(request);
        var doc = XDocument.Parse(response);
        CompanyName = doc.Descendants("CompanyName").FirstOrDefault()?.Value;
    }

    /// <summary>
    /// Sends a QBXML request and returns the raw XML response string.
    /// </summary>
    public string ProcessRequest(string qbxmlRequest)
    {
        if (_requestProcessor == null || _ticket == null)
        {
            throw new InvalidOperationException("Not connected to QuickBooks. Call Connect() first.");
        }

        return _requestProcessor.ProcessRequest(_ticket, qbxmlRequest);
    }

    /// <summary>
    /// Queries all accounts from QB and returns them as QBAccount objects.
    /// </summary>
    public List<QBAccount> QueryAccounts()
    {
        var request = QBXmlBuilder.BuildAccountQuery();
        var response = ProcessRequest(request);
        return ParseAccountQueryResponse(response);
    }

    private static List<QBAccount> ParseAccountQueryResponse(string xml)
    {
        var accounts = new List<QBAccount>();
        var doc = XDocument.Parse(xml);

        var accountRets = doc.Descendants("AccountRet");
        foreach (var accountRet in accountRets)
        {
            accounts.Add(new QBAccount
            {
                ListID = accountRet.Element("ListID")?.Value ?? "",
                FullName = accountRet.Element("FullName")?.Value ?? "",
                AccountType = accountRet.Element("AccountType")?.Value ?? "",
                Balance = decimal.TryParse(accountRet.Element("Balance")?.Value, out var bal) ? bal : 0
            });
        }

        return accounts;
    }

    // --- Entity (Vendor/Customer/OtherName) management ---

    /// <summary>
    /// Queries all entity names from QB (vendors, customers, other names).
    /// Returns a set of names for quick lookup.
    /// </summary>
    public HashSet<string> QueryAllEntityNames()
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (query, elementName) in new[]
        {
            (QBXmlBuilder.BuildVendorQuery(), "VendorRet"),
            (QBXmlBuilder.BuildCustomerQuery(), "CustomerRet"),
            (QBXmlBuilder.BuildOtherNameQuery(), "OtherNameRet"),
        })
        {
            try
            {
                var response = ProcessRequest(query);
                var doc = XDocument.Parse(response);
                foreach (var ret in doc.Descendants(elementName))
                {
                    var name = ret.Element("Name")?.Value;
                    if (name != null)
                    {
                        names.Add(name);
                    }
                }
            }
            catch { /* Some entity types may not be queryable */ }
        }

        return names;
    }

    /// <summary>
    /// Adds an entity to QuickBooks. entityType must be "Vendor", "Customer", or "Other".
    /// </summary>
    public (bool Success, string Message) AddEntity(string name, string entityType)
    {
        var xml = entityType switch
        {
            "Vendor" => QBXmlBuilder.BuildVendorAdd(name),
            "Customer" => QBXmlBuilder.BuildCustomerAdd(name),
            "Other" => QBXmlBuilder.BuildOtherNameAdd(name),
            _ => throw new ArgumentException($"Unknown entity type: {entityType}")
        };

        var response = ProcessRequest(xml);
        return ParseTransactionResponse(response);
    }

    // --- Transaction queries ---

    /// <summary>
    /// Queries existing checks and deposits in the given date range.
    /// </summary>
    public List<ExistingTransaction> QueryExistingTransactions(DateTime fromDate, DateTime toDate)
    {
        var results = new List<ExistingTransaction>();

        // Query checks
        try
        {
            var checkResponse = ProcessRequest(QBXmlBuilder.BuildCheckQuery(fromDate, toDate));
            var checkDoc = XDocument.Parse(checkResponse);
            foreach (var ret in checkDoc.Descendants("CheckRet"))
            {
                var date = DateTime.TryParse(ret.Element("TxnDate")?.Value, out var d) ? d : DateTime.MinValue;
                var amount = decimal.TryParse(ret.Element("Amount")?.Value, out var a) ? a : 0;
                var memo = ret.Element("Memo")?.Value;
                var payee = ret.Element("PayeeEntityRef")?.Element("FullName")?.Value;
                var refNum = ret.Element("RefNumber")?.Value;
                results.Add(new ExistingTransaction(date, amount, IsDebit: true, memo, payee, refNum));
            }
        }
        catch { /* Check query may fail if no checks exist */ }

        // Query deposits
        try
        {
            var depositResponse = ProcessRequest(QBXmlBuilder.BuildDepositQuery(fromDate, toDate));
            var depositDoc = XDocument.Parse(depositResponse);
            foreach (var ret in depositDoc.Descendants("DepositRet"))
            {
                var date = DateTime.TryParse(ret.Element("TxnDate")?.Value, out var d) ? d : DateTime.MinValue;
                var amount = decimal.TryParse(ret.Element("DepositTotal")?.Value, out var a) ? a : 0;
                var memo = ret.Element("Memo")?.Value;
                results.Add(new ExistingTransaction(date, amount, IsDebit: false, memo, PayeeName: null, RefNumber: null));
            }
        }
        catch { /* Deposit query may fail if no deposits exist */ }

        return results;
    }

    /// <summary>
    /// Marks bank transactions that match existing QB transactions as possible duplicates.
    /// Matches on: same date + same amount + same debit/credit direction.
    /// For checks with a check number, also matches RefNumber.
    /// </summary>
    public static void MarkDuplicates(List<BankTransaction> transactions, List<ExistingTransaction> existing)
    {
        foreach (var txn in transactions)
        {
            txn.IsPossibleDuplicate = existing.Any(e =>
                e.Date == txn.Date &&
                e.Amount == txn.Amount &&
                e.IsDebit == txn.IsDebit &&
                (txn.CheckNumber == null || string.Equals(txn.CheckNumber, e.RefNumber, StringComparison.OrdinalIgnoreCase)));
        }
    }

    // --- Transaction operations ---

    /// <summary>
    /// Sends a CheckAdd or DepositAdd request and returns the status code and message.
    /// </summary>
    public (bool Success, string Message) SendTransaction(string qbxmlRequest)
    {
        var response = ProcessRequest(qbxmlRequest);
        return ParseTransactionResponse(response);
    }

    private static (bool Success, string Message) ParseTransactionResponse(string xml)
    {
        var doc = XDocument.Parse(xml);

        // Look for any response element (*Rs) that has statusCode
        var responseElement = doc.Descendants()
            .FirstOrDefault(e => e.Attribute("statusCode") != null);

        if (responseElement == null)
        {
            return (false, "No status found in QB response.");
        }

        var statusCode = responseElement.Attribute("statusCode")!.Value;
        var statusMessage = responseElement.Attribute("statusMessage")?.Value ?? "Unknown";

        if (statusCode == "0")
        {
            return (true, statusMessage);
        }

        return (false, $"QB Error {statusCode}: {statusMessage}");
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (_ticket != null && _requestProcessor != null)
            {
                _requestProcessor.EndSession(_ticket);
                _ticket = null;
            }
        }
        catch (COMException) { }

        try
        {
            _requestProcessor?.CloseConnection();
        }
        catch (COMException) { }

        // Release the COM object so QB doesn't think we're still connected
        if (_requestProcessor != null)
        {
            try
            {
                Marshal.FinalReleaseComObject(_requestProcessor);
            }
            catch { }
            _requestProcessor = null;
        }

        _disposed = true;
    }

    ~QBConnectionManager() => Dispose(false);
}
