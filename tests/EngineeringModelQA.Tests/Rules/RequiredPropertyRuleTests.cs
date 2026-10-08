using EngineeringModelQA.Core.Records;
using EngineeringModelQA.Core.Rules;
using static EngineeringModelQA.Tests.TestData;

namespace EngineeringModelQA.Tests.Rules;

public class RequiredPropertyRuleTests
{
    private readonly RequiredPropertyRule _rule = new();
    private readonly RuleDefinition _definition = Rule(RuleType.RequiredProperty, severity: Severity.Error);

    [Fact]
    public void PresentValue_Passes()
    {
        var result = _rule.Evaluate(new[] { Element("a", mark: "B-1") }, _definition);

        Assert.Equal(EvaluationStatus.Passed, StatusOf(result, "a"));
        Assert.Empty(result.Findings);
    }

    [Theory]
    [InlineData(null, "(missing)")]
    [InlineData("", "(blank)")]
    [InlineData("   ", "(blank)")]
    public void MissingOrBlank_Fails(string? mark, string observed)
    {
        var result = _rule.Evaluate(new[] { Element("a", mark: mark) }, _definition);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(EvaluationStatus.Failed, StatusOf(result, "a"));
        Assert.Equal(observed, finding.Observed);
        Assert.Equal("R-1", finding.RuleId);
        Assert.Equal(MarkKey, finding.ConditionKey);
        Assert.Equal(Severity.Error, finding.Severity);
        Assert.Equal("Non-empty " + MarkKey, finding.Expected);
    }

    [Fact]
    public void OutOfScopeType_IsNotEvaluated()
    {
        var result = _rule.Evaluate(new[] { Element("s", SupportedEntityTypes.Slab) }, _definition);

        Assert.Equal(EvaluationStatus.NotEvaluated, StatusOf(result, "s"));
        Assert.Empty(result.Findings);
        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public void ExcludedElement_IsNotEvaluated()
    {
        var definition = Rule(RuleType.RequiredProperty, exclusions: new PropertyExclusion("Pset_BeamCommon.Status", "temporary"));
        var element = Element("a", properties: ("Pset_BeamCommon.Status", "TEMPORARY"));

        var result = _rule.Evaluate(new[] { element }, definition);

        Assert.Equal(EvaluationStatus.NotEvaluated, StatusOf(result, "a"));
    }

    [Fact]
    public void EveryElementGetsOneStatus_InModelOrder()
    {
        var elements = new[] { Element("a", mark: "B-1"), Element("b"), Element("c", SupportedEntityTypes.Wall) };

        var result = _rule.Evaluate(elements, _definition);

        Assert.Equal(new[] { "a", "b", "c" }, result.Statuses.Select(s => s.Key));
        Assert.Equal((1, 1, 1), (result.PassedCount, result.FailedCount, result.NotEvaluatedCount));
    }

    [Fact]
    public void WrongRuleType_Throws() =>
        Assert.Throws<ArgumentException>(() => _rule.Evaluate(Array.Empty<ElementRecord>(), Rule(RuleType.UniqueValue)));
}
