using System.Windows;
using PimpMyWindows.Desktop.Theme;

namespace PimpMyWindows.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        SourceInitialized += (_, _) => UpdateTitleBar();
        ThemeManager.ThemeChanged += UpdateTitleBar;
        Closed += (_, _) => ThemeManager.ThemeChanged -= UpdateTitleBar;
    }

    private void UpdateTitleBar()
    {
        var isLight = Application.Current.Resources["Text"] is System.Windows.Media.SolidColorBrush { Color.R: < 100 };
        WindowTheming.SetDarkTitleBar(this, dark: !isLight);
    }
}
