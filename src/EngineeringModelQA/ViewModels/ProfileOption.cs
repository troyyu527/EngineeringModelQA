using EngineeringModelQA.Application.Contracts;

namespace EngineeringModelQA.ViewModels;

/// <summary>An entry of the Check profile list: a profile file and the result of validating it.</summary>
public sealed class ProfileOption(string path, ProfileLoadResult result)
{
    public string Path { get; } = path;

    public ProfileLoadResult Result { get; } = result;

    public string FileName => System.IO.Path.GetFileName(Path);

    public string DisplayName => Result.Profile is { } profile ? profile.Name : $"{FileName} (invalid)";

    public override string ToString() => DisplayName;
}
