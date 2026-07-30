namespace Zlet.FolderConverter.Core.Models;

public sealed class RuleSet
{
    private readonly IReadOnlyDictionary<SourceFormat, ConversionRule> _rules;

    private RuleSet(IReadOnlyDictionary<SourceFormat, ConversionRule> rules)
    {
        _rules = rules;
    }

    public IReadOnlyCollection<ConversionRule> Rules => _rules.Values.ToArray();

    public static RuleSet CreateDefault() =>
        new(FormatCapabilityCatalog.All.ToDictionary(
            capability => capability.SourceFormat,
            capability => new ConversionRule(capability.SourceFormat, capability.DefaultTarget)));

    public ConversionRule GetRule(SourceFormat sourceFormat) => _rules[sourceFormat];

    public RuleSet WithRule(SourceFormat sourceFormat, ConversionTarget target)
    {
        var capability = FormatCapabilityCatalog.Get(sourceFormat);
        if (!capability.Supports(target))
        {
            throw new ArgumentException(
                $"{sourceFormat} cannot be converted to {target}.",
                nameof(target));
        }

        var rules = _rules.ToDictionary(pair => pair.Key, pair => pair.Value);
        rules[sourceFormat] = new ConversionRule(sourceFormat, target);
        return new RuleSet(rules);
    }
}
