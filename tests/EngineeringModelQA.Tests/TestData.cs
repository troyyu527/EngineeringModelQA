using EngineeringModelQA.Core.Records;

namespace EngineeringModelQA.Tests;

/// <summary>In-memory records for unit tests. These are not proof of IFC integration (see Ifc.Tests).</summary>
internal static class TestData
{
    public const string MarkKey = "ProjectInformation.MemberMark";

    private static int _label = 100;

    /// <param name="mark">null = no mark property, "" = blank.</param>
    public static ElementRecord Element(string globalId, string type = SupportedEntityTypes.Beam, string? mark = null,
        string? material = "Steel S355", string? storey = "Level 1", params (string Key, string Value)[] properties)
    {
        var dict = properties.ToDictionary(p => p.Key, p => p.Value);
        if (mark is not null)
            dict[MarkKey] = mark;
        return new ElementRecord(globalId, type, "Steel Beam:W310x39", dict, material, storey, "#" + ++_label);
    }

    public static RuleDefinition Rule(RuleType type, string id = "R-1", Severity severity = Severity.Error,
        string[]? entityTypes = null, string? pattern = null, UniqueScope scope = UniqueScope.Project,
        params PropertyExclusion[] exclusions)
    {
        var usesProperty = type is RuleType.RequiredProperty or RuleType.UniqueValue or RuleType.NamingPattern;
        return new RuleDefinition(id, id + " name", type, severity,
            entityTypes ?? new[] { SupportedEntityTypes.Beam, SupportedEntityTypes.Column })
        {
            PropertySet = usesProperty ? "ProjectInformation" : null,
            PropertyName = usesProperty ? "MemberMark" : null,
            Pattern = pattern,
            UniqueScope = scope,
            Exclusions = exclusions,
        };
    }

    public static EvaluationStatus StatusOf(RuleResult result, string globalId) =>
        result.Statuses.Single(s => s.Key == globalId).Value;
}
