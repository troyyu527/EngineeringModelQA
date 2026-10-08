using EngineeringModelQA.Core.Records;

namespace EngineeringModelQA.Core.Rules;

/// <summary>Fails when the configured property is missing, empty or whitespace.</summary>
public sealed class RequiredPropertyRule : IRule
{
    public RuleType Type => RuleType.RequiredProperty;

    public RuleResult Evaluate(IReadOnlyList<ElementRecord> elements, RuleDefinition rule)
    {
        RuleScope.RequireType(rule, Type);
        var key = RuleScope.RequirePropertyKey(rule);
        var result = new RuleScope.Builder(rule);

        foreach (var element in elements)
        {
            if (!RuleScope.AppliesTo(element, rule))
            {
                result.Skip(element);
                continue;
            }

            var value = element.GetProperty(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                result.Pass(element);
                continue;
            }

            var missing = value is null;
            result.Fail(RuleScope.CreateFinding(rule, element, key,
                expected: $"Non-empty {key}",
                observed: missing ? "(missing)" : "(blank)",
                explanation: missing ? $"The element has no {key} property." : $"{key} exists but is blank.",
                suggestedAction: $"Fill in {key} in the source model and export the IFC again."));
        }

        return result.Build();
    }
}
