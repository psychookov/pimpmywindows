using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Cleanup;

/// <summary>
/// Limpa arquivos temporários seguros. Diferente dos ajustes, apagar arquivo <b>não tem como desfazer</b>,
/// por isso: só pastas de lixo conhecidas, só arquivos parados há pelo menos um dia, sempre com a quantidade e o tamanho
/// mostrados antes, e nunca se sai da pasta escolhida.
/// </summary>
public sealed class CleanupService
{
    private readonly ICleanupFileSystem _files;
    private readonly IActionLog _log;
    private readonly Func<bool> _isAdmin;
    private readonly Func<DateTime> _utcNow;

    public CleanupService(ICleanupFileSystem files, IActionLog log, Func<bool> isAdmin, Func<DateTime>? utcNow = null)
    {
        _files = files;
        _log = log;
        _isAdmin = isAdmin;
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>As categorias oferecidas. Os caminhos vêm de variáveis do próprio Windows, nunca digitados pelo usuário.</summary>
    public static IReadOnlyList<CleanupCategory> BuildCategories(string localAppData, string windowsDirectory)
    {
        var day = TimeSpan.FromDays(1);
        return
        [
            new("temp.user", "Temporários do seu usuário",
                "Arquivos que programas deixam em %TEMP% e esquecem. Só os parados há mais de 1 dia.",
                Path.Combine(localAppData, "Temp"), "*", false, day, true),
            new("temp.windows", "Temporários do Windows",
                "Arquivos de C:\\Windows\\Temp. Só os parados há mais de 1 dia. Exige administrador.",
                Path.Combine(windowsDirectory, "Temp"), "*", true, day, true),
            new("thumbs", "Cache de miniaturas",
                "Miniaturas de imagens e vídeos. O Windows refaz sozinho, só demora um pouco na próxima vez.",
                Path.Combine(localAppData, "Microsoft", "Windows", "Explorer"), "thumbcache_*.db", false, day, true),
            new("crashdumps", "Relatórios de travamento",
                "Cópias de memória que o Windows guarda quando um programa fecha com erro (CrashDumps).",
                Path.Combine(localAppData, "CrashDumps"), "*", false, TimeSpan.Zero, true),
            new("wer", "Relatórios de erro do Windows",
                "Relatórios que o Windows monta para enviar à Microsoft depois de um erro.",
                Path.Combine(localAppData, "Microsoft", "Windows", "WER"), "*", false, day, false),
        ];
    }

    public IReadOnlyList<CleanupScanItem> Scan(IEnumerable<CleanupCategory> categories) =>
        categories.Select(Scan).ToList();

    public CleanupScanItem Scan(CleanupCategory category)
    {
        if (category.RequiresAdmin && !_isAdmin())
        {
            return new CleanupScanItem(category, 0, 0, true, "Abra o programa como administrador para limpar esta pasta.");
        }

        if (!_files.DirectoryExists(category.Root))
        {
            return new CleanupScanItem(category, 0, 0, false, null);
        }

        var count = 0;
        long bytes = 0;
        foreach (var file in Candidates(category))
        {
            count++;
            bytes += file.SizeBytes;
        }

        return new CleanupScanItem(category, count, bytes, false, null);
    }

    public CleanupResult Clean(CleanupCategory category)
    {
        if ((category.RequiresAdmin && !_isAdmin()) || !_files.DirectoryExists(category.Root))
        {
            return new CleanupResult(category, 0, 0, 0);
        }

        var deleted = 0;
        var locked = 0;
        long freed = 0;
        foreach (var file in Candidates(category).ToList())
        {
            if (_files.TryDeleteFile(file.Path))
            {
                deleted++;
                freed += file.SizeBytes;
            }
            else
            {
                locked++;
            }
        }

        _files.DeleteEmptyDirectories(category.Root);
        _log.Write($"Limpeza '{category.Id}': {deleted} arquivo(s) apagado(s), {CleanupScanItem.FormatBytes(freed)}, {locked} em uso");
        return new CleanupResult(category, deleted, freed, locked);
    }

    private IEnumerable<CleanupFile> Candidates(CleanupCategory category)
    {
        var limit = _utcNow() - category.MinAge;
        var root = Path.GetFullPath(category.Root).TrimEnd('\\') + "\\";
        return _files.EnumerateFiles(category.Root, category.SearchPattern)
            .Where(f => f.LastWriteUtc <= limit)
            // Rede de segurança extra: nada fora da pasta escolhida entra na lista, mesmo que o disco devolva.
            .Where(f => Path.GetFullPath(f.Path).StartsWith(root, StringComparison.OrdinalIgnoreCase));
    }
}
