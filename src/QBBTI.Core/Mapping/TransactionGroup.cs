using QBBTI.Core.Models;

namespace QBBTI.Core.Mapping;

public class TransactionGroup
{
    public string GroupKey { get; set; } = string.Empty;
    public MappingRule? MatchedRule { get; set; }
    public List<BankTransaction> Transactions { get; set; } = [];
    public bool IsAutoMapped => MatchedRule != null;
    public decimal Total => Transactions.Sum(t => t.IsDebit ? -t.Amount : t.Amount);
}
