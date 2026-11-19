using PimpMyWindows.Core.Safety;
using PimpMyWindows.Core.Settings;

namespace PimpMyWindows.Desktop.Services;

/// <summary>Cria o ponto de restauração do Windows e lembra quando foi o último.</summary>
public sealed class RestorePointCoordinator
{
    private readonly RestorePointService _service;
    private readonly UserSettings _settings;
    private readonly JsonSettingsStore _store;
    private readonly AppState _state;

    public RestorePointCoordinator(
        RestorePointService service,
        UserSettings settings,
        JsonSettingsStore store,
        AppState state)
    {
        _service = service;
        _settings = settings;
        _store = store;
        _state = state;
    }

    public DateTimeOffset? LastCreatedAt => _settings.LastRestorePointAt;

    public bool CreateBeforeApplyDefault => _settings.CreateRestorePointBeforeApply;

    public void RememberPreference(bool createBeforeApply)
    {
        _settings.CreateRestorePointBeforeApply = createBeforeApply;
        _store.Save(_settings);
    }

    public async Task<PimpMyWindows.Core.OperationResult> CreateAsync()
    {
        _state.Say("Criando ponto de restauração… isso pode levar um minuto.");
        var result = await _service.CreateAsync("Pimp my Windows - antes de otimizar");

        if (result.Success)
        {
            _settings.LastRestorePointAt = DateTimeOffset.Now;
            _store.Save(_settings);
            _state.NotifyStateChanged();
        }

        _state.Say(result.Message);
        return result;
    }
}
