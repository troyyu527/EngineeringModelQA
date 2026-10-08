using System.Text.RegularExpressions;
using EngineeringModelQA.Core.Records;

namespace EngineeringModelQA.Core.Rules;

/// <summary>
/// Fails when the value does not match the rule's regular expression (unanchored unless the pattern uses ^…$).
/// Missing or blank values and matches that time out are not evaluated.
/// </summary>
public sealed class NamingPatternRule : IRule
{
    /// <summary>Upper bound for one match, so a bad pattern cannot hang a run.</summary>
    public static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(200);

    public RuleType Type => RuleType.NamingPattern;

    public RuleResult Evaluate(IReadOnlyList<ElementRecord> elements, RuleDefinition rule)
    {
        RuleScope.RequireType(rule, Type);
        var key = RuleScope.RequirePropertyKey(rule);
        var pattern = rule.Pattern ?? throw new ArgumentException($"Rule '{rule.Id}' needs a pattern.", nameof(rule));
        var regex = new Regex(pattern, RegexOptions.CultureInvariant, MatchTimeout);
        var result = new RuleScope.Builder(rule);

        foreach (var element in elements)
        {
            var value = RuleScope.AppliesTo(element, rule) ? element.GetProperty(key) : null;
            if (string.IsNullOrWhiteSpace(value))
            {
                result.Skip(element);
                continue;
            }

            bool matches;
            try
            {
                matches = regex.IsMatch(value);
            }
            catch (RegexMatchTimeoutException)
            {
                result.Skip(element);
                continue;
            }

            if (matches)
            {
                result.Pass(element);
                continue;
            }

            result.Fail(RuleScope.CreateFinding(rule, element, key,
                expected: $"Matches {pattern}",
                observed: value!,
                explanation: $"'{value}' does not match the naming pattern {pattern}.",
                suggestedAction: "Rename the mark in the source model to follow the project naming rule."));
        }

        return result.Build();
    }
}
