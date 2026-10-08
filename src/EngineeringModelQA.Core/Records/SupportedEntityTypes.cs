namespace EngineeringModelQA.Core.Records;

/// <summary>IFC entity types the reader copies and profiles may target.</summary>
public static class SupportedEntityTypes
{
    public const string Beam = "IfcBeam";
    public const string Column = "IfcColumn";
    public const string Slab = "IfcSlab";
    public const string Wall = "IfcWall";

    public static IReadOnlyList<string> All { get; } = new[] { Beam, Column, Slab, Wall };

    public static bool IsSupported(string entityType) => All.Contains(entityType, StringComparer.Ordinal);
}
