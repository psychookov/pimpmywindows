using System.Windows;
using PimpMyWindows.Desktop.Views;

namespace PimpMyWindows.Desktop.Services;

/// <summary>Caixas de diálogo usadas pela interface (separadas para manter a lógica testável).</summary>
public interface IUserPrompt
{
    bool Confirm(string title, string message);

    void ShowInfo(string title, string message);

    void ShowError(string title, string message);

    /// <summary>Mostra a conferência do que será alterado. Retorna verdadeiro se o usuário confirmou.</summary>
    bool ShowPlanPreview(PlanPreviewModel model);
}

public sealed class MessageBoxPrompt : IUserPrompt
{
    public bool Confirm(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            == MessageBoxResult.Yes;

    public void ShowInfo(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string title, string message) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

    public bool ShowPlanPreview(PlanPreviewModel model)
    {
        var window = new PreviewWindow(model) { Owner = Application.Current.MainWindow };
        return window.ShowDialog() == true;
    }
}
