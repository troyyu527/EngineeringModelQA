namespace EngineeringModelQA.Core.Records;

/// <summary>Excludes an element from a rule when a property has a given value (case-insensitive).</summary>
/// <param name="Property">Qualified key "PropertySet.PropertyName".</param>
public sealed record PropertyExclusion(string Property, string EqualsValue);

/// <summary>One validated rule from a profile.</summary>
public sealed record RuleDefinition(
    string Id,
    string Name,
    RuleType Type,
    Severity Severity,
    IReadOnlyList<string> EntityTypes)
{
    public bool Enabled { get; init; } = true;

    public string? PropertySet { get; init; }

    public string? PropertyName { get; init; }

    /// <summary>Regular expression for <see cref="RuleType.NamingPattern"/>.</summary>
    public string? Pattern { get; init; }

    public UniqueScope UniqueScope { get; init; } = UniqueScope.Project;

    public IReadOnlyList<PropertyExclusion> Exclusions { get; init; } = Array.Empty<PropertyExclusion>();

    /// <summary>"PropertySet.PropertyName", or null for rules that do not read a property.</summary>
    public string? PropertyKey => PropertySet is null || PropertyName is null ? null : PropertySet + "." + PropertyName;
}
