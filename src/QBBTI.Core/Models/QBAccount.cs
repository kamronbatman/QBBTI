namespace QBBTI.Core.Models;

public class QBAccount
{
    public string ListID { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string AccountType { get; set; } = string.Empty; // Bank, CreditCard, Income, Expense, etc.
    public decimal Balance { get; set; }

    public bool IsBankAccount => AccountType is "Bank" or "CreditCard";
}
