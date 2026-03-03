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

                if (rule.Memo != null)
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
            if (IsMatch(rule, txn.RawDescription))
            {
                return rule;
            }
        }

        return null;
    }

    private bool IsMatch(MappingRule rule, string description)
    {
        if (rule.IsRegex)
        {
            var regex = GetOrCreateRegex(rule.Pattern);
            return regex.IsMatch(description);
        }

        return description.Contains(rule.Pattern, StringComparison.OrdinalIgnoreCase);
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
}
