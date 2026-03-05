using System.Text.Json;
using System.Text.RegularExpressions;
using QBBTI.Core.Models;

namespace QBBTI.Core.Mapping;

public class MappingEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _mappingsFilePath;
    private readonly string _companyName;
    private readonly MappingRuleSet _fullRuleSet;
    private readonly List<MappingRule> _rules;
    private readonly Dictionary<string, Regex> _regexCache = new();

    public IReadOnlyList<MappingRule> Rules => _rules;

    public MappingEngine(string mappingsFilePath, string companyName)
    {
        _mappingsFilePath = mappingsFilePath;
        _companyName = companyName;
        _fullRuleSet = LoadFullRuleSet();
        _rules = LoadCompanyRules();
    }

    private MappingRuleSet LoadFullRuleSet()
    {
        if (!File.Exists(_mappingsFilePath))
        {
            return new MappingRuleSet();
        }

        var json = File.ReadAllText(_mappingsFilePath);
        return JsonSerializer.Deserialize<MappingRuleSet>(json, JsonOptions) ?? new MappingRuleSet();
    }

    private List<MappingRule> LoadCompanyRules()
    {
        if (_fullRuleSet.Companies.TryGetValue(_companyName, out var companyMappings))
        {
            return companyMappings.Rules;
        }

        return new List<MappingRule>();
    }

    private void Save()
    {
        _fullRuleSet.Companies[_companyName] = new CompanyMappings { Rules = _rules };
        var json = JsonSerializer.Serialize(_fullRuleSet, JsonOptions);
        File.WriteAllText(_mappingsFilePath, json);
    }

    public void ApplyMappings(List<BankTransaction> transactions)
    {
        foreach (var txn in transactions)
        {
            var rule = FindMatchingRule(txn);
            if (rule != null)
            {
                txn.Payee = rule.PayeeName;
                txn.MappedAccountName = rule.AccountName;
                txn.EntityType = rule.EntityType;
                txn.MatchedRuleId = rule.Id;
                txn.IsAutoMapped = true;

                if (!string.IsNullOrEmpty(rule.Memo))
                {
                    txn.Memo = rule.Memo;
                }
            }
        }
    }

    public MappingRule? FindMatchingRule(BankTransaction txn)
    {
        foreach (var rule in _rules.OrderBy(r => r.Priority))
        {
            if (IsMatch(rule, txn))
            {
                return rule;
            }
        }

        return null;
    }

    private bool IsMatch(MappingRule rule, BankTransaction txn)
    {
        bool patternMatch;
        if (rule.IsRegex)
        {
            var regex = GetOrCreateRegex(rule.Pattern);
            patternMatch = regex.IsMatch(txn.RawDescription);
        }
        else
        {
            patternMatch = txn.RawDescription.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase);
        }

        if (!patternMatch)
            return false;

        if (rule.MinAmount.HasValue && txn.Amount < rule.MinAmount.Value)
            return false;

        if (rule.MaxAmount.HasValue && txn.Amount > rule.MaxAmount.Value)
            return false;

        return true;
    }

    private Regex GetOrCreateRegex(string pattern)
    {
        if (!_regexCache.TryGetValue(pattern, out var regex))
        {
            regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
            _regexCache[pattern] = regex;
        }

        return regex;
    }

    public void SaveRule(MappingRule rule)
    {
        var existing = _rules.FirstOrDefault(r => r.Id == rule.Id);
        if (existing != null)
        {
            _rules.Remove(existing);
        }

        _rules.Add(rule);
        Save();
    }

    public void DeleteRule(string id)
    {
        _rules.RemoveAll(r => r.Id == id);
        Save();
    }

    private static readonly Regex AchOrigCoName = new(@"ORIG CO NAME:([^/]+?)(?:\s*/|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AchCoEntryDescr = new(@"CO ENTRY DESCR:(\S+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string SuggestPattern(string rawDescription)
    {
        var origMatch = AchOrigCoName.Match(rawDescription);
        var entryMatch = AchCoEntryDescr.Match(rawDescription);

        if (origMatch.Success && entryMatch.Success)
        {
            var origName = Regex.Escape(origMatch.Groups[1].Value.Trim());
            var entryDescr = Regex.Escape(entryMatch.Groups[1].Value.Trim());
            return $"ORIG CO NAME:{origName}.*CO ENTRY DESCR:{entryDescr}";
        }

        if (origMatch.Success)
        {
            var origName = Regex.Escape(origMatch.Groups[1].Value.Trim());
            return $"ORIG CO NAME:{origName}";
        }

        // Fallback: first 30 chars
        var len = Math.Min(rawDescription.Length, 30);
        return rawDescription[..len];
    }

    public static (decimal? Min, decimal? Max) SuggestAmountRange(IEnumerable<BankTransaction> transactions)
    {
        var amounts = transactions.Select(t => t.Amount).Distinct().ToList();
        if (amounts.Count <= 1)
            return (null, null);

        var min = amounts.Min();
        var max = amounts.Max();
        var padding = (max - min) * 0.10m;

        return (Math.Max(0, min - padding), max + padding);
    }
}
