using QBBTI.Core.Models;

namespace QBBTI.Core.Parsing;

public interface IBankFileParser
{
    string DisplayName { get; }
    IReadOnlyList<string> SupportedExtensions { get; }
    bool CanParse(string filePath);
    List<BankTransaction> Parse(string filePath);
}
