using System.Xml.Linq;
using QBBTI.Core.Models;

namespace QBBTI.Core.QuickBooks;

public static class QBXmlBuilder
{
    private const string QbxmlVersion = "17.0";

    public static string BuildAccountQuery()
    {
        return WrapRequest(
            new XElement("AccountQueryRq",
                new XAttribute("requestID", "1")));
    }

    public static string BuildCompanyQuery()
    {
        return WrapRequest(
            new XElement("CompanyQueryRq",
                new XAttribute("requestID", "1")));
    }

    /// <summary>
    /// Builds CheckAdd QBXML for a debit transaction.
    /// For actual checks (CHECK_PAID): includes RefNumber and IsToBePrinted=true.
    /// For ACH debits, fees, etc.: sets IsToBePrinted=false and omits RefNumber
    /// so QuickBooks does NOT assign a check number.
    /// PayeeEntityRef is optional - if null, payee info goes into Memo only.
    /// </summary>
    public static string BuildCheckAdd(
        string bankAccountName,
        string expenseAccountName,
        decimal amount,
        DateTime txnDate,
        string? memo = null,
        string? payeeName = null,
        string? checkNumber = null)
    {
        var checkAdd = new XElement("CheckAdd",
            new XElement("AccountRef",
                new XElement("FullName", bankAccountName)));

        // Only include PayeeEntityRef if we have a payee name.
        // Note: QB requires the payee to exist as a Vendor/Customer/Employee.
        // If the entity doesn't exist, the request will fail.
        if (payeeName != null)
        {
            checkAdd.Add(new XElement("PayeeEntityRef",
                new XElement("FullName", payeeName)));
        }

        // Set check number if present, otherwise blank to prevent QB auto-numbering.
        checkAdd.Add(new XElement("RefNumber", checkNumber ?? ""));

        checkAdd.Add(new XElement("TxnDate", txnDate.ToString("yyyy-MM-dd")));

        if (memo != null)
        {
            checkAdd.Add(new XElement("Memo", memo));
        }

        // No check number = not a printed check
        if (checkNumber == null)
        {
            checkAdd.Add(new XElement("IsToBePrinted", "false"));
        }

        checkAdd.Add(new XElement("ExpenseLineAdd",
            new XElement("AccountRef",
                new XElement("FullName", expenseAccountName)),
            new XElement("Amount", amount.ToString("F2")),
            new XElement("Memo", memo ?? "")));

        return WrapRequest(
            new XElement("CheckAddRq",
                new XAttribute("requestID", "1"),
                checkAdd));
    }

    /// <summary>
    /// Builds DepositAdd QBXML for a credit transaction.
    /// DepositAdd has no top-level payee - entity info goes on the deposit line.
    /// </summary>
    public static string BuildDepositAdd(
        string bankAccountName,
        string depositFromAccountName,
        decimal amount,
        DateTime txnDate,
        string? memo = null,
        string? entityName = null)
    {
        var depositAdd = new XElement("DepositAdd",
            new XElement("TxnDate", txnDate.ToString("yyyy-MM-dd")),
            new XElement("DepositToAccountRef",
                new XElement("FullName", bankAccountName)));

        if (memo != null)
        {
            depositAdd.Add(new XElement("Memo", memo));
        }

        var depositLine = new XElement("DepositLineAdd",
            new XElement("AccountRef",
                new XElement("FullName", depositFromAccountName)),
            new XElement("Memo", memo ?? ""),
            new XElement("Amount", amount.ToString("F2")));

        // EntityRef on deposit line is optional - only include if we have one
        if (entityName != null)
        {
            depositLine.AddFirst(new XElement("EntityRef",
                new XElement("FullName", entityName)));
        }

        depositAdd.Add(depositLine);

        return WrapRequest(
            new XElement("DepositAddRq",
                new XAttribute("requestID", "1"),
                depositAdd));
    }

    // --- Entity (Vendor/Customer/OtherName) operations ---

    public static string BuildVendorQuery()
    {
        return WrapRequest(
            new XElement("VendorQueryRq",
                new XAttribute("requestID", "1")));
    }

    public static string BuildCustomerQuery()
    {
        return WrapRequest(
            new XElement("CustomerQueryRq",
                new XAttribute("requestID", "1")));
    }

    public static string BuildOtherNameQuery()
    {
        return WrapRequest(
            new XElement("OtherNameQueryRq",
                new XAttribute("requestID", "1")));
    }

    public static string BuildVendorAdd(string name)
    {
        return WrapRequest(
            new XElement("VendorAddRq",
                new XAttribute("requestID", "1"),
                new XElement("VendorAdd",
                    new XElement("Name", name))));
    }

    public static string BuildCustomerAdd(string name)
    {
        return WrapRequest(
            new XElement("CustomerAddRq",
                new XAttribute("requestID", "1"),
                new XElement("CustomerAdd",
                    new XElement("Name", name))));
    }

    public static string BuildOtherNameAdd(string name)
    {
        return WrapRequest(
            new XElement("OtherNameAddRq",
                new XAttribute("requestID", "1"),
                new XElement("OtherNameAdd",
                    new XElement("Name", name))));
    }

    // --- Transaction builders ---

    /// <summary>
    /// Builds the appropriate QBXML request for a BankTransaction.
    /// Automatically selects CheckAdd vs DepositAdd and sets correct flags.
    /// Payee is included in PayeeEntityRef/EntityRef (must exist in QB).
    /// </summary>
    public static string BuildTransactionRequest(BankTransaction txn, string bankAccountName)
    {
        if (txn.IsDebit)
        {
            var payee = string.IsNullOrEmpty(txn.Payee) ? null : txn.Payee;
            return BuildCheckAdd(
                bankAccountName,
                txn.MappedAccountName ?? "Ask My Accountant",
                txn.Amount,
                txn.Date,
                memo: txn.Memo ?? txn.RawDescription,
                payeeName: payee,
                checkNumber: txn.CheckNumber);
        }

        var entity = string.IsNullOrEmpty(txn.Payee) ? null : txn.Payee;
        return BuildDepositAdd(
            bankAccountName,
            txn.MappedAccountName ?? "Ask My Accountant",
            txn.Amount,
            txn.Date,
            memo: txn.Memo ?? txn.RawDescription,
            entityName: entity);
    }

    private static string WrapRequest(XElement requestElement)
    {
        var body = new XElement("QBXML",
            new XElement("QBXMLMsgsRq",
                new XAttribute("onError", "stopOnError"),
                requestElement));

        return $"<?qbxml version=\"{QbxmlVersion}\"?>\n{body}";
    }
}
