namespace QBBTI.Core.Parsing;

public class BankFileParserFactory
{
    private readonly List<IBankFileParser> _parsers = [new ChaseCsvParser(), new OfxParser()];

    public IReadOnlyList<IBankFileParser> AvailableParsers => _parsers;

    public IBankFileParser? DetectParser(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        for (var i = 0; i < _parsers.Count; i++)
        {
            var parser = _parsers[i];
            if (parser.SupportedExtensions.Contains(ext) && parser.CanParse(filePath))
            {
                return parser;
            }
        }

        return null;
    }
}
