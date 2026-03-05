namespace QBBTI.Core.Models;

public record ExistingTransaction(
    DateTime Date,
    decimal Amount,
    bool IsDebit,
    string? Memo,
    string? PayeeName,
    string? RefNumber);
