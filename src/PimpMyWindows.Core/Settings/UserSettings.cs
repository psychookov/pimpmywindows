using System.Text.Json;
using PimpMyWindows.Core.Platform;

namespace PimpMyWindows.Core.Settings;

public enum ThemeChoice
{
    System,
    Light,
    Dark,
}

public sealed class UserSettings
{
    public ThemeChoice Theme { get; set; } = ThemeChoice.System;

    public bool CreateRestorePointBeforeApply { get; set; } = true;

    public DateTimeOffset? LastRestorePointAt { get; set; }
}

/// <summary>Guarda as preferências em JSON. Nunca lança erro: arquivo ausente ou corrompido = padrões.</summary>
public sealed class JsonSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    public JsonSettingsStore(string path)
    {
        _path = path;
    }

    public UserSettings Load()
    {
        try
        {
            return File.Exists(_path)
                ? JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(_path)) ?? new UserSettings()
                : new UserSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return new UserSettings();
        }
    }

    public void Save(UserSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temp = _path + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(settings, Options));
            File.Move(temp, _path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Preferência não salva não pode derrubar o programa.
        }
    }
}

public static class SystemTheme
{
    private static readonly RegistryLocation AppsUseLightTheme = new(
        RegistryRoot.CurrentUser,
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
        "AppsUseLightTheme");

    /// <summary>Verdadeiro se o Windows está no tema claro. Sem informação, assume escuro.</summary>
    public static bool IsLight(IRegistryAccess registry)
    {
        try
        {
            var value = registry.Read(AppsUseLightTheme);
            return value is { Kind: RegistryDataKind.DWord } && value.AsDWord() == 1;
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            return false;
        }
    }
}
