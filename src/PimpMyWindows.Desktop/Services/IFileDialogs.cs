using Microsoft.Win32;

namespace PimpMyWindows.Desktop.Services;

public interface IFileDialogs
{
    string? PickSavePath(string title, string defaultFileName);

    string? PickOpenPath(string title);

    string? PickFolder(string title);
}

public sealed class WindowsFileDialogs : IFileDialogs
{
    private const string Filter = "Seleção do Pimp my Windows (*.json)|*.json|Todos os arquivos (*.*)|*.*";

    public string? PickSavePath(string title, string defaultFileName)
    {
        var dialog = new SaveFileDialog { Title = title, FileName = defaultFileName, Filter = Filter, DefaultExt = ".json" };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickOpenPath(string title)
    {
        var dialog = new OpenFileDialog { Title = title, Filter = Filter, CheckFileExists = true };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickFolder(string title)
    {
        var dialog = new OpenFolderDialog { Title = title };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }
}
