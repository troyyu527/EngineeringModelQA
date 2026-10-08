namespace EngineeringModelQA.Core.Records;

/// <summary>How serious a failed rule is. Order matters: most severe first.</summary>
public enum Severity
{
    Error,
    Warning,
    Info,
}

/// <summary>Result of one rule on one element.</summary>
public enum EvaluationStatus
{
    Passed,
    Failed,
    /// <summary>The rule does not apply or cannot be judged (out of scope, excluded, no data to compare).</summary>
    NotEvaluated,
}

/// <summary>The five rule types a profile can use.</summary>
public enum RuleType
{
    RequiredProperty,
    UniqueValue,
    NamingPattern,
    MaterialAssigned,
    StoreyAssigned,
}

/// <summary>Where a value must be unique.</summary>
public enum UniqueScope
{
    Project,
    Storey,
}
