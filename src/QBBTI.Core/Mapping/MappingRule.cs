using System.Text.Json.Serialization;

namespace QBBTI.Core.Mapping;

public class MappingRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Pattern { get; set; } = string.Empty;
    public bool IsRegex { get; set; } = true;
    public string PayeeName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string? Memo { get; set; }
    public string EntityType { get; set; } = "Vendor"; // Vendor, Customer, Other

    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 100; // lower = checked first
}
