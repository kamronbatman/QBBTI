using System.Text.RegularExpressions;
using QBBTI.Core.Models;

namespace QBBTI.Core.Parsing;

public partial class OfxParser : IBankFileParser
{
    private static readonly Dictionary<string, string> TrnTypeMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CHECK"] = "CHECK_PAID",
        ["DEBIT"] = "ACH_DEBIT",
        ["CREDIT"] = "CREDIT",
        ["FEE"] = "FEE_TRANSACTION"
    };

    public string DisplayName => "OFX/QFX/QBO";
    public IReadOnlyList<string> SupportedExtensions { get; } = [".ofx", ".qfx", ".qbo"];

    public bool CanParse(string filePath)
    {
        try
        {
            var lines = File.ReadLines(filePath).Take(20);
            foreach (var line in lines)
            {
                if (line.Contains("OFXHEADER:", StringComparison.OrdinalIgnoreCase) ||
                    line.TrimStart().StartsWith("<?OFX", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }
        catch
        {
            return false;
        }
    }

    public List<BankTransaction> Parse(string filePath)
    {
        var transactions = new List<BankTransaction>();
        var content = File.ReadAllText(filePath);

        // Skip plain-text header — real data starts at <OFX>
        var ofxStart = content.IndexOf("<OFX>", StringComparison.OrdinalIgnoreCase);
        if (ofxStart < 0)
        {
            return transactions;
        }
        content = content[ofxStart..];

        foreach (Match stmtMatch in StmtTrnRegex().Matches(content))
        {
            var block = stmtMatch.Groups[1].Value;
            var tags = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match tagMatch in TagValueRegex().Matches(block))
            {
                tags[tagMatch.Groups[1].Value] = tagMatch.Groups[2].Value.Trim();
            }

            if (!tags.TryGetValue("DTPOSTED", out var dtPosted) ||
                !tags.TryGetValue("TRNAMT", out var trnAmtStr))
            {
                continue;
            }

            // Parse date: yyyyMMdd prefix (ignore time/timezone)
            if (dtPosted.Length < 8 ||
                !DateTime.TryParseExact(dtPosted[..8], "yyyyMMdd", null,
                    System.Globalization.DateTimeStyles.None, out var date))
            {
                continue;
            }

            if (!decimal.TryParse(trnAmtStr, out var trnAmt))
            {
                continue;
            }

            tags.TryGetValue("TRNTYPE", out var trnType);
            trnType ??= "";
            tags.TryGetValue("NAME", out var name);
            name ??= "";
            tags.TryGetValue("MEMO", out var memo);
            tags.TryGetValue("CHECKNUM", out var checkNum);

            var mappedType = TrnTypeMap.GetValueOrDefault(trnType, trnType);
            var isDebit = trnAmt < 0;

            var txn = new BankTransaction
            {
                Date = date,
                RawDescription = name,
                Payee = ExtractPayee(name, mappedType, checkNum),
                Amount = Math.Abs(trnAmt),
                IsDebit = isDebit,
                CheckNumber = checkNum,
                TransactionType = mappedType,
                Memo = memo
            };

            transactions.Add(txn);
        }

        return transactions;
    }

    private static string ExtractPayee(string name, string txnType, string? checkNumber)
    {
        if (txnType == "CHECK_PAID" && checkNumber != null)
        {
            return $"Check #{checkNumber}";
        }

        var origCoMatch = OrigCoRegex().Match(name);
        if (origCoMatch.Success)
        {
            var coName = origCoMatch.Groups[1].Value.Trim();
            return CleanPayeeName(coName);
        }

        if (name.Equals("MONTHLY SERVICE FEE", StringComparison.OrdinalIgnoreCase))
        {
            return "Chase Bank";
        }

        if (name.Contains("FEE REVERSAL", StringComparison.OrdinalIgnoreCase))
        {
            return "Chase Bank";
        }

        if (name.StartsWith("REMOTE ONLINE DEPOSIT", StringComparison.OrdinalIgnoreCase))
        {
            return "Remote Deposit";
        }

        if (name.StartsWith("DEPOSIT", StringComparison.OrdinalIgnoreCase))
        {
            return "Deposit";
        }

        return name.Length > 40 ? name[..40].Trim() : name;
    }

    private static string CleanPayeeName(string name)
    {
        return System.Globalization.CultureInfo.CurrentCulture.TextInfo
            .ToTitleCase(name.ToLower());
    }

    [GeneratedRegex("<STMTTRN>(.*?)</STMTTRN>", RegexOptions.Singleline)]
    private static partial Regex StmtTrnRegex();

    [GeneratedRegex(@"<(\w+)>([^<\r\n]+)")]
    private static partial Regex TagValueRegex();

    [GeneratedRegex(@"ORIG CO NAME:\s*(\S+(?:\s+\S+)*?)(?:\s{2,}|$)")]
    private static partial Regex OrigCoRegex();
}
