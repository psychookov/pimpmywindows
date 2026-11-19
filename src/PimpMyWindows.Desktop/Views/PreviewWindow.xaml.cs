using System.Windows;
using PimpMyWindows.Desktop.Services;
using PimpMyWindows.Desktop.Theme;

namespace PimpMyWindows.Desktop.Views;

public partial class PreviewWindow : Window
{
    public PreviewWindow(PlanPreviewModel model)
    {
        InitializeComponent();
        DataContext = model;
        SourceInitialized += (_, _) => WindowTheming.SetDarkTitleBar(this, dark: !IsLightTheme());
    }

    private static bool IsLightTheme() =>
        Application.Current.Resources["Text"] is System.Windows.Media.SolidColorBrush { Color.R: < 100 };

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}
