using System.Text.RegularExpressions;
using QBBTI.Core.Models;

namespace QBBTI.Core.Parsing;

public partial class ChaseCsvParser : IBankFileParser
{
    private static readonly string[] ChaseHeaders = ["Details", "Posting Date", "Description", "Amount", "Type", "Balance"
    ];

    // Transaction types that are debits
    private static readonly HashSet<string> DebitTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ACH_DEBIT", "CHECK_PAID", "FEE_TRANSACTION"
    };

    public string DisplayName => "Chase Bank (CSV)";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".csv"];

    public bool CanParse(string filePath)
    {
        try
        {
            var firstLine = File.ReadLines(filePath).FirstOrDefault();
            if (firstLine == null)
            {
                return false;
            }

            return ChaseHeaders.All(h => firstLine.Contains(h, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Parses a Chase bank CSV file into BankTransaction objects.
    /// Expected columns: Details, Posting Date, Description, Amount, Type, Balance, Check or Slip #
    /// </summary>
    public List<BankTransaction> Parse(string filePath)
    {
        var transactions = new List<BankTransaction>();
        var lines = File.ReadAllLines(filePath);

        if (lines.Length < 2)
        {
            return transactions;
        }

        // Skip header row
        for (var i = 1; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line);
            if (fields.Count < 6)
            {
                continue;
            }

            // Columns: Details(0), Posting Date(1), Description(2), Amount(3), Type(4), Balance(5), Check or Slip #(6)
            var rawAmount = decimal.Parse(fields[3]);
            var txnType = fields[4].Trim();
            var description = fields[2].Trim().Trim('"');
            var checkNumber = fields.Count > 6 ? fields[6].Trim() : null;
            if (string.IsNullOrEmpty(checkNumber))
            {
                checkNumber = null;
            }

            var txn = new BankTransaction
            {
                Date = DateTime.Parse(fields[1].Trim()),
                RawDescription = description,
                Payee = ExtractPayee(description, txnType, checkNumber),
                Amount = Math.Abs(rawAmount),
                IsDebit = DebitTypes.Contains(txnType),
                CheckNumber = checkNumber,
                TransactionType = txnType,
                Memo = BuildMemo(description, txnType)
            };

            transactions.Add(txn);
        }

        return transactions;
    }

    /// <summary>
    /// Parses a CSV line handling quoted fields with internal commas.
    /// </summary>
    private List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var inQuotes = false;
        var fieldStart = 0;

        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (line[i] == ',' && !inQuotes)
            {
                fields.Add(line[fieldStart..i]);
                fieldStart = i + 1;
            }
        }

        // Add the last field
        if (fieldStart <= line.Length)
        {
            fields.Add(line[fieldStart..]);
        }

        return fields;
    }

    /// <summary>
    /// Extracts a clean payee name from the raw description.
    /// ACH descriptions contain "ORIG CO NAME:company_name" which we extract.
    /// </summary>
    private string ExtractPayee(string description, string txnType, string? checkNumber)
    {
        // For checks, use "CHECK {number}" format
        if (txnType == "CHECK_PAID" && checkNumber != null)
        {
            return $"Check #{checkNumber}";
        }

        // For ACH transactions, extract company name from "ORIG CO NAME:xxx"
        var origCoMatch = origCoRegex().Match(description);
        if (origCoMatch.Success)
        {
            var name = origCoMatch.Groups[1].Value.Trim();
            return CleanPayeeName(name);
        }

        // For fee transactions
        if (txnType == "FEE_TRANSACTION")
        {
            return "Chase Bank";
        }

        if (txnType == "REFUND_TRANSACTION" && description.Contains("FEE REVERSAL"))
        {
            return "Chase Bank";
        }

        // For deposits, clean up the description
        if (description.StartsWith("REMOTE ONLINE DEPOSIT"))
        {
            return "Remote Deposit";
        }

        if (description.StartsWith("DEPOSIT"))
        {
            return "Deposit";
        }

        if (description.StartsWith("CHECK DEPOSIT") || txnType == "CHECK_DEPOSIT")
        {
            return "Check Deposit";
        }

        return description.Length > 40 ? description[..40].Trim() : description;
    }

    private static string CleanPayeeName(string name)
    {
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(name.ToLower());
    }

    private static string BuildMemo(string description, string txnType)
    {
        // For ACH, extract the CO ENTRY DESCR field
        var entryDescrMatch = DescrRegex().Match(description);
        if (entryDescrMatch.Success)
        {
            var descr = entryDescrMatch.Groups[1].Value;
            // e.g. "FEE 518519" or "NET 477367" or "TAX 477368"
            if (descr.StartsWith("FEE", StringComparison.OrdinalIgnoreCase))
            {
                return "Service fee";
            }

            if (descr.StartsWith("NET", StringComparison.OrdinalIgnoreCase))
            {
                return "Payroll - net pay";
            }

            if (descr.StartsWith("TAX", StringComparison.OrdinalIgnoreCase))
            {
                return "Payroll tax";
            }

            return descr;
        }

        // For simple descriptions, use as-is
        if (description.Length <= 80)
        {
            return description;
        }

        return description[..80].Trim();
    }

    [GeneratedRegex(@"CO ENTRY DESCR:(\S+)")]
    private static partial Regex DescrRegex();
    [GeneratedRegex(@"ORIG CO NAME:\s*(\S+(?:\s+\S+)*?)\s{2,}")]
    private static partial Regex origCoRegex();
}
