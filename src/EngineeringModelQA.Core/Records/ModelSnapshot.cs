namespace EngineeringModelQA.Core.Records;

/// <summary>Everything the app keeps from one IFC file. No live IFC objects.</summary>
/// <param name="SourceFingerprint">SHA-256 of the file bytes (lowercase hex).</param>
/// <param name="Warnings">Import warnings: data that was not read or could not be mapped.</param>
public sealed record ModelSnapshot(
    string SourcePath,
    string SourceFingerprint,
    string Schema,
    IReadOnlyList<ElementRecord> Elements,
    IReadOnlyList<string> Warnings);
