using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EngineeringModelQA.Application;
using EngineeringModelQA.Application.Contracts;
using EngineeringModelQA.Core.Records;
using EngineeringModelQA.Services;

namespace EngineeringModelQA.ViewModels;

public enum MessageKind
{
    None,
    Info,
    Warning,
    Error,
}

/// <summary>
/// Model Check page (mockup img1): pick a model and a profile, run the check off the UI thread with progress and
/// cancel, then show summary cards, a filterable findings table and the details of the selected finding.
/// The last completed run stays on screen when a later run fails or is canceled.
/// </summary>
public sealed partial class ModelCheckViewModel : PageViewModel
{
    public const string AllSeverities = "All severities";
    public const string AllRules = "All rules";
    public const string AllTypes = "All types";

    private readonly ValidationService _validation;
    private readonly IProfileStore _profileStore;
    private readonly IDialogService _dialogs;
    private readonly IClipboardService _clipboard;
    private readonly Action _openReports;
    private List<FindingRow> _allRows = new();
    private CancellationTokenSource? _cancellation;

    public ModelCheckViewModel(ValidationService validation, IProfileStore profileStore, string profilesFolder,
        IDialogService dialogs, IClipboardService clipboard, Action openReports)
        : base("Model Check", "")
    {
        _validation = validation;
        _profileStore = profileStore;
        _dialogs = dialogs;
        _clipboard = clipboard;
        _openReports = openReports;
        LoadProfiles(profilesFolder);
        StatusText = "Local processing • Ready";
    }

    public ObservableCollection<ProfileOption> Profiles { get; } = new();

    public IReadOnlyList<string> SeverityOptions { get; } = new[] { AllSeverities, "Error", "Warning", "Info" };

    public ObservableCollection<string> RuleOptions { get; } = new() { AllRules };

    public IReadOnlyList<string> TypeOptions { get; } = new[] { AllTypes }.Concat(SupportedEntityTypes.All).ToList();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCheckCommand))]
    private string _modelPath = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCheckCommand))]
    private ProfileOption? _selectedProfile;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    [NotifyCanExecuteChangedFor(nameof(RunCheckCommand), nameof(BrowseCommand), nameof(CancelCommand), nameof(ExportReportCommand))]
    private bool _isRunning;

    public bool IsIdle => !IsRunning;

    [ObservableProperty]
    private string? _progressText;

    /// <summary>The last completed run. Never replaced by a canceled or failed run.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasResults))]
    [NotifyCanExecuteChangedFor(nameof(ExportReportCommand))]
    private ValidationRun? _currentRun;

    public bool HasResults => CurrentRun is not null;

    [ObservableProperty]
    private int _missingMarks;

    [ObservableProperty]
    private int _duplicateMarks;

    [ObservableProperty]
    private int _namingIssues;

    /// <summary>Which run is displayed: file, profile, element count, time.</summary>
    [ObservableProperty]
    private string? _resultsCaption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarnings))]
    private string? _warningsText;

    public bool HasWarnings => WarningsText is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMessage))]
    private string? _messageText;

    [ObservableProperty]
    private MessageKind _messageKind;

    public bool HasMessage => !string.IsNullOrEmpty(MessageText);

    [ObservableProperty]
    private string _searchText = "";

    [ObservableProperty]
    private string _selectedSeverity = AllSeverities;

    [ObservableProperty]
    private string _selectedRule = AllRules;

    [ObservableProperty]
    private string _selectedType = AllTypes;

    /// <summary>Visible rows. Replaced as a whole on every filter change (fast for large result sets).</summary>
    [ObservableProperty]
    private IReadOnlyList<FindingRow> _findings = Array.Empty<FindingRow>();

    [ObservableProperty]
    private string _findingsHeader = "Findings (0)";

    private const string NoRunText = "No check has run yet. Choose an IFC model and a profile, then select Run Check.";

    /// <summary>Shown over the table when it has no rows; null when rows are visible.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEmptyText))]
    private string? _emptyText = NoRunText;

    public bool HasEmptyText => EmptyText is not null;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    [NotifyCanExecuteChangedFor(nameof(CopyElementIdCommand))]
    private FindingRow? _selectedFinding;

    public bool HasSelection => SelectedFinding is not null;

    [ObservableProperty]
    private string? _copyStatus;

    public int TotalFindings => _allRows.Count;

    // ------------------------------------------------------------------ commands

    [RelayCommand(CanExecute = nameof(IsIdle))]
    private void Browse()
    {
        var path = _dialogs.PickModelFile(ModelPath);
        if (path is not null)
            ModelPath = path;
    }

    private bool CanRunCheck() => IsIdle && !string.IsNullOrWhiteSpace(ModelPath) && SelectedProfile is not null;

    [RelayCommand(CanExecute = nameof(CanRunCheck))]
    private async Task RunCheckAsync()
    {
        var option = SelectedProfile!;
        var modelPath = ModelPath.Trim().Trim('"');

        // Re-read the profile so edits made since startup are validated too. Invalid => the check never starts.
        var loaded = _profileStore.Load(option.Path);
        if (loaded.Profile is null)
        {
            ShowMessage(MessageKind.Error,
                $"The profile '{option.FileName}' is invalid and cannot be used: {string.Join(" ", loaded.Errors)}");
            SetStatus(StatusKind.Failed, "Check not started");
            return;
        }

        IsRunning = true;
        CopyStatus = null;
        ShowMessage(MessageKind.None, null);
        SetStatus(StatusKind.Busy, "Importing");
        ProgressText = "Opening file…";
        _cancellation = new CancellationTokenSource();
        var progress = new Progress<CheckProgress>(OnProgress);
        try
        {
            var run = await _validation.RunAsync(modelPath, loaded.Profile, progress, _cancellation.Token);
            ShowRun(run);
            SetStatus(StatusKind.Complete, "Check complete", run.Schema);
        }
        catch (OperationCanceledException)
        {
            SetStatus(StatusKind.Canceled, "Check canceled");
            ShowMessage(MessageKind.Warning, CurrentRun is null
                ? "Check canceled. No results are shown."
                : "Check canceled — showing the previous completed check.");
        }
        catch (ModelReadException ex)
        {
            SetStatus(StatusKind.Failed, "Check failed");
            ShowMessage(MessageKind.Error, ex.Message + PreviousRunNote());
        }
        catch (Exception ex)
        {
            SetStatus(StatusKind.Failed, "Check failed");
            ShowMessage(MessageKind.Error, $"The check failed unexpectedly: {ex.Message}{PreviousRunNote()}");
        }
        finally
        {
            _cancellation.Dispose();
            _cancellation = null;
            ProgressText = null;
            IsRunning = false;
        }
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Cancel()
    {
        ProgressText = "Canceling…";
        _cancellation?.Cancel();
    }

    private bool CanExportReport() => IsIdle && CurrentRun is not null;

    /// <summary>Opens the Reports page (Q4.1); the export itself lives there.</summary>
    [RelayCommand(CanExecute = nameof(CanExportReport))]
    private void ExportReport() => _openReports();

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CopyElementId()
    {
        var id = SelectedFinding!.GlobalId;
        CopyStatus = _clipboard.TrySetText(id)
            ? $"Copied {id}"
            : "The clipboard is in use by another program. Try again.";
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchText = "";
        SelectedSeverity = AllSeverities;
        SelectedRule = AllRules;
        SelectedType = AllTypes;
    }

    // ------------------------------------------------------------------ results and filters

    partial void OnSearchTextChanged(string value) => ApplyFilters();

    partial void OnSelectedSeverityChanged(string value) => ApplyFilters();

    partial void OnSelectedRuleChanged(string value) => ApplyFilters();

    partial void OnSelectedTypeChanged(string value) => ApplyFilters();

    partial void OnSelectedFindingChanged(FindingRow? value) => CopyStatus = null;

    private void ShowRun(ValidationRun run)
    {
        CurrentRun = run;
        _allRows = run.Findings.Select(f => new FindingRow(f, run.LabelFor(f.ElementGlobalId))).ToList();
        OnPropertyChanged(nameof(TotalFindings));

        int Failed(RuleType type) => run.RuleResults.Where(r => r.Rule.Type == type).Sum(r => r.FailedCount);
        MissingMarks = Failed(RuleType.RequiredProperty);
        DuplicateMarks = Failed(RuleType.UniqueValue);
        NamingIssues = Failed(RuleType.NamingPattern);

        RuleOptions.Clear();
        RuleOptions.Add(AllRules);
        foreach (var name in run.RuleResults.Select(r => r.Rule.Name).Distinct())
            RuleOptions.Add(name);

        ResultsCaption = string.Format(CultureInfo.CurrentCulture, "{0} • {1} {2} • {3:N0} elements • checked {4:HH:mm:ss}",
            Path.GetFileName(run.SourcePath), run.ProfileName, run.ProfileVersion, run.ElementCount, run.FinishedAt);
        WarningsText = run.Warnings.Count == 0
            ? null
            : $"Import warnings ({run.Warnings.Count}): " + string.Join(" ", run.Warnings);

        SelectedFinding = null;
        ClearFilters();
        ApplyFilters();
        SelectedFinding = Findings.FirstOrDefault();
    }

    private void ApplyFilters()
    {
        var search = SearchText.Trim();
        var rows = _allRows.Where(r =>
                (SelectedSeverity == AllSeverities || r.SeverityText == SelectedSeverity) &&
                (SelectedRule == AllRules || r.Rule == SelectedRule) &&
                (SelectedType == AllTypes || r.EntityType == SelectedType) &&
                (search.Length == 0 || r.Matches(search)))
            .ToList();

        var selected = SelectedFinding;
        Findings = rows;
        FindingsHeader = rows.Count == _allRows.Count
            ? $"Findings ({_allRows.Count:N0})"
            : $"Findings ({rows.Count:N0} of {_allRows.Count:N0})";
        EmptyText = CurrentRun is null ? NoRunText
            : _allRows.Count == 0 ? "No findings. Every evaluated element passed the profile's rules."
            : rows.Count == 0 ? "No findings match the filters."
            : null;
        // The table may clear its selection when the rows are replaced; keep it if the row is still visible.
        SelectedFinding = selected is not null && rows.Contains(selected) ? selected : null;
    }

    private void OnProgress(CheckProgress progress)
    {
        if (!IsRunning || _cancellation is { IsCancellationRequested: true })
            return;
        if (progress.Stage == CheckStage.Validating)
        {
            SetStatus(StatusKind.Busy, "Validating");
            ProgressText = $"Validating {progress.ElementsRead:N0} elements…";
        }
        else
        {
            ProgressText = progress.ElementsRead > 0
                ? $"{progress.Stage}… {progress.ElementsRead:N0} elements"
                : $"{progress.Stage}…";
        }
    }

    private void LoadProfiles(string folder)
    {
        if (Directory.Exists(folder))
        {
            foreach (var path in Directory.GetFiles(folder, "*.json").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
                Profiles.Add(new ProfileOption(path, _profileStore.Load(path)));
        }
        SelectedProfile = Profiles.FirstOrDefault(p => p.Result.Profile?.ProfileId == "member-information")
                          ?? Profiles.FirstOrDefault(p => p.Result.IsValid)
                          ?? Profiles.FirstOrDefault();
        if (Profiles.Count == 0)
            ShowMessage(MessageKind.Warning, $"No rule profiles were found in '{folder}'.");
    }

    private string PreviousRunNote() => CurrentRun is null ? "" : " The previous completed check is still shown.";

    private void ShowMessage(MessageKind kind, string? text)
    {
        MessageKind = kind;
        MessageText = text;
    }

    private void SetStatus(StatusKind kind, string state, string? schema = null)
    {
        StatusKind = kind;
        StatusText = schema is null ? $"Local processing • {state}" : $"{schema} • Local processing • {state}";
    }
}
