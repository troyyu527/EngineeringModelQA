using EngineeringModelQA.Core.Records;

namespace EngineeringModelQA.Core.Rules;

/// <summary>Evaluates one rule type on a whole model (uniqueness needs every element at once).</summary>
public interface IRule
{
    RuleType Type { get; }

    /// <returns>A status for every element, in model order, and a finding for every failure.</returns>
    RuleResult Evaluate(IReadOnlyList<ElementRecord> elements, RuleDefinition rule);
}
