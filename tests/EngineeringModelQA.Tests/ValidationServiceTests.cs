using EngineeringModelQA.Application;
using EngineeringModelQA.Application.Contracts;
using EngineeringModelQA.Core.Records;
using static EngineeringModelQA.Tests.TestData;

namespace EngineeringModelQA.Tests;

public class ValidationServiceTests
{
    private static readonly ModelSnapshot Snapshot = new("model.ifc", "abc", "IFC4", new[]
    {
        Element("a", mark: "B-101"),
        Element("b", mark: "B-101"),
        Element("c"),
        Element("s", SupportedEntityTypes.Slab),
    }, new[] { "1 IfcDoor element(s) are outside the checked types and were not read." });

    private static RuleProfile Profile(params RuleDefinition[] rules) =>
        new("p", "Profile", "1.0", "Beams", new[] { "IfcBeam" }, "ProjectInformation", "MemberMark", rules, "fp");

    [Fact]
    public async Task Run_EvaluatesEnabledRules_AndKeepsRunMetadata()
    {
        var profile = Profile(Rule(RuleType.RequiredProperty, "R-1"), Rule(RuleType.UniqueValue, "R-2"),
            Rule(RuleType.MaterialAssigned, "R-3") with { Enabled = false });

        var run = await new ValidationService(new FakeReader(Snapshot)).RunAsync("model.ifc", profile, null, CancellationToken.None);

        Assert.Equal(new[] { "R-1", "R-2" }, run.RuleResults.Select(r => r.Rule.Id));
        Assert.Equal(new[] { "c", "a", "b" }, run.Findings.Select(f => f.ElementGlobalId));
        Assert.Equal(3, run.FailedCount);
        Assert.Equal(4, run.ElementCount);
        Assert.Equal(("abc", "IFC4", "fp", "1.0"), (run.SourceFingerprint, run.Schema, run.ProfileFingerprint, run.ProfileVersion));
        Assert.Single(run.Warnings);
    }

    [Fact]
    public async Task Run_LabelsElementsByMark_OrByTypeAndStepLabel()
    {
        var run = await new ValidationService(new FakeReader(Snapshot))
            .RunAsync("model.ifc", Profile(Rule(RuleType.RequiredProperty)), null, CancellationToken.None);

        Assert.Equal("B-101", run.LabelFor("a"));
        Assert.Matches(@"^Beam #\d+ \(no mark\)$", run.LabelFor("c"));
        Assert.Matches(@"^Slab #\d+ \(no mark\)$", run.LabelFor("s"));
    }

    [Fact]
    public async Task Run_IsRepeatable()
    {
        var service = new ValidationService(new FakeReader(Snapshot));
        var profile = Profile(Rule(RuleType.RequiredProperty), Rule(RuleType.UniqueValue, "R-2"));

        var first = await service.RunAsync("model.ifc", profile, null, CancellationToken.None);
        var second = await service.RunAsync("model.ifc", profile, null, CancellationToken.None);

        Assert.Equal(first.Findings, second.Findings);
        Assert.NotEqual(first.RunId, second.RunId);
    }

    [Fact]
    public async Task Run_ReportsImportAndValidationProgress()
    {
        var reports = new List<CheckProgress>();
        await new ValidationService(new FakeReader(Snapshot))
            .RunAsync("model.ifc", Profile(Rule(RuleType.RequiredProperty)), new Sync(reports.Add), CancellationToken.None);

        Assert.Equal(CheckStage.Importing, reports.First().Stage);
        Assert.Contains(reports, r => r.Stage == CheckStage.Importing && r.Text == "Reading elements");
        Assert.Equal(CheckStage.Validating, reports.Last().Stage);
    }

    [Fact]
    public async Task Run_Canceled_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new ValidationService(new FakeReader(Snapshot)).RunAsync("model.ifc", Profile(Rule(RuleType.RequiredProperty)), null, cts.Token));
    }

    [Fact]
    public async Task Run_ReaderError_IsPassedOn()
    {
        var ex = await Assert.ThrowsAsync<ModelReadException>(() =>
            new ValidationService(new FakeReader(null)).RunAsync("x.ifc", Profile(Rule(RuleType.RequiredProperty)), null, CancellationToken.None));

        Assert.Equal("broken", ex.Message);
    }

    private sealed class FakeReader(ModelSnapshot? snapshot) : IModelReader
    {
        public Task<ModelSnapshot> ReadAsync(string path, IProgress<ImportProgress>? progress, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (snapshot is null)
                throw new ModelReadException("broken");
            progress?.Report(new ImportProgress("Reading elements", snapshot.Elements.Count));
            return Task.FromResult(snapshot);
        }
    }

    private sealed class Sync(Action<CheckProgress> report) : IProgress<CheckProgress>
    {
        public void Report(CheckProgress value) => report(value);
    }
}
