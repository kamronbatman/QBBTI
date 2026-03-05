namespace QBBTI.Core.Models;

public class BankTransaction
{
    public DateTime Date { get; set; }
    public string Payee { get; set; } = string.Empty;
    public string RawDescription { get; set; } = string.Empty;
    public decimal Amount { get; set; }           // always positive
    public bool IsDebit { get; set; }
    public string? CheckNumber { get; set; }
    public string? Memo { get; set; }
    public string TransactionType { get; set; } = string.Empty; // ACH_DEBIT, DEPOSIT, etc.

    // Mapping fields (set during review)
    public string? MappedAccountName { get; set; }  // QB expense/income account
    public bool IsSelected { get; set; }
    public bool IsAutoMapped { get; set; }
    public string EntityType { get; set; } = "Vendor"; // Vendor, Customer, Other (for Quick Add)
    public string? MatchedRuleId { get; set; }
    public bool IsPossibleDuplicate { get; set; }
}
