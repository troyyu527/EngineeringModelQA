using EngineeringModelQA.Core.Records;
using EngineeringModelQA.Infrastructure;

namespace EngineeringModelQA.Tests;

public class JsonProfileStoreTests
{
    private readonly JsonProfileStore _store = new();

    private static string SamplePath(string name) =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples", "profiles", name);

    /// <summary>A valid profile with one rule; <paramref name="rule"/> replaces the rule body.</summary>
    private static string ProfileWithRule(string rule, string mapping = "\"propertySet\": \"ProjectInformation\", \"propertyName\": \"MemberMark\",") =>
        "{ \"profileId\": \"p\", \"name\": \"P\", \"version\": \"1\", " + mapping + " \"rules\": [ " + rule + " ] }";

    [Fact]
    public void MemberInformation_Loads()
    {
        var result = _store.Load(SamplePath("member-information.json"));

        Assert.True(result.IsValid, string.Join("; ", result.Errors));
        var profile = result.Profile!;
        Assert.Equal(("member-information", "Member Information", "1.0"), (profile.ProfileId, profile.Name, profile.Version));
        Assert.Equal("Structural beams and columns", profile.ScopeDescription);
        Assert.Equal("ProjectInformation.MemberMark", profile.PropertyKey);
        Assert.Equal(new[] { "Required mark", "Unique mark", "Naming pattern" }, profile.Rules.Select(r => r.Name));
        Assert.Equal(new[] { Severity.Error, Severity.Error, Severity.Warning }, profile.Rules.Select(r => r.Severity));
        Assert.All(profile.Rules, r => Assert.Equal("ProjectInformation.MemberMark", r.PropertyKey));
        Assert.Equal(@"^[BC]-\d{3}$", profile.Rules[2].Pattern);
        Assert.Matches("^[0-9a-f]{64}$", profile.Fingerprint);
    }

    [Fact]
    public void StructuralFull_LoadsAllFiveRuleTypes()
    {
        var profile = _store.Load(SamplePath("structural-full.json")).Profile!;

        Assert.Equal(6, profile.Rules.Count);
        Assert.Equal(5, profile.Rules.Select(r => r.Type).Distinct().Count());
        Assert.False(profile.Rules.Single(r => r.Id == "STR-006").Enabled);
        Assert.Equal(UniqueScope.Storey, profile.Rules.Single(r => r.Id == "STR-006").UniqueScope);
        var material = profile.Rules.Single(r => r.Id == "STR-004");
        Assert.Null(material.PropertyKey);
        Assert.Equal(4, material.EntityTypes.Count);
        Assert.Equal(new PropertyExclusion("Pset_WallCommon.LoadBearing", "FALSE"), Assert.Single(material.Exclusions));
    }

    [Fact]
    public void Fingerprint_IgnoresFormatting_ButTracksContent()
    {
        var original = File.ReadAllText(SamplePath("member-information.json"));
        var reformatted = "/* other comment */\n" + original.Replace("  ", "\t").Replace(": ", ":").Replace("\n", "\n\n");
        var changed = original.Replace("\"severity\": \"warning\"", "\"severity\": \"info\"");

        var a = _store.Parse(original).Profile!.Fingerprint;
        var b = _store.Parse(reformatted).Profile!.Fingerprint;
        var c = _store.Parse(changed).Profile!.Fingerprint;

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void MissingFile_IsReported()
    {
        var result = _store.Load(SamplePath("nope.json"));

        Assert.False(result.IsValid);
        Assert.Contains("'nope.json' was not found", Assert.Single(result.Errors));
    }

    [Fact]
    public void BrokenJson_ReportsLine()
    {
        var result = _store.Parse("{\n  \"profileId\": \"p\"\n  \"name\": \"x\"\n}");

        Assert.Null(result.Profile);
        Assert.Contains("line 3", Assert.Single(result.Errors));
    }

    [Theory]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"foo\", \"severity\": \"error\" }", "type 'foo' is not one of")]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"requiredProperty\", \"severity\": \"critical\" }", "severity 'critical' is not one of")]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"requiredProperty\", \"severity\": \"error\", \"entityTypes\": [\"IfcDoor\"] }", "entity type 'IfcDoor' is not supported")]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"namingPattern\", \"severity\": \"error\" }", "need a 'pattern'")]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"namingPattern\", \"severity\": \"error\", \"pattern\": \"[B-\" }", "not a valid regular expression")]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"requiredProperty\", \"severity\": \"error\", \"pattern\": \"^B\" }", "only used by namingPattern")]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"uniqueValue\", \"severity\": \"error\", \"uniqueScope\": \"building\" }", "uniqueScope 'building' is not one of")]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"requiredProperty\", \"severity\": \"error\", \"uniqueScope\": \"storey\" }", "only used by uniqueValue")]
    [InlineData("{ \"id\": \"R 1\", \"name\": \"n\", \"type\": \"requiredProperty\", \"severity\": \"error\" }", "id may only contain")]
    [InlineData("{ \"id\": \"R1\", \"type\": \"requiredProperty\", \"severity\": \"error\" }", "'name' is missing")]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"requiredProperty\", \"severity\": \"error\", \"enabled\": \"yes\" }", "true or false")]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"materialAssigned\", \"severity\": \"error\", \"excludeWhen\": [{ \"property\": \"LoadBearing\", \"equals\": \"FALSE\" }] }", "PropertySet.PropertyName")]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"materialAssigned\", \"severity\": \"error\", \"propertySet\": \"P\" }", "not used by materialAssigned")]
    [InlineData("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"requiredProperty\", \"severity\": \"error\", \"colour\": \"red\" }", "unknown field 'colour'")]
    public void InvalidRule_IsRejectedWithReason(string rule, string expected)
    {
        var result = _store.Parse(ProfileWithRule(rule));

        Assert.Null(result.Profile);
        Assert.Contains(result.Errors, e => e.Contains(expected));
    }

    [Fact]
    public void PropertyRule_WithoutMapping_IsRejected()
    {
        var result = _store.Parse(ProfileWithRule("{ \"id\": \"R1\", \"name\": \"n\", \"type\": \"requiredProperty\", \"severity\": \"error\" }", mapping: ""));

        Assert.Contains("Rule 'R1': needs propertySet and propertyName", Assert.Single(result.Errors));
    }

    [Fact]
    public void ProfileLevelProblems_AreAllReportedAtOnce()
    {
        var json = "{ \"profileId\": \"bad id\", \"version\": \"1\", \"propertySet\": \"P\", \"extra\": 1, \"rules\": [ " +
                   "{ \"id\": \"R1\", \"name\": \"a\", \"type\": \"materialAssigned\", \"severity\": \"error\" }, " +
                   "{ \"id\": \"r1\", \"name\": \"b\", \"type\": \"storeyAssigned\", \"severity\": \"info\" } ] }";

        var errors = _store.Parse(json).Errors;

        Assert.Contains(errors, e => e.Contains("profileId 'bad id'"));
        Assert.Contains(errors, e => e.Contains("'name' is missing"));
        Assert.Contains(errors, e => e.Contains("set propertySet and propertyName together"));
        Assert.Contains(errors, e => e.Contains("unknown field 'extra'"));
        Assert.Contains(errors, e => e.Contains("Rule id 'R1' is used 2 times"));
    }

    [Theory]
    [InlineData("{ \"profileId\": \"p\", \"name\": \"P\", \"version\": \"1\", \"rules\": [] }")]
    [InlineData("{ \"profileId\": \"p\", \"name\": \"P\", \"version\": \"1\" }")]
    [InlineData("[1, 2]")]
    public void EmptyOrMissingRules_AreRejected(string json) => Assert.False(_store.Parse(json).IsValid);
}
