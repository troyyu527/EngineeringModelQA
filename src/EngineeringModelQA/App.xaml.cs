using System.Windows;
using EngineeringModelQA.Application;
using EngineeringModelQA.Ifc;
using EngineeringModelQA.Infrastructure;
using EngineeringModelQA.Services;
using EngineeringModelQA.ViewModels;

namespace EngineeringModelQA;

/// <summary>Composition root: creates the services and the main window (no DI container).</summary>
// The EngineeringModelQA.Application namespace hides WPF's Application here, so the base type is fully qualified.
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var profilesFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "profiles");
        var viewModel = new MainWindowViewModel(
            new ValidationService(new IfcModelReader()),
            new JsonProfileStore(),
            profilesFolder,
            new WpfDialogService(),
            new WpfClipboardService());

        MainWindow = new MainWindow { DataContext = viewModel };
        MainWindow.Show();
    }
}
