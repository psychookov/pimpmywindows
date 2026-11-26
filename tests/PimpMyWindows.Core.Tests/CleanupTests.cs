using PimpMyWindows.Core.Cleanup;
using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Tests;

public class CleanupTests
{
    private static readonly DateTime Now = new(2026, 11, 25, 12, 0, 0, DateTimeKind.Utc);

    private sealed class FakeDisk : ICleanupFileSystem
    {
        public List<CleanupFile> Files { get; } = [];

        public HashSet<string> Locked { get; } = new(StringComparer.OrdinalIgnoreCase);

        public List<string> Deleted { get; } = [];

        public int EmptyDirCalls { get; private set; }

        public bool DirectoryExists(string path) => Files.Any(f => f.Path.StartsWith(path, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<CleanupFile> EnumerateFiles(string root, string searchPattern) => Files;

        public bool TryDeleteFile(string path)
        {
            if (Locked.Contains(path))
            {
                return false;
            }

            Deleted.Add(path);
            return true;
        }

        public void DeleteEmptyDirectories(string root) => EmptyDirCalls++;
    }

    private static CleanupCategory Temp(bool admin = false) =>
        new("temp", "Temp", "d", @"C:\Users\x\Temp", "*", admin, TimeSpan.FromDays(1), true);

    private static CleanupFile File(string name, long size, double ageDays, string root = @"C:\Users\x\Temp") =>
        new(Path.Combine(root, name), size, Now.AddDays(-ageDays));

    private static CleanupService Service(FakeDisk disk, bool admin = false) =>
        new(disk, new NullActionLog(), () => admin, () => Now);

    [Fact]
    public void Scan_CountsOnlyOldFiles()
    {
        var disk = new FakeDisk();
        disk.Files.Add(File("velho.tmp", 1000, 5));
        disk.Files.Add(File("novo.tmp", 5000, 0.2));
        var item = Service(disk).Scan(Temp());
        Assert.Equal(1, item.FileCount);
        Assert.Equal(1000, item.TotalBytes);
    }

    [Fact]
    public void Clean_DeletesOldFilesOnly_AndReportsLockedOnes()
    {
        var disk = new FakeDisk();
        disk.Files.Add(File("a.tmp", 100, 3));
        disk.Files.Add(File("b.tmp", 200, 3));
        disk.Files.Add(File("novo.tmp", 999, 0.1));
        disk.Locked.Add(Path.Combine(@"C:\Users\x\Temp", "b.tmp"));

        var result = Service(disk).Clean(Temp());

        Assert.Equal(1, result.DeletedFiles);
        Assert.Equal(100, result.FreedBytes);
        Assert.Equal(1, result.LockedFiles);
        Assert.Equal(new[] { Path.Combine(@"C:\Users\x\Temp", "a.tmp") }, disk.Deleted.ToArray());
        Assert.Equal(1, disk.EmptyDirCalls);
    }

    [Fact]
    public void Clean_NeverTouchesFilesOutsideTheRoot()
    {
        var disk = new FakeDisk();
        disk.Files.Add(File("ok.tmp", 10, 3));
        disk.Files.Add(File("importante.docx", 10, 3, @"C:\Users\x\Documents"));
        disk.Files.Add(new CleanupFile(@"C:\Users\x\Temp\..\Documents\fuga.docx", 10, Now.AddDays(-9)));
        disk.Files.Add(new CleanupFile(@"C:\Users\x\TempOutra\arquivo.tmp", 10, Now.AddDays(-9)));

        var result = Service(disk).Clean(Temp());

        Assert.Equal(1, result.DeletedFiles);
        Assert.DoesNotContain(disk.Deleted, d => d.Contains("Documents") || d.Contains("TempOutra"));
    }

    [Fact]
    public void AdminCategory_IsSkippedWithoutAdmin_AndNeverDeletes()
    {
        var disk = new FakeDisk();
        disk.Files.Add(File("a.tmp", 100, 3));

        var item = Service(disk, admin: false).Scan(Temp(admin: true));
        Assert.True(item.Skipped);
        Assert.NotNull(item.SkipReason);

        Assert.Equal(0, Service(disk, admin: false).Clean(Temp(admin: true)).DeletedFiles);
        Assert.Empty(disk.Deleted);

        Assert.Equal(1, Service(disk, admin: true).Clean(Temp(admin: true)).DeletedFiles);
    }

    [Fact]
    public void MissingFolder_ScansAsEmpty()
    {
        var item = Service(new FakeDisk()).Scan(Temp());
        Assert.Equal(0, item.FileCount);
        Assert.False(item.Skipped);
    }

    [Fact]
    public void Categories_UseWindowsFoldersAndAreSane()
    {
        var categories = CleanupService.BuildCategories(@"C:\Users\x\AppData\Local", @"C:\Windows");
        Assert.Equal(categories.Count, categories.Select(c => c.Id).Distinct().Count());
        Assert.All(categories, c => Assert.StartsWith(@"C:\", c.Root));
        // Nunca limpar a raiz de um disco nem a pasta inteira do Windows/usuário.
        Assert.DoesNotContain(categories, c => c.Root is @"C:\" or @"C:\Windows" or @"C:\Users\x\AppData\Local");
        Assert.Contains(categories, c => c.Id == "temp.windows" && c.RequiresAdmin);
        Assert.All(categories.Where(c => c.Id.StartsWith("temp")), c => Assert.True(c.MinAge >= TimeSpan.FromDays(1)));
    }

    [Theory]
    [InlineData(500, "500 bytes")]
    [InlineData(2048, "2 KB")]
    [InlineData(5 * 1024 * 1024, "5,0 MB")]
    public void FormatBytes_IsReadable(long bytes, string expected)
    {
        var text = CleanupScanItem.FormatBytes(bytes);
        Assert.Equal(expected.Replace(',', '.'), text.Replace(',', '.'));
    }
}
