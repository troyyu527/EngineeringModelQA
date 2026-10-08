using CommunityToolkit.Mvvm.ComponentModel;
using EngineeringModelQA.Application;
using EngineeringModelQA.Application.Contracts;
using EngineeringModelQA.Services;

namespace EngineeringModelQA.ViewModels;

/// <summary>Navigation rail and the page shown next to it.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    public MainWindowViewModel(ValidationService validation, IProfileStore profileStore, string profilesFolder,
        IDialogService dialogs, IClipboardService clipboard)
    {
        RevisionCompare = new PlaceholderViewModel("Revision Compare", "",
            "Compare a baseline and a revised model with the same profile. Coming in a later step.");
        Reports = new PlaceholderViewModel("Reports", "",
            "Preview the latest model check and export it as CSV or HTML. Coming in the next step.");
        Settings = new PlaceholderViewModel("Settings", "",
            "Validate rule profiles and set export preferences. Coming in a later step.");
        ModelCheck = new ModelCheckViewModel(validation, profileStore, profilesFolder, dialogs, clipboard,
            openReports: () => CurrentPage = Reports);

        Pages = new PageViewModel[] { ModelCheck, RevisionCompare, Reports, Settings };
        _currentPage = ModelCheck;
    }

    public IReadOnlyList<PageViewModel> Pages { get; }

    public ModelCheckViewModel ModelCheck { get; }

    public PlaceholderViewModel RevisionCompare { get; }

    public PlaceholderViewModel Reports { get; }

    public PlaceholderViewModel Settings { get; }

    [ObservableProperty]
    private PageViewModel _currentPage;

    public string VersionText { get; } = "v" + typeof(MainWindowViewModel).Assembly.GetName().Version!.ToString(3);
}
