namespace PimpMyWindows.Core.Cleanup;

public sealed class WindowsCleanupFileSystem : ICleanupFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public IEnumerable<CleanupFile> EnumerateFiles(string root, string searchPattern)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            // Pula atalhos de pasta (junções e links) para nunca sair da raiz.
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        IEnumerator<FileInfo>? enumerator = null;
        try
        {
            enumerator = new DirectoryInfo(root).EnumerateFiles(searchPattern, options).GetEnumerator();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            enumerator = null;
        }

        if (enumerator is null)
        {
            yield break;
        }

        using (enumerator)
        {
            while (true)
            {
                CleanupFile? current = null;
                var more = true;
                try
                {
                    more = enumerator.MoveNext();
                    if (more)
                    {
                        var file = enumerator.Current;
                        current = new CleanupFile(file.FullName, file.Length, file.LastWriteTimeUtc);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Se a leitura falhar no meio, encerra a lista aqui em vez de arriscar.
                    more = false;
                }

                if (!more)
                {
                    yield break;
                }

                if (current is not null)
                {
                    yield return current;
                }
            }
        }
    }

    public bool TryDeleteFile(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                return false;
            }

            info.Delete();
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void DeleteEmptyDirectories(string root)
    {
        try
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.ReparsePoint,
            };
            var directories = new DirectoryInfo(root).EnumerateDirectories("*", options)
                .OrderByDescending(d => d.FullName.Length)
                .ToList();
            foreach (var directory in directories)
            {
                try
                {
                    if (!directory.EnumerateFileSystemInfos().Any())
                    {
                        directory.Delete();
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Pasta em uso: deixa onde está.
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Raiz inacessível: nada a fazer.
        }
    }
}
