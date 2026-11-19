namespace PimpMyWindows.Core.Safety;

/// <summary>
/// Protege uma pasta de backups: confere se ela é confiável antes de ler e prepara/restringe antes de gravar.
/// Lança <see cref="UnauthorizedAccessException"/> quando a pasta não é confiável.
/// </summary>
public interface IDirectoryGuard
{
    void BeforeRead(string directory);

    void BeforeWrite(string directory);
}

public sealed class NullDirectoryGuard : IDirectoryGuard
{
    public void BeforeRead(string directory)
    {
    }

    public void BeforeWrite(string directory)
    {
    }
}
