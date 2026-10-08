using EngineeringModelQA.Core.Records;

namespace EngineeringModelQA.Core.Rules;

/// <summary>Shared scope, exclusion and result helpers for the rule implementations.</summary>
public static class RuleScope
{
    /// <summary>True when the element's type is in the rule's entity types and no exclusion matches.</summary>
    public static bool AppliesTo(ElementRecord element, RuleDefinition rule) =>
        rule.EntityTypes.Contains(element.EntityType, StringComparer.Ordinal) && !IsExcluded(element, rule);

    /// <summary>An exclusion matches when the property exists and equals the value (trimmed, case-insensitive).</summary>
    public static bool IsExcluded(ElementRecord element, RuleDefinition rule) =>
        rule.Exclusions.Any(x => element.GetProperty(x.Property) is { } value &&
                                 string.Equals(value.Trim(), x.EqualsValue.Trim(), StringComparison.OrdinalIgnoreCase));

    internal static string RequirePropertyKey(RuleDefinition rule) =>
        rule.PropertyKey ?? throw new ArgumentException($"Rule '{rule.Id}' needs a property set and property name.", nameof(rule));

    internal static void RequireType(RuleDefinition rule, RuleType type)
    {
        if (rule.Type != type)
            throw new ArgumentException($"Rule '{rule.Id}' is {rule.Type}, not {type}.", nameof(rule));
    }

    internal static Finding CreateFinding(RuleDefinition rule, ElementRecord element, string conditionKey,
        string expected, string observed, string explanation, string suggestedAction) =>
        new(rule.Id, rule.Name, rule.Type, element.GlobalId, element.EntityType, element.Name, conditionKey,
            rule.Severity, expected, observed, explanation, suggestedAction);

    /// <summary>Collects statuses and findings in model order.</summary>
    internal sealed class Builder(RuleDefinition rule)
    {
        private readonly List<KeyValuePair<string, EvaluationStatus>> _statuses = new();
        private readonly List<Finding> _findings = new();

        public void Pass(ElementRecord element) => Add(element, EvaluationStatus.Passed);

        public void Skip(ElementRecord element) => Add(element, EvaluationStatus.NotEvaluated);

        public void Fail(Finding finding)
        {
            _statuses.Add(new KeyValuePair<string, EvaluationStatus>(finding.ElementGlobalId, EvaluationStatus.Failed));
            _findings.Add(finding);
        }

        public RuleResult Build() => new(rule, _statuses, _findings);

        private void Add(ElementRecord element, EvaluationStatus status) =>
            _statuses.Add(new KeyValuePair<string, EvaluationStatus>(element.GlobalId, status));
    }
}
