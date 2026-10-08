using EngineeringModelQA.Core.Records;

namespace EngineeringModelQA.Application.Contracts;

/// <summary>Result of loading a profile: a validated profile, or every error found (never both).</summary>
public sealed record ProfileLoadResult(RuleProfile? Profile, IReadOnlyList<string> Errors)
{
    public bool IsValid => Profile is not null;

    public static ProfileLoadResult Valid(RuleProfile profile) => new(profile, Array.Empty<string>());

    public static ProfileLoadResult Invalid(IReadOnlyList<string> errors) => new(null, errors);
}

/// <summary>Loads and validates rule profiles. Validation happens here, before any check can start.</summary>
public interface IProfileStore
{
    ProfileLoadResult Load(string path);
}
