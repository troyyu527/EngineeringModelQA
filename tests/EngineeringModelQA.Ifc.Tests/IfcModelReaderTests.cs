using EngineeringModelQA.Application.Contracts;
using EngineeringModelQA.Core.Records;
using Xunit.Abstractions;

namespace EngineeringModelQA.Ifc.Tests;

public class IfcModelReaderTests(ITestOutputHelper output)
{
    private readonly IfcModelReader _reader = new();

    private Task<ModelSnapshot> Read(string path) => _reader.ReadAsync(path, null, CancellationToken.None);

    private static ElementRecord Get(ModelSnapshot snapshot, string globalId) => snapshot.Elements.Single(e => e.GlobalId == globalId);

    [Fact]
    public async Task Baseline_IsIfc4_WithElementCountsPerType()
    {
        var snapshot = await Read(Fixtures.Baseline);

        var counts = snapshot.Elements.GroupBy(e => e.EntityType).ToDictionary(g => g.Key, g => g.Count());
        output.WriteLine($"Schema {snapshot.Schema}: " + string.Join(", ", counts.Select(c => $"{c.Key} {c.Value}")));
        Assert.Equal("IFC4", snapshot.Schema);
        Assert.Equal(14, counts["IfcBeam"]);
        Assert.Equal(12, counts["IfcColumn"]);
        Assert.Equal(2, counts["IfcSlab"]);
        Assert.Equal(4, counts["IfcWall"]);
        Assert.Empty(snapshot.Warnings);
    }

    [Fact]
    public async Task Baseline_CopiesAttributesPropertiesMaterialAndStorey()
    {
        var snapshot = await Read(Fixtures.Baseline);
        var beam = Get(snapshot, Fixtures.Id.B101);

        Assert.Equal("IfcBeam", beam.EntityType);
        Assert.Equal("Steel Beam:W310x39", beam.Name);
        Assert.Equal("B-101", beam.GetProperty("ProjectInformation.MemberMark"));
        Assert.Equal("TRUE", beam.GetProperty("Pset_BeamCommon.LoadBearing"));
        Assert.Equal("W310x39", beam.GetProperty("Pset_BeamCommon.Reference")); // from the beam type
        Assert.Equal("Steel S355", beam.Material);                               // profile set usage
        Assert.Equal("Level 1", beam.Storey);
        Assert.StartsWith("#", beam.SourceLabel);

        Assert.Equal("Concrete C30/37", Get(snapshot, Fixtures.Id.C101).Material); // direct IfcMaterial
        Assert.Equal("Concrete C30/37", Get(snapshot, Fixtures.Id.S1).Material);   // layer set usage
        Assert.Equal("Concrete C30/37", Get(snapshot, Fixtures.Id.W3).Material);   // only on the wall type
        Assert.Equal("IfcWall", Get(snapshot, Fixtures.Id.W2).EntityType);         // IfcWallStandardCase
        Assert.Equal("FALSE", Get(snapshot, Fixtures.Id.W4).GetProperty("Pset_WallCommon.LoadBearing"));
    }

    [Fact]
    public async Task Baseline_ContainsTheSeededDefects()
    {
        var snapshot = await Read(Fixtures.Baseline);

        Assert.Null(Get(snapshot, Fixtures.Id.B103).GetProperty("ProjectInformation.MemberMark"));
        Assert.Equal("", Get(snapshot, Fixtures.Id.C104).GetProperty("ProjectInformation.MemberMark"));
        Assert.Equal("B-105", Get(snapshot, Fixtures.Id.B105).GetProperty("ProjectInformation.MemberMark"));
        Assert.Equal("B-105", Get(snapshot, Fixtures.Id.B106).GetProperty("ProjectInformation.MemberMark"));
        Assert.Equal("C-2O3", Get(snapshot, Fixtures.Id.C203).GetProperty("ProjectInformation.MemberMark"));
        Assert.Equal("B204", Get(snapshot, Fixtures.Id.B204).GetProperty("ProjectInformation.MemberMark"));
        Assert.Null(Get(snapshot, Fixtures.Id.B207).Material);
        Assert.Null(Get(snapshot, Fixtures.Id.C206).Storey);
        Assert.Null(Get(snapshot, Fixtures.Id.W4).Material);
    }

    [Fact]
    public async Task Revision_SharesGlobalIds_WithOneRemovedAndOneAddedBeam()
    {
        var baseline = (await Read(Fixtures.Baseline)).Elements.Select(e => e.GlobalId).ToList();
        var revision = (await Read(Fixtures.Revision)).Elements.Select(e => e.GlobalId).ToList();

        Assert.Equal(new[] { Fixtures.Id.B207 }, baseline.Except(revision));
        Assert.Equal(new[] { Fixtures.Id.B208 }, revision.Except(baseline));
        Assert.Equal(31, baseline.Intersect(revision).Count());
    }

    [Fact]
    public async Task Warnings_ListOutOfScopeElementsAndSkippedProperties()
    {
        var snapshot = await Read(Fixtures.Ifc("warnings.ifc"));

        Assert.Single(snapshot.Elements);
        Assert.Contains(snapshot.Warnings, w => w.Contains("1 IfcDoor element(s)"));
        Assert.Contains(snapshot.Warnings, w => w.Contains("IfcPropertyEnumeratedValue"));
    }

    [Fact]
    public async Task EmptyModel_ReturnsNoElementsAndAWarning()
    {
        var snapshot = await Read(Fixtures.Ifc("empty-model.ifc"));

        Assert.Empty(snapshot.Elements);
        Assert.Contains(snapshot.Warnings, w => w.Contains("contains no IfcBeam"));
    }

    [Theory]
    [InlineData("invalid/ifc2x3.ifc", "IFC4 files only")]
    [InlineData("invalid/not-ifc.ifc", "not a readable IFC file")]
    [InlineData("invalid/empty-file.ifc", "is empty")]
    [InlineData("invalid/does-not-exist.ifc", "was not found")]
    public async Task BadInput_ThrowsReadableMessage(string file, string expected)
    {
        var ex = await Assert.ThrowsAsync<ModelReadException>(() => Read(Fixtures.Ifc(file)));

        output.WriteLine(ex.Message);
        Assert.Contains(expected, ex.Message);
        Assert.Contains(Path.GetFileName(file), ex.Message);
    }

    [Fact]
    public async Task Read_ReleasesFileLock()
    {
        await Read(Fixtures.Baseline);

        using var exclusive = new FileStream(Fixtures.Baseline, FileMode.Open, FileAccess.Read, FileShare.None);
        Assert.True(exclusive.CanRead);
    }

    [Fact]
    public async Task Read_ReportsProgressAndStableFingerprint()
    {
        var reports = new List<ImportProgress>();
        var first = await _reader.ReadAsync(Fixtures.Baseline, new SyncProgress<ImportProgress>(reports.Add), CancellationToken.None);
        var second = await Read(Fixtures.Baseline);

        Assert.Contains(reports, r => r.Stage == "Opening file");
        Assert.Equal(32, reports.Last().ElementsRead);
        Assert.Matches("^[0-9a-f]{64}$", first.SourceFingerprint);
        Assert.Equal(first.SourceFingerprint, second.SourceFingerprint);
        Assert.Equal(first.Elements, second.Elements, new ElementComparer());
    }

    [Fact]
    public async Task Read_WithCanceledToken_Throws()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _reader.ReadAsync(Fixtures.Baseline, null, cts.Token));
    }

    private sealed class SyncProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class ElementComparer : IEqualityComparer<ElementRecord>
    {
        public bool Equals(ElementRecord? x, ElementRecord? y) =>
            x!.GlobalId == y!.GlobalId && x.Name == y.Name && x.Material == y.Material && x.Storey == y.Storey &&
            x.Properties.OrderBy(p => p.Key).SequenceEqual(y.Properties.OrderBy(p => p.Key));

        public int GetHashCode(ElementRecord obj) => obj.GlobalId.GetHashCode();
    }
}
