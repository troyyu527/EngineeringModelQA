using CommunityToolkit.Mvvm.ComponentModel;

namespace EngineeringModelQA.ViewModels;

/// <summary>Drives the status bar icon.</summary>
public enum StatusKind
{
    Ready,
    Busy,
    Complete,
    Canceled,
    Failed,
}

/// <summary>A page in the navigation rail. Its status text is shown in the window's status bar.</summary>
public abstract partial class PageViewModel(string title, string iconGlyph) : ObservableObject
{
    public string Title { get; } = title;

    /// <summary>Segoe Fluent Icons / MDL2 glyph for the rail.</summary>
    public string IconGlyph { get; } = iconGlyph;

    /// <summary>Also the UI Automation name of the rail item.</summary>
    public override string ToString() => Title;

    [ObservableProperty]
    private string _statusText = "Local processing";

    [ObservableProperty]
    private StatusKind _statusKind = StatusKind.Ready;
}

/// <summary>Pages that later steps fill in.</summary>
public sealed class PlaceholderViewModel(string title, string iconGlyph, string message) : PageViewModel(title, iconGlyph)
{
    public string Message { get; } = message;
}
