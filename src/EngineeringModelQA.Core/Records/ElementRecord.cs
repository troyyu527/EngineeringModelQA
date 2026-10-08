namespace EngineeringModelQA.Core.Records;

/// <summary>
/// Plain copy of one IFC element. Property keys are "PropertySet.PropertyName" (case-sensitive);
/// a property that exists without a value is stored as "" so missing and blank stay distinguishable.
/// </summary>
/// <param name="EntityType">Checked base type, e.g. IfcWall also for IfcWallStandardCase.</param>
/// <param name="Material">Material summary, or null when the element and its type have none.</param>
/// <param name="Storey">Name of the containing building storey, or null when not directly in a storey.</param>
/// <param name="SourceLabel">STEP entity label such as "#412" (provenance).</param>
public sealed record ElementRecord(
    string GlobalId,
    string EntityType,
    string? Name,
    IReadOnlyDictionary<string, string> Properties,
    string? Material,
    string? Storey,
    string SourceLabel)
{
    /// <summary>Property value, "" when blank, or null when the property does not exist.</summary>
    public string? GetProperty(string key) => Properties.TryGetValue(key, out var value) ? value : null;
}
