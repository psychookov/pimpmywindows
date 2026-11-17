namespace PimpMyWindows.Core.Startup;

/// <summary>Um atalho/arquivo que está em uma pasta Inicializar.</summary>
public sealed record StartupFile(string FileName, string Target, string Description);

/// <summary>Acesso ao disco usado pela lista de inicialização (separado para poder ser testado).</summary>
public interface IStartupFileSystem
{
    /// <summary>Itens da pasta Inicializar do usuário atual (<c>AllUsers</c> = de todos os usuários).</summary>
    IReadOnlyList<StartupFile> ListFolder(bool allUsers);

    bool FileExists(string path);

    string ExpandVariables(string text);

    /// <summary>Descrição do programa (propriedade do arquivo), ou vazio.</summary>
    string Describe(string path);
}

public static class StartupCommand
{
    /// <summary>Extrai o caminho do programa de um comando como <c>"C:\A B\app.exe" --silent</c>.</summary>
    public static string ExtractExecutable(string command)
    {
        var text = command.Trim();
        if (text.Length == 0)
        {
            return string.Empty;
        }

        if (text[0] == '"')
        {
            var end = text.IndexOf('"', 1);
            return end > 0 ? text[1..end] : text[1..];
        }

        var exe = text.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exe >= 0)
        {
            return text[..(exe + 4)];
        }

        var space = text.IndexOf(' ');
        return space > 0 ? text[..space] : text;
    }
}
