using System.Collections.ObjectModel;
using System.Windows.Input;
using PimpMyWindows.Core.Cleanup;
using PimpMyWindows.Desktop.Services;

namespace PimpMyWindows.Desktop.ViewModels;

public sealed class CleanupItemViewModel : ObservableObject
{
    private bool _isSelected;
    private CleanupScanItem _scan;

    public CleanupItemViewModel(CleanupScanItem scan)
    {
        _scan = scan;
        _isSelected = scan.Category.Recommended && !scan.Skipped && scan.FileCount > 0;
    }

    public CleanupScanItem Scan
    {
        get => _scan;
        set
        {
            if (SetProperty(ref _scan, value))
            {
                OnPropertyChanged(nameof(SizeText));
                OnPropertyChanged(nameof(CanSelect));
                OnPropertyChanged(nameof(DetailText));
                if (!CanSelect)
                {
                    IsSelected = false;
                }
            }
        }
    }

    public CleanupCategory Category => _scan.Category;

    public string Name => Category.Name;

    public string Description => Category.Description;

    public bool CanSelect => !_scan.Skipped && _scan.FileCount > 0;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value && CanSelect);
    }

    public string SizeText => _scan.Skipped ? "—" : _scan.SizeText;

    public string DetailText => _scan.Skipped
        ? _scan.SkipReason ?? string.Empty
        : _scan.FileCount == 0 ? "Nada para limpar." : $"{_scan.FileCount} arquivo(s)";
}

/// <summary>Limpeza de temporários: mostra o que será apagado e o tamanho antes, e só apaga o que você marcar.</summary>
public sealed class CleanupPageViewModel : PageViewModel
{
    private readonly CleanupService _service;
    private readonly IReadOnlyList<CleanupCategory> _categories;
    private readonly IUserPrompt _prompt;
    private readonly AppState _state;
    private bool _isBusy;
    private string _resultText = string.Empty;
    private string _selectionText = string.Empty;

    public CleanupPageViewModel(
        CleanupService service,
        IReadOnlyList<CleanupCategory> categories,
        IUserPrompt prompt,
        AppState state)
    {
        _service = service;
        _categories = categories;
        _prompt = prompt;
        _state = state;
        ScanCommand = new AsyncRelayCommand(_ => ScanAsync(), _ => !IsBusy);
        CleanCommand = new AsyncRelayCommand(_ => CleanAsync(), _ => !IsBusy && SelectedItems().Any());
    }

    public override string Key => "cleanup";

    public ObservableCollection<CleanupItemViewModel> Items { get; } = [];

    public ICommand ScanCommand { get; }

    public ICommand CleanCommand { get; }

    public bool IsBusy
    {
        get => _isBusy;
        private set => SetProperty(ref _isBusy, value);
    }

    public string ResultText
    {
        get => _resultText;
        private set
        {
            if (SetProperty(ref _resultText, value))
            {
                OnPropertyChanged(nameof(HasResult));
            }
        }
    }

    public bool HasResult => _resultText.Length > 0;

    public string SelectionText
    {
        get => _selectionText;
        private set => SetProperty(ref _selectionText, value);
    }

    public override void OnShown()
    {
        if (Items.Count == 0)
        {
            _ = ScanAsync();
        }
    }

    private IEnumerable<CleanupItemViewModel> SelectedItems() => Items.Where(i => i.IsSelected && i.CanSelect);

    private void UpdateSelectionText()
    {
        var selected = SelectedItems().ToList();
        SelectionText = selected.Count == 0
            ? "Marque o que quer limpar."
            : $"Marcado: {CleanupScanItem.FormatBytes(selected.Sum(i => i.Scan.TotalBytes))} em {selected.Sum(i => i.Scan.FileCount)} arquivo(s).";
    }

    private async Task ScanAsync()
    {
        IsBusy = true;
        ResultText = string.Empty;
        try
        {
            var scans = await Task.Run(() => _service.Scan(_categories));
            foreach (var old in Items)
            {
                old.PropertyChanged -= OnItemChanged;
            }

            Items.Clear();
            foreach (var scan in scans)
            {
                var item = new CleanupItemViewModel(scan);
                item.PropertyChanged += OnItemChanged;
                Items.Add(item);
            }

            UpdateSelectionText();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnItemChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CleanupItemViewModel.IsSelected))
        {
            UpdateSelectionText();
            CommandManager.InvalidateRequerySuggested();
        }
    }

    private async Task CleanAsync()
    {
        var selected = SelectedItems().ToList();
        var bytes = selected.Sum(i => i.Scan.TotalBytes);
        var files = selected.Sum(i => i.Scan.FileCount);
        if (!_prompt.Confirm(
                "Limpar arquivos",
                $"Serão apagados cerca de {files} arquivo(s), liberando até {CleanupScanItem.FormatBytes(bytes)}:\n\n" +
                string.Join("\n", selected.Select(i => $"• {i.Name} ({i.SizeText})")) +
                "\n\nAtenção: apagar arquivos não tem como desfazer. Arquivos em uso são pulados. Continuar?"))
        {
            return;
        }

        IsBusy = true;
        _state.Say("Limpando…");
        try
        {
            var results = await Task.Run(() => selected.Select(i => _service.Clean(i.Category)).ToList());
            var freed = results.Sum(r => r.FreedBytes);
            var locked = results.Sum(r => r.LockedFiles);
            ResultText = $"Pronto: {CleanupScanItem.FormatBytes(freed)} liberados em {results.Sum(r => r.DeletedFiles)} arquivo(s)." +
                         (locked > 0 ? $" {locked} arquivo(s) estavam em uso e foram deixados." : string.Empty);
            _state.Say("Limpeza concluída.");
        }
        finally
        {
            IsBusy = false;
        }

        await ScanAsync();
    }
}
