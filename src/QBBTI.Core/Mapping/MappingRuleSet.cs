namespace QBBTI.Core.Mapping;

public class MappingRuleSet
{
    public int Version { get; set; } = 2;
    public Dictionary<string, CompanyMappings> Companies { get; set; } = new();
}

public class CompanyMappings
{
    public List<MappingRule> Rules { get; set; } = new();
}
