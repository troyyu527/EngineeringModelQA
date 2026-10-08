namespace EngineeringModelQA.Core.Records;

/// <summary>A completed check of one model against one profile.</summary>
/// <param name="ElementLabels">Display label per GlobalId: the profile's mark, or "Beam #412 (no mark)".</param>
public sealed record ValidationRun(
    Guid RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string SourcePath,
    string SourceFingerprint,
    string Schema,
    string ProfileId,
    string ProfileName,
    string ProfileVersion,
    string ProfileFingerprint,
    int ElementCount,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<RuleResult> RuleResults,
    IReadOnlyDictionary<string, string> ElementLabels)
{
    /// <summary>All findings, rule by rule in profile order, elements in model order.</summary>
    public IReadOnlyList<Finding> Findings => RuleResults.SelectMany(r => r.Findings).ToList();

    public int FailedCount => RuleResults.Sum(r => r.FailedCount);

    public int PassedCount => RuleResults.Sum(r => r.PassedCount);

    public int NotEvaluatedCount => RuleResults.Sum(r => r.NotEvaluatedCount);

    public string LabelFor(string globalId) => ElementLabels.TryGetValue(globalId, out var label) ? label : globalId;
}
