using System.Diagnostics;
using EngineeringModelQA.Core.Records;
using EngineeringModelQA.Core.Rules;
using static EngineeringModelQA.Tests.TestData;

namespace EngineeringModelQA.Tests.Rules;

public class NamingPatternRuleTests
{
    private readonly NamingPatternRule _rule = new();

    [Theory]
    [InlineData("B-101", EvaluationStatus.Passed)]
    [InlineData("C-999", EvaluationStatus.Passed)]
    [InlineData("B101", EvaluationStatus.Failed)]
    [InlineData("C-2O3", EvaluationStatus.Failed)]
    [InlineData("B-1011", EvaluationStatus.Failed)]
    [InlineData("b-101", EvaluationStatus.Failed)]
    public void AnchoredPattern(string mark, EvaluationStatus expected)
    {
        var result = _rule.Evaluate(new[] { Element("a", mark: mark) }, Rule(RuleType.NamingPattern, pattern: @"^[BC]-\d{3}$"));

        Assert.Equal(expected, StatusOf(result, "a"));
        if (expected == EvaluationStatus.Failed)
        {
            var finding = Assert.Single(result.Findings);
            Assert.Equal(mark, finding.Observed);
            Assert.Equal(@"Matches ^[BC]-\d{3}$", finding.Expected);
        }
    }

    [Fact]
    public void UnanchoredPattern_MatchesAnywhere()
    {
        var result = _rule.Evaluate(new[] { Element("a", mark: "XB-1Y") }, Rule(RuleType.NamingPattern, pattern: "B-1"));

        Assert.Equal(EvaluationStatus.Passed, StatusOf(result, "a"));
    }

    [Fact]
    public void MissingOrBlank_IsNotEvaluated()
    {
        var result = _rule.Evaluate(new[] { Element("a"), Element("b", mark: "") }, Rule(RuleType.NamingPattern, pattern: "^B"));

        Assert.Equal(2, result.NotEvaluatedCount);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void CatastrophicBacktracking_TimesOut_AsNotEvaluated()
    {
        var value = new string('a', 40) + "!";
        var watch = Stopwatch.StartNew();

        var result = _rule.Evaluate(new[] { Element("a", mark: value) }, Rule(RuleType.NamingPattern, pattern: "^(a+)+$"));

        Assert.Equal(EvaluationStatus.NotEvaluated, StatusOf(result, "a"));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"took {watch.Elapsed}");
    }
}
