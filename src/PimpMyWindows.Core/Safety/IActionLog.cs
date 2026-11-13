namespace PimpMyWindows.Core.Safety;

/// <summary>
/// Registro de tudo o que o programa altera, para que o usuário possa auditar.
/// </summary>
public interface IActionLog
{
    void Write(string message);
}

public sealed class NullActionLog : IActionLog
{
    public void Write(string message)
    {
    }
}

/// <summary>Grava um arquivo de texto simples, uma linha por ação.</summary>
public sealed class FileActionLog : IActionLog
{
    private readonly string _path;
    private readonly object _lock = new();

    public FileActionLog(string path)
    {
        _path = path;
    }

    public void Write(string message)
    {
        try
        {
            lock (_lock)
            {
                var directory = Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.AppendAllText(_path, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}  {message}{Environment.NewLine}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Falhar ao gravar o log nunca deve impedir o usuário de usar o programa.
        }
    }
}
