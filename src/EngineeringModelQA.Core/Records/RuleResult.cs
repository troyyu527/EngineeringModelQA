namespace EngineeringModelQA.Core.Records;

/// <summary>Outcome of one rule on a whole model.</summary>
/// <param name="Statuses">Status for every element in the model, in model order, keyed by GlobalId.</param>
public sealed record RuleResult(
    RuleDefinition Rule,
    IReadOnlyList<KeyValuePair<string, EvaluationStatus>> Statuses,
    IReadOnlyList<Finding> Findings)
{
    public int PassedCount => Statuses.Count(s => s.Value == EvaluationStatus.Passed);

    public int FailedCount => Statuses.Count(s => s.Value == EvaluationStatus.Failed);

    public int NotEvaluatedCount => Statuses.Count(s => s.Value == EvaluationStatus.NotEvaluated);
}
