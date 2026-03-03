using QBBTI.Core.Models;

namespace QBBTI.Core.Mapping;

public class TransactionGrouper
{
    public List<TransactionGroup> Group(List<BankTransaction> transactions, MappingEngine engine)
    {
        var groups = new Dictionary<string, TransactionGroup>();

        foreach (var txn in transactions)
        {
            string key;
            MappingRule? rule = null;

            if (txn.MatchedRuleId != null)
            {
                key = $"rule:{txn.MatchedRuleId}";
                rule = engine.Rules.FirstOrDefault(r => r.Id == txn.MatchedRuleId);
            }
            else
            {
                key = $"payee:{txn.Payee}";
            }

            if (!groups.TryGetValue(key, out var group))
            {
                group = new TransactionGroup
                {
                    GroupKey = key,
                    MatchedRule = rule
                };
                groups[key] = group;
            }

            group.Transactions.Add(txn);
        }

        return groups.Values.OrderBy(g => !g.IsAutoMapped).ThenBy(g => g.GroupKey).ToList();
    }

    public (TransactionGroup original, TransactionGroup newGroup) Ungroup(
        TransactionGroup group, BankTransaction transaction)
    {
        group.Transactions.Remove(transaction);

        var standalone = new TransactionGroup
        {
            GroupKey = $"payee:{transaction.Payee}",
            Transactions = { transaction }
        };

        return (group, standalone);
    }
}
