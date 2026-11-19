using PimpMyWindows.Core.Platform;
using PimpMyWindows.Core.Settings;

namespace PimpMyWindows.Desktop.Theme;

/// <summary>Escolhe o tema (claro, escuro ou o do Windows), aplica e guarda a preferência.</summary>
public sealed class ThemeController
{
    private readonly UserSettings _settings;
    private readonly JsonSettingsStore _store;
    private readonly IRegistryAccess _registry;

    public ThemeController(UserSettings settings, JsonSettingsStore store, IRegistryAccess registry)
    {
        _settings = settings;
        _store = store;
        _registry = registry;
    }

    public bool IsLight { get; private set; }

    public void ApplySaved() => Apply(_settings.Theme);

    public void Toggle()
    {
        _settings.Theme = IsLight ? ThemeChoice.Dark : ThemeChoice.Light;
        _store.Save(_settings);
        Apply(_settings.Theme);
    }

    private void Apply(ThemeChoice choice)
    {
        IsLight = choice switch
        {
            ThemeChoice.Light => true,
            ThemeChoice.Dark => false,
            _ => SystemTheme.IsLight(_registry),
        };

        ThemeManager.Apply(IsLight);
    }
}
