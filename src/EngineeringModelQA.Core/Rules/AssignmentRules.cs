using EngineeringModelQA.Core.Records;

namespace EngineeringModelQA.Core.Rules;

/// <summary>Fails when neither the element nor its type has a material (the reader resolves both).</summary>
public sealed class MaterialAssignedRule : IRule
{
    public RuleType Type => RuleType.MaterialAssigned;

    public RuleResult Evaluate(IReadOnlyList<ElementRecord> elements, RuleDefinition rule)
    {
        RuleScope.RequireType(rule, Type);
        var result = new RuleScope.Builder(rule);
        foreach (var element in elements)
        {
            if (!RuleScope.AppliesTo(element, rule))
                result.Skip(element);
            else if (!string.IsNullOrWhiteSpace(element.Material))
                result.Pass(element);
            else
                result.Fail(RuleScope.CreateFinding(rule, element, "material",
                    expected: "A material on the element or its type",
                    observed: "(none)",
                    explanation: "No material is associated with the element or its type.",
                    suggestedAction: "Assign a material in the source model and export the IFC again."));
        }

        return result.Build();
    }
}

/// <summary>Fails when the element is not directly contained in a building storey.</summary>
public sealed class StoreyAssignedRule : IRule
{
    public RuleType Type => RuleType.StoreyAssigned;

    public RuleResult Evaluate(IReadOnlyList<ElementRecord> elements, RuleDefinition rule)
    {
        RuleScope.RequireType(rule, Type);
        var result = new RuleScope.Builder(rule);
        foreach (var element in elements)
        {
            if (!RuleScope.AppliesTo(element, rule))
                result.Skip(element);
            else if (!string.IsNullOrWhiteSpace(element.Storey))
                result.Pass(element);
            else
                result.Fail(RuleScope.CreateFinding(rule, element, "storey",
                    expected: "Contained in a building storey",
                    observed: "(not in a storey)",
                    explanation: "The element is not directly contained in an IfcBuildingStorey.",
                    suggestedAction: "Place the element on a level in the source model and export the IFC again."));
        }

        return result.Build();
    }
}
