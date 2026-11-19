using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using PimpMyWindows.Core.Tweaks;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class UndoEntryViewModel : ObservableObject
{
    private bool _isSelected;

    public UndoEntryViewModel(UndoEntry entry)
    {
        Entry = entry;
    }

    public UndoEntry Entry { get; }

    public string Name => Entry.Tweak.Name;

    public string Summary => Entry.Tweak.Summary;

    public string AppliedText => "Alterado em " + Entry.AppliedAt.LocalDateTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.GetCultureInfo("pt-BR"));

    public bool WasUndoneOutside => Entry.WasUndoneOutside;

    public bool CanRevert => !Entry.WasUndoneOutside;

    public bool IsPartial => Entry.CurrentState == TweakState.Partial;

    public string StateText => Entry.CurrentState switch
    {
        TweakState.Applied => "Ainda ativo",
        TweakState.Partial => "Parcialmente ativo",
        TweakState.NotApplied => "Já desfeito fora do programa",
        _ => "Estado desconhecido",
    };

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

/// <summary>Central Desfazer: tudo o que o programa já alterou, com o caminho de volta.</summary>
public sealed class UndoPageViewModel : PageViewModel
{
    private readonly UndoCenterService _undo;
    private readonly IUserPrompt _prompt;
    private readonly AppState _state;
    private int _count;

    public UndoPageViewModel(UndoCenterService undo, IUserPrompt prompt, AppState state)
    {
        _undo = undo;
        _prompt = prompt;
        _state = state;

        RevertCommand = new RelayCommand(p => RevertOne((UndoEntryViewModel)p!), p => p is UndoEntryViewModel);
        ReapplyCommand = new RelayCommand(p => ReapplyOne((UndoEntryViewModel)p!), p => p is UndoEntryViewModel);
        DiscardCommand = new RelayCommand(p => DiscardOne((UndoEntryViewModel)p!), p => p is UndoEntryViewModel);
        RevertSelectedCommand = new RelayCommand(_ => RevertMany(selectedOnly: true), _ => Entries.Any(e => e.IsSelected && e.CanRevert));
        RevertAllCommand = new RelayCommand(_ => RevertMany(selectedOnly: false), _ => Entries.Any(e => e.CanRevert));
        SelectAllCommand = new RelayCommand(_ => SelectAll());

        Load();
    }

    public override string Key => "undo";

    public ObservableCollection<UndoEntryViewModel> Entries { get; } = [];

    public int Count
    {
        get => _count;
        private set => SetProperty(ref _count, value);
    }

    public ICommand RevertCommand { get; }

    public ICommand ReapplyCommand { get; }

    public ICommand DiscardCommand { get; }

    public ICommand RevertSelectedCommand { get; }

    public ICommand RevertAllCommand { get; }

    public ICommand SelectAllCommand { get; }

    public override void OnShown() => Load();

    private void Load()
    {
        Entries.Clear();
        foreach (var entry in _undo.List())
        {
            Entries.Add(new UndoEntryViewModel(entry));
        }

        Count = Entries.Count;
    }

    private void SelectAll()
    {
        var selectAll = Entries.Any(e => !e.IsSelected);
        foreach (var entry in Entries)
        {
            entry.IsSelected = selectAll;
        }
    }

    private void RevertOne(UndoEntryViewModel item) =>
        Finish(_undo.Revert(item.Entry.Tweak), item.Name);

    private void ReapplyOne(UndoEntryViewModel item) =>
        Finish(_undo.Reapply(item.Entry.Tweak), item.Name);

    private void DiscardOne(UndoEntryViewModel item) =>
        Finish(_undo.Discard(item.Entry.Tweak), item.Name);

    private void Finish(PimpMyWindows.Core.OperationResult result, string name)
    {
        _state.Say(result.Message);
        if (!result.Success)
        {
            _prompt.ShowError(name, result.Message);
        }

        Load();
        _state.NotifyStateChanged();
    }

    private void RevertMany(bool selectedOnly)
    {
        var targets = Entries.Where(e => e.CanRevert && (!selectedOnly || e.IsSelected)).ToList();
        if (targets.Count == 0)
        {
            return;
        }

        var question = selectedOnly
            ? $"Desfazer {targets.Count} ajuste(s) selecionado(s)? Cada um volta aos valores que você tinha antes."
            : $"Desfazer TODOS os {targets.Count} ajustes feitos por este programa? Cada um volta aos valores que você tinha antes.";
        if (!_prompt.Confirm("Desfazer ajustes", question))
        {
            _state.Say("Cancelado. Nada foi alterado.");
            return;
        }

        var batch = _undo.RevertMany(targets.Select(t => t.Entry.Tweak));
        _state.Say(batch.FailedCount == 0
            ? $"{batch.SucceededCount} ajustes desfeitos."
            : $"{batch.SucceededCount} desfeitos, {batch.FailedCount} com problema.");

        if (batch.FailedCount > 0)
        {
            _prompt.ShowError(
                "Alguns ajustes não foram desfeitos",
                string.Join("\n\n", batch.Failures.Select(f => $"• {f.Tweak.Name}\n  {f.Result.Message}")));
        }

        Load();
        _state.NotifyStateChanged();
    }
}
