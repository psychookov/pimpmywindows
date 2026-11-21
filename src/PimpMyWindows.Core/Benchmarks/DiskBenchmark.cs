using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PimpMyWindows.Core.Benchmarks;

/// <summary>
/// Teste de disco. Cria UM arquivo temporário novo (nunca mexe em arquivos seus), mede e apaga no fim.
/// Usa acesso sem cache do Windows, senão o teste mediria a memória e não o disco.
/// Só lê de forma aleatória (não escreve aleatoriamente), para não gastar SSD à toa.
/// </summary>
public static class DiskBenchmark
{
    private const int Sector = 4096;
    private const int Block = 1024 * 1024;
    private const int NoBuffering = 0x20000000;

    public static OperationResultWith<IReadOnlyList<BenchmarkResult>> Run(
        long fileSizeBytes,
        TimeSpan randomReadDuration,
        CancellationToken cancellationToken,
        string? baseDirectory = null)
    {
        var size = fileSizeBytes / Block * Block;
        if (size < Block)
        {
            return OperationResultWith<IReadOnlyList<BenchmarkResult>>.Fail("Tamanho de teste inválido.");
        }

        var directory = Path.Combine(baseDirectory ?? Path.GetTempPath(), "PimpMyWindows-bench");
        string? path = null;

        try
        {
            Directory.CreateDirectory(directory);

            var root = Path.GetPathRoot(Path.GetFullPath(directory));
            if (root is not null && new DriveInfo(root).AvailableFreeSpace < size + (512L * 1024 * 1024))
            {
                return OperationResultWith<IReadOnlyList<BenchmarkResult>>.Fail(
                    "Não há espaço livre suficiente para o teste de disco. Libere espaço e tente de novo.");
            }

            path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            using var handle = File.OpenHandle(
                path,
                FileMode.CreateNew,
                FileAccess.ReadWrite,
                FileShare.None,
                FileOptions.WriteThrough | (FileOptions)NoBuffering);

            var buffer = AlignedBuffer(Block);
            new Random(7).NextBytes(buffer.Span);

            var watch = Stopwatch.StartNew();
            for (long offset = 0; offset < size; offset += Block)
            {
                RandomAccess.Write(handle, buffer.Span, offset);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var writeSpeed = size / 1e6 / watch.Elapsed.TotalSeconds;

            watch.Restart();
            for (long offset = 0; offset < size; offset += Block)
            {
                RandomAccess.Read(handle, buffer.Span, offset);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var readSpeed = size / 1e6 / watch.Elapsed.TotalSeconds;

            var small = AlignedBuffer(Sector);
            var slots = size / Sector;
            var random = new Random(99);
            long reads = 0;
            watch.Restart();
            while (watch.Elapsed < randomReadDuration)
            {
                RandomAccess.Read(handle, small.Span, random.NextInt64(slots) * Sector);
                reads++;
                cancellationToken.ThrowIfCancellationRequested();
            }

            var seconds = watch.Elapsed.TotalSeconds;
            var iops = reads / seconds;
            var latencyUs = seconds * 1e6 / Math.Max(reads, 1);

            return OperationResultWith<IReadOnlyList<BenchmarkResult>>.Ok(
            [
                new BenchmarkResult("disk.seq.write", "Disco: gravação sequencial", writeSpeed, "MB/s", true),
                new BenchmarkResult("disk.seq.read", "Disco: leitura sequencial", readSpeed, "MB/s", true),
                new BenchmarkResult("disk.rand.read", "Disco: leitura aleatória 4K", iops, "operações/s", true),
                new BenchmarkResult("disk.rand.latency", "Disco: tempo de resposta 4K", latencyUs, "µs", false),
            ]);
        }
        catch (Exception ex) when (ExpectedFailures.Is(ex))
        {
            return OperationResultWith<IReadOnlyList<BenchmarkResult>>.Fail(
                $"Não foi possível testar o disco. Detalhe: {ex.Message}");
        }
        finally
        {
            if (path is not null)
            {
                try
                {
                    File.Delete(path);
                    if (!Directory.EnumerateFileSystemEntries(directory).Any())
                    {
                        Directory.Delete(directory);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Sobra de arquivo temporário: não é crítico, fica na pasta temporária do Windows.
                }
            }
        }
    }

    /// <summary>Memória alinhada ao setor do disco, exigência do acesso sem cache.</summary>
    private static Memory<byte> AlignedBuffer(int size)
    {
        var array = GC.AllocateUninitializedArray<byte>(size + Sector, pinned: true);
        var address = (long)Marshal.UnsafeAddrOfPinnedArrayElement(array, 0);
        var shift = (int)((Sector - (address % Sector)) % Sector);
        return array.AsMemory(shift, size);
    }
}

/// <summary>Resultado de uma operação que devolve dados, ou uma mensagem de falha em português.</summary>
public sealed record OperationResultWith<T>(bool Success, T? Value, string Message)
{
    public static OperationResultWith<T> Ok(T value) => new(true, value, string.Empty);

    public static OperationResultWith<T> Fail(string message) => new(false, default, message);
}
