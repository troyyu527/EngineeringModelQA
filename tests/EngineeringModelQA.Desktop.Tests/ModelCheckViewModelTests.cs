using EngineeringModelQA.Application;
using EngineeringModelQA.Application.Contracts;
using EngineeringModelQA.Core.Records;
using EngineeringModelQA.Ifc;
using EngineeringModelQA.Infrastructure;
using EngineeringModelQA.Services;
using EngineeringModelQA.ViewModels;

namespace EngineeringModelQA.Desktop.Tests;

public sealed class ModelCheckViewModelTests : IDisposable
{
    private static readonly string Samples = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples");
    private static readonly string Baseline = Path.Combine(Samples, "fixtures", "baseline.ifc");

    private readonly string _profilesFolder = Path.Combine(Path.GetTempPath(), "emqa-tests-" + Guid.NewGuid().ToString("N"));
    private readonly FakeClipboard _clipboard = new();
    private int _reportsOpened;

    public ModelCheckViewModelTests()
    {
        Directory.CreateDirectory(_profilesFolder);
        foreach (var file in Directory.GetFiles(Path.Combine(Samples, "profiles"), "*.json"))
            File.Copy(file, Path.Combine(_profilesFolder, Path.GetFileName(file)));
    }

    public void Dispose() => Directory.Delete(_profilesFolder, recursive: true);

    private ModelCheckViewModel Create(IModelReader? reader = null) =>
        new(new ValidationService(reader ?? new IfcModelReader()), new JsonProfileStore(), _profilesFolder,
            new FakeDialogs(Baseline), _clipboard, () => _reportsOpened++);

    private static async Task<ModelCheckViewModel> RunBaseline(ModelCheckViewModel vm)
    {
        vm.ModelPath = Baseline;
        await vm.RunCheckCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public void Start_ListsProfiles_SelectsMemberInformation_AndShowsEmptyState()
    {
        var vm = Create();

        Assert.Equal(new[] { "Member Information", "Structural Full" }, vm.Profiles.Select(p => p.DisplayName));
        Assert.Equal("Member Information", vm.SelectedProfile!.DisplayName);
        Assert.False(vm.RunCheckCommand.CanExecute(null)); // no model yet
        Assert.False(vm.ExportReportCommand.CanExecute(null));
        Assert.False(vm.HasResults);
        Assert.StartsWith("No check has run yet", vm.EmptyText);
        Assert.Equal("Local processing • Ready", vm.StatusText);
    }

    [Fact]
    public async Task Run_FillsCardsTableDetailsAndStatus()
    {
        var vm = await RunBaseline(Create());

        Assert.Equal((2, 2, 2), (vm.MissingMarks, vm.DuplicateMarks, vm.NamingIssues));
        Assert.Equal("Findings (6)", vm.FindingsHeader);
        Assert.Equal(6, vm.Findings.Count);
        Assert.Equal(new[] { "(blank)", "(missing)", "B-105", "B-105", "C-2O3", "B204" }, vm.Findings.Select(r => r.Observed));
        Assert.Equal(vm.Findings[0], vm.SelectedFinding);
        Assert.Equal("Required mark", vm.SelectedFinding!.Rule);
        Assert.Equal("Non-empty ProjectInformation.MemberMark", vm.SelectedFinding.Expected);
        Assert.Equal(StatusKind.Complete, vm.StatusKind);
        Assert.Equal("IFC4 • Local processing • Check complete", vm.StatusText);
        Assert.StartsWith("baseline.ifc • Member Information 1.0 • 32 elements", vm.ResultsCaption);
        Assert.Null(vm.EmptyText);
        Assert.False(vm.HasMessage);
        Assert.True(vm.ExportReportCommand.CanExecute(null));
    }

    [Fact]
    public async Task Run_StructuralFull_ShowsAllRuleNamesInFilter()
    {
        var vm = Create();
        vm.SelectedProfile = vm.Profiles.Single(p => p.DisplayName == "Structural Full");

        await RunBaseline(vm);

        Assert.Equal(8, vm.TotalFindings);
        Assert.Equal(new[] { ModelCheckViewModel.AllRules, "Required mark", "Unique mark", "Naming pattern", "Material assigned", "Storey assigned" },
            vm.RuleOptions);
        Assert.Equal((2, 2, 2), (vm.MissingMarks, vm.DuplicateMarks, vm.NamingIssues));
    }

    [Theory]
    [InlineData("search", "B-105", 2)]
    [InlineData("search", "b-105", 2)]
    [InlineData("search", "1mpdRzgi4ejcSNvPuYRwuU", 1)]
    [InlineData("search", "nothing like this", 0)]
    [InlineData("severity", "Warning", 2)]
    [InlineData("severity", "Error", 4)]
    [InlineData("severity", "Info", 0)]
    [InlineData("rule", "Unique mark", 2)]
    [InlineData("type", "IfcColumn", 2)]
    [InlineData("type", "IfcSlab", 0)]
    public async Task Filters_HideAndShowRows(string filter, string value, int expected)
    {
        var vm = await RunBaseline(Create());

        switch (filter)
        {
            case "search": vm.SearchText = value; break;
            case "severity": vm.SelectedSeverity = value; break;
            case "rule": vm.SelectedRule = value; break;
            default: vm.SelectedType = value; break;
        }

        Assert.Equal(expected, vm.Findings.Count);
        Assert.Equal(expected == 6 ? "Findings (6)" : $"Findings ({expected} of 6)", vm.FindingsHeader);
        Assert.Equal(expected == 0 ? "No findings match the filters." : null, vm.EmptyText);

        vm.ClearFiltersCommand.Execute(null);
        Assert.Equal(6, vm.Findings.Count);
    }

    [Fact]
    public async Task Filters_Combine()
    {
        var vm = await RunBaseline(Create());

        vm.SelectedSeverity = "Error";
        vm.SelectedType = "IfcColumn";

        Assert.Equal("(blank)", Assert.Single(vm.Findings).Observed);
    }

    [Fact]
    public async Task Filter_KeepsSelectionWhenVisible_AndClearsItOtherwise()
    {
        var vm = await RunBaseline(Create());
        var duplicate = vm.Findings.First(r => r.Rule == "Unique mark");
        vm.SelectedFinding = duplicate;

        vm.SearchText = "B-105";
        Assert.Same(duplicate, vm.SelectedFinding);

        vm.SelectedSeverity = "Warning";
        Assert.Null(vm.SelectedFinding);
        Assert.False(vm.CopyElementIdCommand.CanExecute(null));
    }

    [Fact]
    public async Task CopyElementId_PutsGlobalIdOnClipboard()
    {
        var vm = await RunBaseline(Create());
        vm.SelectedFinding = vm.Findings[1];

        vm.CopyElementIdCommand.Execute(null);

        Assert.Equal("1mpdRzgi4ejcSNvPuYRwuU", _clipboard.Text);
        Assert.Equal("Copied 1mpdRzgi4ejcSNvPuYRwuU", vm.CopyStatus);
    }

    [Fact]
    public async Task Running_BlocksRunAndBrowse_AndEnablesCancel()
    {
        var reader = new BlockingReader();
        var vm = Create(reader);
        vm.ModelPath = Baseline;

        var run = vm.RunCheckCommand.ExecuteAsync(null);
        await reader.Started.Task;

        Assert.True(vm.IsRunning);
        Assert.False(vm.RunCheckCommand.CanExecute(null));
        Assert.False(vm.BrowseCommand.CanExecute(null));
        Assert.False(vm.ExportReportCommand.CanExecute(null));
        Assert.True(vm.CancelCommand.CanExecute(null));
        Assert.Equal(StatusKind.Busy, vm.StatusKind);

        vm.CancelCommand.Execute(null);
        await run;
        Assert.False(vm.IsRunning);
        Assert.True(vm.RunCheckCommand.CanExecute(null));
    }

    [Fact]
    public async Task Cancel_WithoutPreviousResults_ShowsNoResults_AndIsNotComplete()
    {
        var reader = new BlockingReader();
        var vm = Create(reader);
        vm.ModelPath = Baseline;

        var run = vm.RunCheckCommand.ExecuteAsync(null);
        await reader.Started.Task;
        vm.CancelCommand.Execute(null);
        await run;

        Assert.Null(vm.CurrentRun);
        Assert.Empty(vm.Findings);
        Assert.Equal((0, 0, 0), (vm.MissingMarks, vm.DuplicateMarks, vm.NamingIssues));
        Assert.Equal(StatusKind.Canceled, vm.StatusKind);
        Assert.DoesNotContain("complete", vm.StatusText);
        Assert.Equal("Check canceled. No results are shown.", vm.MessageText);
    }

    [Fact]
    public async Task Cancel_AfterACompletedRun_KeepsThePreviousResults_ThenANewRunWorks()
    {
        var reader = new BlockingReader();
        var vm = await RunBaseline(Create(new SwitchReader(new IfcModelReader(), reader)));
        var previous = vm.CurrentRun;

        var run = vm.RunCheckCommand.ExecuteAsync(null);
        await reader.Started.Task;
        vm.CancelCommand.Execute(null);
        await run;

        Assert.Same(previous, vm.CurrentRun);
        Assert.Equal(6, vm.Findings.Count);
        Assert.Equal("Check canceled — showing the previous completed check.", vm.MessageText);
        Assert.Equal("Local processing • Check canceled", vm.StatusText);

        await vm.RunCheckCommand.ExecuteAsync(null); // SwitchReader uses the real reader again
        Assert.NotSame(previous, vm.CurrentRun);
        Assert.Equal(StatusKind.Complete, vm.StatusKind);
        Assert.False(vm.HasMessage);
    }

    [Fact]
    public async Task FailedRun_KeepsPreviousResults_AndShowsTheReason()
    {
        var vm = await RunBaseline(Create());
        var previous = vm.CurrentRun;

        vm.ModelPath = Path.Combine(Samples, "fixtures", "invalid", "ifc2x3.ifc");
        await vm.RunCheckCommand.ExecuteAsync(null);

        Assert.Same(previous, vm.CurrentRun);
        Assert.Equal(6, vm.Findings.Count);
        Assert.Equal(StatusKind.Failed, vm.StatusKind);
        Assert.Equal(MessageKind.Error, vm.MessageKind);
        Assert.Contains("IFC4 files only", vm.MessageText);
        Assert.EndsWith("The previous completed check is still shown.", vm.MessageText);
    }

    [Fact]
    public async Task InvalidProfile_NeverStartsTheReader()
    {
        File.WriteAllText(Path.Combine(_profilesFolder, "broken.json"), "{ \"profileId\": \"broken\", \"rules\": [] }");
        var reader = new BlockingReader();
        var vm = Create(reader);
        vm.SelectedProfile = vm.Profiles.Single(p => p.DisplayName == "broken.json (invalid)");
        vm.ModelPath = Baseline;

        await vm.RunCheckCommand.ExecuteAsync(null);

        Assert.False(reader.Started.Task.IsCompleted);
        Assert.Null(vm.CurrentRun);
        Assert.Equal(MessageKind.Error, vm.MessageKind);
        Assert.StartsWith("The profile 'broken.json' is invalid and cannot be used:", vm.MessageText);
    }

    [Fact]
    public async Task ExportReport_OpensReportsPage_AfterACheck()
    {
        var vm = await RunBaseline(Create());

        vm.ExportReportCommand.Execute(null);

        Assert.Equal(1, _reportsOpened);
    }

    [Fact]
    public void Browse_SetsTheChosenPath()
    {
        var vm = Create();

        vm.BrowseCommand.Execute(null);

        Assert.Equal(Baseline, vm.ModelPath);
        Assert.True(vm.RunCheckCommand.CanExecute(null));
    }

    [Fact]
    public async Task MainWindow_HasFourPages_AndExportNavigatesToReports()
    {
        var main = new MainWindowViewModel(new ValidationService(new IfcModelReader()), new JsonProfileStore(), _profilesFolder,
            new FakeDialogs(Baseline), _clipboard);

        Assert.Equal(new[] { "Model Check", "Revision Compare", "Reports", "Settings" }, main.Pages.Select(p => p.Title));
        Assert.Same(main.ModelCheck, main.CurrentPage);

        await RunBaseline(main.ModelCheck);
        main.ModelCheck.ExportReportCommand.Execute(null);

        Assert.Same(main.Reports, main.CurrentPage);
    }

    // ------------------------------------------------------------------ fakes

    private sealed class FakeDialogs(string path) : IDialogService
    {
        public string? PickModelFile(string? currentPath) => path;
    }

    private sealed class FakeClipboard : IClipboardService
    {
        public string? Text { get; private set; }

        public bool TrySetText(string text)
        {
            Text = text;
            return true;
        }
    }

    /// <summary>Waits until canceled; signals when the read has started.</summary>
    private sealed class BlockingReader : IModelReader
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ModelSnapshot> ReadAsync(string path, IProgress<ImportProgress>? progress, CancellationToken cancellationToken)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("unreachable");
        }
    }

    /// <summary>Real reader on the 1st, 3rd, ... call; blocking reader on the 2nd.</summary>
    private sealed class SwitchReader(IModelReader real, IModelReader blocking) : IModelReader
    {
        private int _calls;

        public Task<ModelSnapshot> ReadAsync(string path, IProgress<ImportProgress>? progress, CancellationToken cancellationToken) =>
            ++_calls == 2 ? blocking.ReadAsync(path, progress, cancellationToken) : real.ReadAsync(path, progress, cancellationToken);
    }
}
