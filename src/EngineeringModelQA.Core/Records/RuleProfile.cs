namespace EngineeringModelQA.Core.Records;

/// <summary>A validated rule profile. Only a profile store creates these, after validation.</summary>
/// <param name="ScopeDescription">Human-readable scope, e.g. "Structural beams and columns".</param>
/// <param name="EntityTypes">Profile-level default entity types.</param>
/// <param name="PropertySet">Profile-level property mapping (rules may override).</param>
/// <param name="Fingerprint">SHA-256 of the validated content in a fixed form (lowercase hex).</param>
public sealed record RuleProfile(
    string ProfileId,
    string Name,
    string Version,
    string ScopeDescription,
    IReadOnlyList<string> EntityTypes,
    string? PropertySet,
    string? PropertyName,
    IReadOnlyList<RuleDefinition> Rules,
    string Fingerprint)
{
    /// <summary>Profile-level mapping key, or null when the profile has no default property.</summary>
    public string? PropertyKey => PropertySet is null || PropertyName is null ? null : PropertySet + "." + PropertyName;
}
