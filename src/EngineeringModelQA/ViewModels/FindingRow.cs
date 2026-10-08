using EngineeringModelQA.Core.Records;

namespace EngineeringModelQA.ViewModels;

/// <summary>One row of the findings table and the source of the details pane.</summary>
public sealed class FindingRow
{
    private readonly string _searchText;

    public FindingRow(Finding finding, string elementLabel)
    {
        Finding = finding;
        Element = elementLabel;
        _searchText = string.Join("\n", elementLabel, finding.ElementGlobalId, finding.RuleName, finding.RuleId,
            finding.Observed, finding.EntityType, finding.ElementName ?? "");
    }

    public Finding Finding { get; }

    public Severity Severity => Finding.Severity;

    public string SeverityText => Finding.Severity.ToString();

    /// <summary>Mark, or e.g. "Beam #412 (no mark)".</summary>
    public string Element { get; }

    public string EntityType => Finding.EntityType;

    public string GlobalId => Finding.ElementGlobalId;

    public string Rule => Finding.RuleName;

    public string Expected => Finding.Expected;

    public string Observed => Finding.Observed;

    public string Explanation => Finding.Explanation;

    public string Guidance => Finding.SuggestedAction;

    public bool Matches(string search) => _searchText.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
}
