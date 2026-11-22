namespace PimpMyWindows.Core.Games;

public sealed class WindowsGameFileSystem : IGameFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public string? ReadAllText(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public IReadOnlyList<string> ListFiles(string directory, string pattern)
    {
        try
        {
            return Directory.Exists(directory) ? Directory.GetFiles(directory, pattern) : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public IReadOnlyList<ExecutableFile> ListExecutables(string directory)
    {
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = 2,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
        };

        try
        {
            return new DirectoryInfo(directory)
                .EnumerateFiles("*.exe", options)
                .Select(f => new ExecutableFile(f.FullName, f.Length))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
