using EngineeringModelQA.Core.Records;
using EngineeringModelQA.Core.Rules;
using static EngineeringModelQA.Tests.TestData;

namespace EngineeringModelQA.Tests.Rules;

public class AssignmentRuleTests
{
    private static readonly string[] AllTypes = SupportedEntityTypes.All.ToArray();

    [Theory]
    [InlineData("Steel S355", EvaluationStatus.Passed)]
    [InlineData(null, EvaluationStatus.Failed)]
    [InlineData(" ", EvaluationStatus.Failed)]
    public void Material(string? material, EvaluationStatus expected)
    {
        var result = new MaterialAssignedRule().Evaluate(new[] { Element("a", material: material) },
            Rule(RuleType.MaterialAssigned, entityTypes: AllTypes));

        Assert.Equal(expected, StatusOf(result, "a"));
        if (expected == EvaluationStatus.Failed)
            Assert.Equal("(none)", Assert.Single(result.Findings).Observed);
    }

    [Theory]
    [InlineData("Level 1", EvaluationStatus.Passed)]
    [InlineData(null, EvaluationStatus.Failed)]
    public void Storey(string? storey, EvaluationStatus expected)
    {
        var result = new StoreyAssignedRule().Evaluate(new[] { Element("a", storey: storey) },
            Rule(RuleType.StoreyAssigned, entityTypes: AllTypes));

        Assert.Equal(expected, StatusOf(result, "a"));
        if (expected == EvaluationStatus.Failed)
            Assert.Equal("storey", Assert.Single(result.Findings).ConditionKey);
    }

    [Fact]
    public void Exclusion_IsCaseInsensitive_AndNeedsTheProperty()
    {
        var definition = Rule(RuleType.MaterialAssigned, entityTypes: AllTypes,
            exclusions: new PropertyExclusion("Pset_WallCommon.LoadBearing", "FALSE"));
        var elements = new[]
        {
            Element("excluded", SupportedEntityTypes.Wall, material: null, properties: ("Pset_WallCommon.LoadBearing", "false")),
            Element("bearing", SupportedEntityTypes.Wall, material: null, properties: ("Pset_WallCommon.LoadBearing", "TRUE")),
            Element("unknown", SupportedEntityTypes.Wall, material: null),
        };

        var result = new MaterialAssignedRule().Evaluate(elements, definition);

        Assert.Equal(EvaluationStatus.NotEvaluated, StatusOf(result, "excluded"));
        Assert.Equal(EvaluationStatus.Failed, StatusOf(result, "bearing"));
        Assert.Equal(EvaluationStatus.Failed, StatusOf(result, "unknown"));
        Assert.Equal(2, result.FailedCount);
    }

    [Fact]
    public void OutOfScope_IsNotEvaluated()
    {
        var result = new StoreyAssignedRule().Evaluate(new[] { Element("s", SupportedEntityTypes.Slab, storey: null) },
            Rule(RuleType.StoreyAssigned));

        Assert.Equal(EvaluationStatus.NotEvaluated, StatusOf(result, "s"));
    }

    [Theory]
    [InlineData(RuleType.RequiredProperty)]
    [InlineData(RuleType.UniqueValue)]
    [InlineData(RuleType.NamingPattern)]
    [InlineData(RuleType.MaterialAssigned)]
    [InlineData(RuleType.StoreyAssigned)]
    public void Engine_DispatchesEveryRuleType(RuleType type)
    {
        var result = new RuleEngine().Evaluate(new[] { Element("a", mark: "B-101") }, Rule(type, pattern: "^B"));

        Assert.Equal(type, result.Rule.Type);
        Assert.Equal(EvaluationStatus.Passed, StatusOf(result, "a"));
    }
}
