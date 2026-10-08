using EngineeringModelQA.Core.Records;

namespace EngineeringModelQA.Core.Rules;

/// <summary>
/// Fails every in-scope element whose value (trimmed, case-sensitive) is shared with another in-scope element,
/// project-wide or per storey. Missing or blank values are not evaluated: the required-property rule reports them.
/// </summary>
public sealed class UniqueValueRule : IRule
{
    public RuleType Type => RuleType.UniqueValue;

    public RuleResult Evaluate(IReadOnlyList<ElementRecord> elements, RuleDefinition rule)
    {
        RuleScope.RequireType(rule, Type);
        var key = RuleScope.RequirePropertyKey(rule);
        var perStorey = rule.UniqueScope == UniqueScope.Storey;

        // group key -> number of in-scope elements using it
        string? GroupOf(ElementRecord element)
        {
            if (!RuleScope.AppliesTo(element, rule)) return null;
            var value = element.GetProperty(key);
            if (string.IsNullOrWhiteSpace(value)) return null;
            if (perStorey && string.IsNullOrWhiteSpace(element.Storey)) return null;
            return (perStorey ? element.Storey : "") + "\u001F" + value!.Trim();
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var element in elements)
        {
            if (GroupOf(element) is { } group)
                counts[group] = counts.TryGetValue(group, out var n) ? n + 1 : 1;
        }

        var scopeText = perStorey ? "in the same storey" : "in the project";
        var result = new RuleScope.Builder(rule);
        foreach (var element in elements)
        {
            var group = GroupOf(element);
            if (group is null)
            {
                result.Skip(element);
                continue;
            }

            var count = counts[group];
            if (count == 1)
            {
                result.Pass(element);
                continue;
            }

            var value = element.GetProperty(key)!.Trim();
            result.Fail(RuleScope.CreateFinding(rule, element, key,
                expected: $"{key} used by one element only ({(perStorey ? "per storey" : "project-wide")})",
                observed: value,
                explanation: $"'{value}' is also used by {count - 1} other element(s) {scopeText}.",
                suggestedAction: "Give each element its own mark in the source model and export the IFC again."));
        }

        return result.Build();
    }
}
