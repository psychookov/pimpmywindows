namespace PimpMyWindows.Core.Cleanup;

/// <summary>Um arquivo candidato à limpeza.</summary>
public sealed record CleanupFile(string Path, long SizeBytes, DateTime LastWriteUtc);

/// <summary>Acesso ao disco usado pela limpeza (separado para testar sem apagar nada de verdade).</summary>
public interface ICleanupFileSystem
{
    bool DirectoryExists(string path);

    /// <summary>
    /// Arquivos dentro da pasta, em todos os níveis. Não entra em atalhos de pasta (junções e links simbólicos)
    /// nem lista os próprios atalhos, para a limpeza nunca sair da pasta escolhida.
    /// </summary>
    IEnumerable<CleanupFile> EnumerateFiles(string root, string searchPattern);

    /// <summary>Apaga um arquivo. Retorna falso se estiver em uso, protegido ou já sumiu: isso é normal e não é erro.</summary>
    bool TryDeleteFile(string path);

    /// <summary>Remove pastas que ficaram vazias dentro da raiz (nunca a própria raiz).</summary>
    void DeleteEmptyDirectories(string root);
}

/// <summary>Um tipo de lixo que o programa sabe limpar.</summary>
public sealed record CleanupCategory(
    string Id,
    string Name,
    string Description,
    string Root,
    string SearchPattern,
    bool RequiresAdmin,
    TimeSpan MinAge,
    bool Recommended);

public sealed record CleanupScanItem(CleanupCategory Category, int FileCount, long TotalBytes, bool Skipped, string? SkipReason)
{
    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1L << 30 => $"{bytes / (double)(1L << 30):0.0} GB",
        >= 1L << 20 => $"{bytes / (double)(1L << 20):0.0} MB",
        >= 1L << 10 => $"{bytes / (double)(1L << 10):0} KB",
        _ => $"{bytes} bytes",
    };

    public string SizeText => FormatBytes(TotalBytes);
}

public sealed record CleanupResult(CleanupCategory Category, int DeletedFiles, long FreedBytes, int LockedFiles);
