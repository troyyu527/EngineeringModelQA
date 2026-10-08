using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.Win32;

namespace EngineeringModelQA.Services;

/// <summary>File dialogs, behind an interface so view-models stay testable.</summary>
public interface IDialogService
{
    /// <returns>The chosen .ifc path, or null when the user cancels.</returns>
    string? PickModelFile(string? currentPath);
}

/// <summary>Clipboard access, behind an interface so view-models stay testable.</summary>
public interface IClipboardService
{
    /// <returns>False when the clipboard is held by another program.</returns>
    bool TrySetText(string text);
}

public sealed class WpfDialogService : IDialogService
{
    public string? PickModelFile(string? currentPath)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose an IFC4 model",
            Filter = "IFC files (*.ifc)|*.ifc|All files (*.*)|*.*",
            CheckFileExists = true,
        };
        if (!string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(Path.GetDirectoryName(currentPath)))
            dialog.InitialDirectory = Path.GetDirectoryName(currentPath);
        return dialog.ShowDialog(System.Windows.Application.Current?.MainWindow) == true ? dialog.FileName : null;
    }
}

public sealed class WpfClipboardService : IClipboardService
{
    public bool TrySetText(string text)
    {
        // Another program may hold the clipboard for a moment (CLIPBRD_E_CANT_OPEN); retry briefly.
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                Clipboard.SetText(text);
                return true;
            }
            catch (COMException)
            {
                Thread.Sleep(50);
            }
        }
        return false;
    }
}
