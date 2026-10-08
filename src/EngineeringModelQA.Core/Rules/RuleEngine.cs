using EngineeringModelQA.Core.Records;

namespace EngineeringModelQA.Core.Rules;

/// <summary>Picks the <see cref="IRule"/> implementation for a rule definition's type.</summary>
public sealed class RuleEngine
{
    private readonly Dictionary<RuleType, IRule> _rules;

    public RuleEngine()
        : this(new IRule[]
        {
            new RequiredPropertyRule(), new UniqueValueRule(), new NamingPatternRule(),
            new MaterialAssignedRule(), new StoreyAssignedRule(),
        })
    {
    }

    public RuleEngine(IEnumerable<IRule> rules) => _rules = rules.ToDictionary(r => r.Type);

    public RuleResult Evaluate(IReadOnlyList<ElementRecord> elements, RuleDefinition rule) =>
        _rules.TryGetValue(rule.Type, out var implementation)
            ? implementation.Evaluate(elements, rule)
            : throw new NotSupportedException($"No implementation for rule type {rule.Type}.");
}
