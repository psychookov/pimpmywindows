using System.Security.Cryptography;
using System.Text;

namespace PimpMyWindows.Core.Games;

/// <summary>Um jogo conhecido: de onde veio, onde está instalado e qual executável ele usa.</summary>
public sealed record GameInfo(string Id, string Name, string Launcher, string InstallDirectory, string ExecutablePath)
{
    /// <summary>O identificador sai do caminho do executável: o mesmo jogo sempre recebe o mesmo id.</summary>
    public static GameInfo Create(string name, string launcher, string installDirectory, string executablePath) =>
        new(IdFor(executablePath), name, launcher, installDirectory, executablePath);

    public static string IdFor(string executablePath)
    {
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(executablePath.Trim().ToLowerInvariant()));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }
}

/// <summary>Um arquivo executável encontrado na pasta de um jogo.</summary>
public sealed record ExecutableFile(string Path, long SizeBytes);

/// <summary>Acesso ao disco usado pela detecção (separado para ser testável sem jogos de verdade).</summary>
public interface IGameFileSystem
{
    bool FileExists(string path);

    bool DirectoryExists(string path);

    /// <summary>Texto do arquivo, ou <c>null</c> se não existir ou não puder ser lido.</summary>
    string? ReadAllText(string path);

    /// <summary>Arquivos de uma pasta (só o primeiro nível) que combinam com o padrão, ex.: <c>*.acf</c>.</summary>
    IReadOnlyList<string> ListFiles(string directory, string pattern);

    /// <summary>Executáveis da pasta e de até duas subpastas abaixo dela.</summary>
    IReadOnlyList<ExecutableFile> ListExecutables(string directory);
}
