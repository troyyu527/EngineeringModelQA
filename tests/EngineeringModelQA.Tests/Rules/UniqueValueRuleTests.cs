using EngineeringModelQA.Core.Records;
using EngineeringModelQA.Core.Rules;
using static EngineeringModelQA.Tests.TestData;

namespace EngineeringModelQA.Tests.Rules;

public class UniqueValueRuleTests
{
    private readonly UniqueValueRule _rule = new();

    [Fact]
    public void DistinctValues_Pass()
    {
        var result = _rule.Evaluate(new[] { Element("a", mark: "B-1"), Element("b", mark: "B-2") }, Rule(RuleType.UniqueValue));

        Assert.Equal(2, result.PassedCount);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void SharedValue_FailsEveryElementInTheGroup()
    {
        var elements = new[] { Element("a", mark: "B-1"), Element("b", mark: "B-1"), Element("c", mark: "B-1"), Element("d", mark: "B-2") };

        var result = _rule.Evaluate(elements, Rule(RuleType.UniqueValue));

        Assert.Equal(new[] { "a", "b", "c" }, result.Findings.Select(f => f.ElementGlobalId));
        Assert.All(result.Findings, f => Assert.Equal("B-1", f.Observed));
        Assert.Contains("also used by 2 other element(s) in the project", result.Findings[0].Explanation);
        Assert.Equal(EvaluationStatus.Passed, StatusOf(result, "d"));
    }

    [Fact]
    public void Comparison_IsCaseSensitive_AndTrimmed()
    {
        var elements = new[] { Element("a", mark: "B-1"), Element("b", mark: "b-1"), Element("c", mark: " B-1 ") };

        var result = _rule.Evaluate(elements, Rule(RuleType.UniqueValue));

        Assert.Equal(new[] { "a", "c" }, result.Findings.Select(f => f.ElementGlobalId));
        Assert.Equal(EvaluationStatus.Passed, StatusOf(result, "b"));
    }

    [Fact]
    public void MissingOrBlank_IsNotEvaluated_AndDoesNotCount()
    {
        var elements = new[] { Element("a"), Element("b", mark: ""), Element("c", mark: " "), Element("d", mark: "B-1") };

        var result = _rule.Evaluate(elements, Rule(RuleType.UniqueValue));

        Assert.Equal(3, result.NotEvaluatedCount);
        Assert.Equal(EvaluationStatus.Passed, StatusOf(result, "d"));
    }

    [Fact]
    public void OutOfScopeElements_DoNotCreateDuplicates()
    {
        var elements = new[] { Element("a", mark: "X-1"), Element("s", SupportedEntityTypes.Slab, mark: "X-1") };

        var result = _rule.Evaluate(elements, Rule(RuleType.UniqueValue));

        Assert.Equal(EvaluationStatus.Passed, StatusOf(result, "a"));
        Assert.Equal(EvaluationStatus.NotEvaluated, StatusOf(result, "s"));
    }

    [Fact]
    public void StoreyScope_GroupsPerStorey_AndSkipsElementsWithoutStorey()
    {
        var elements = new[]
        {
            Element("a", mark: "B-1", storey: "Level 1"),
            Element("b", mark: "B-1", storey: "Level 2"),
            Element("c", mark: "B-2", storey: "Level 1"),
            Element("d", mark: "B-2", storey: "Level 1"),
            Element("e", mark: "B-1", storey: null),
        };

        var result = _rule.Evaluate(elements, Rule(RuleType.UniqueValue, scope: UniqueScope.Storey));

        Assert.Equal(new[] { "c", "d" }, result.Findings.Select(f => f.ElementGlobalId));
        Assert.Contains("in the same storey", result.Findings[0].Explanation);
        Assert.Equal(EvaluationStatus.Passed, StatusOf(result, "a"));
        Assert.Equal(EvaluationStatus.NotEvaluated, StatusOf(result, "e"));
    }
}
