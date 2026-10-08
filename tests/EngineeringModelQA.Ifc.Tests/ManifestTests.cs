using System.Text.Json;
using EngineeringModelQA.Application;
using EngineeringModelQA.Infrastructure;
using Xunit.Abstractions;

namespace EngineeringModelQA.Ifc.Tests;

/// <summary>
/// End to end on real files: IFC reader + JSON profile + rules must reproduce samples/expected/manifest.json exactly.
/// </summary>
public class ManifestTests(ITestOutputHelper output)
{
    public static IEnumerable<object[]> Cases()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures.Root, "expected", "manifest.json")));
        return manifest.RootElement.GetProperty("cases").EnumerateArray()
            .Select(c => new object[] { c.GetProperty("model").GetString()!, c.GetProperty("profile").GetString()! })
            .ToList();
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Findings_AndCounts_MatchManifest(string model, string profile)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(Fixtures.Root, "expected", "manifest.json")));
        var expected = manifest.RootElement.GetProperty("cases").EnumerateArray()
            .Single(c => c.GetProperty("model").GetString() == model && c.GetProperty("profile").GetString() == profile);

        var loaded = new JsonProfileStore().Load(Path.Combine(Fixtures.Root, profile));
        Assert.True(loaded.IsValid, string.Join("; ", loaded.Errors));
        var run = await new ValidationService(new IfcModelReader())
            .RunAsync(Path.Combine(Fixtures.Root, model), loaded.Profile!, null, CancellationToken.None);

        Assert.Equal(expected.GetProperty("elementCount").GetInt32(), run.ElementCount);

        var expectedCounts = expected.GetProperty("rules").EnumerateObject()
            .Select(r => $"{r.Name} {r.Value.GetProperty("passed").GetInt32()}/{r.Value.GetProperty("failed").GetInt32()}/{r.Value.GetProperty("notEvaluated").GetInt32()}");
        var actualCounts = run.RuleResults.Select(r => $"{r.Rule.Id} {r.PassedCount}/{r.FailedCount}/{r.NotEvaluatedCount}");
        Assert.Equal(expectedCounts, actualCounts);

        var expectedFindings = expected.GetProperty("findings").EnumerateArray()
            .Select(f => $"{f.GetProperty("ruleId").GetString()} {f.GetProperty("globalId").GetString()} {f.GetProperty("observed").GetString()}")
            .OrderBy(s => s, StringComparer.Ordinal);
        var actualFindings = run.Findings.Select(f => $"{f.RuleId} {f.ElementGlobalId} {f.Observed}").OrderBy(s => s, StringComparer.Ordinal);
        foreach (var finding in run.Findings)
            output.WriteLine($"{finding.RuleId} {run.LabelFor(finding.ElementGlobalId),-24} {finding.Observed}");
        Assert.Equal(expectedFindings, actualFindings);
    }
}
