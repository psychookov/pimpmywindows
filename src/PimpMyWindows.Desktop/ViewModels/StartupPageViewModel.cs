using System.Collections.ObjectModel;
using System.Windows.Input;
using PimpMyWindows.Core.Startup;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class StartupPageViewModel : PageViewModel
{
    private readonly StartupService _startup;
    private readonly IUserPrompt _prompt;
    private readonly AppState _state;
    private string _summaryText = string.Empty;
    private int _count;

    public StartupPageViewModel(StartupService startup, IUserPrompt prompt, AppState state)
    {
        _startup = startup;
        _prompt = prompt;
        _state = state;
        ToggleCommand = new RelayCommand(p => Toggle((StartupItemViewModel)p!), p => p is StartupItemViewModel);
        Load();
    }

    public override string Key => "startup";

    public ObservableCollection<StartupItemViewModel> Items { get; } = [];

    public ICommand ToggleCommand { get; }

    public int Count
    {
        get => _count;
        private set => SetProperty(ref _count, value);
    }

    public string SummaryText
    {
        get => _summaryText;
        private set => SetProperty(ref _summaryText, value);
    }

    public override void OnShown() => Load();

    private void Load()
    {
        Items.Clear();
        foreach (var entry in _startup.List())
        {
            Items.Add(new StartupItemViewModel(entry));
        }

        Count = Items.Count;
        var enabled = Items.Count(i => i.IsEnabled);
        var missing = Items.Count(i => i.FileMissing);
        SummaryText = $"{enabled} de {Items.Count} programas iniciam com o Windows" +
                      (missing > 0 ? $" · {missing} apontam para programas que não existem mais" : string.Empty);
    }

    private void Toggle(StartupItemViewModel item)
    {
        var result = _startup.SetEnabled(item.Entry, !item.IsEnabled);
        _state.Say(result.Message);

        if (!result.Success)
        {
            _prompt.ShowError(item.Name, result.Message);
        }

        Load();
        _state.NotifyStateChanged();
    }
}
