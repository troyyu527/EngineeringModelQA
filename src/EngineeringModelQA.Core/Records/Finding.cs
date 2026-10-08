namespace EngineeringModelQA.Core.Records;

/// <summary>
/// One failed rule on one element. Identity across revisions is
/// <see cref="RuleId"/> + <see cref="ElementGlobalId"/> + <see cref="ConditionKey"/>; observed values and texts are
/// not part of it.
/// </summary>
public sealed record Finding(
    string RuleId,
    string RuleName,
    RuleType RuleType,
    string ElementGlobalId,
    string EntityType,
    string? ElementName,
    string ConditionKey,
    Severity Severity,
    string Expected,
    string Observed,
    string Explanation,
    string SuggestedAction);
