using System.Windows;
using System.Windows.Media;

namespace PimpMyWindows.Desktop.Theme;

/// <summary>
/// Troca as cores do programa em tempo de execução: os pincéis de Theme.xaml são substituídos
/// em Application.Resources e tudo que usa DynamicResource se atualiza na hora.
/// </summary>
public static class ThemeManager
{
    private static readonly Dictionary<string, string> Dark = new()
    {
        ["SidebarBg"] = "#0E141B", ["Surface"] = "#121820", ["SurfaceAlt"] = "#17202A", ["SurfaceHover"] = "#1B2733",
        ["Line"] = "#1E2833", ["Text"] = "#E8EEF4", ["Muted"] = "#8A97A6", ["Accent"] = "#19D3A2",
        ["AccentHover"] = "#3BE8B8", ["OnAccent"] = "#04110D", ["AccentSoft"] = "#0F3A33", ["Success"] = "#3DDC97",
        ["SuccessBg"] = "#12332A", ["Warning"] = "#F5B63D", ["WarningBg"] = "#3A2E12", ["Danger"] = "#FF6B6B",
        ["DangerBg"] = "#3A1A1C", ["Info"] = "#5CB8FF", ["InfoBg"] = "#12293D", ["ToggleOff"] = "#3A4756",
    };

    private static readonly Dictionary<string, string> Light = new()
    {
        ["SidebarBg"] = "#FFFFFF", ["Surface"] = "#FFFFFF", ["SurfaceAlt"] = "#F1F4F7", ["SurfaceHover"] = "#EEF2F6",
        ["Line"] = "#DDE3EA", ["Text"] = "#14202B", ["Muted"] = "#5C6B7A", ["Accent"] = "#0BA77F",
        ["AccentHover"] = "#089470", ["OnAccent"] = "#FFFFFF", ["AccentSoft"] = "#D9F4EC", ["Success"] = "#18865C",
        ["SuccessBg"] = "#DDF3E8", ["Warning"] = "#9A6700", ["WarningBg"] = "#FFF1CC", ["Danger"] = "#C0392B",
        ["DangerBg"] = "#FBE0DD", ["Info"] = "#0B6FB8", ["InfoBg"] = "#DDEEFA", ["ToggleOff"] = "#B4BFCA",
    };

    public static event Action? ThemeChanged;

    public static void Apply(bool light)
    {
        var resources = Application.Current.Resources;
        foreach (var (key, hex) in light ? Light : Dark)
        {
            resources[key] = Frozen(hex);
        }

        resources["AppBg"] = light ? Frozen("#F2F5F8") : DarkBackground();
        ThemeChanged?.Invoke();
    }

    private static SolidColorBrush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }

    private static Brush DarkBackground()
    {
        var brush = new LinearGradientBrush(
            (Color)ColorConverter.ConvertFromString("#0A0E14"),
            (Color)ColorConverter.ConvertFromString("#0C1824"),
            new Point(0, 0),
            new Point(1, 1));
        brush.Freeze();
        return brush;
    }
}
