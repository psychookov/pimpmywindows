using System.Diagnostics;

namespace PimpMyWindows.Core.Benchmarks;

/// <summary>
/// Testes de processador e memória. Cada teste dura um tempo fixo (não uma quantidade fixa de trabalho),
/// então o tempo de espera é previsível em qualquer computador. Servem para comparar o mesmo PC antes e depois.
/// </summary>
public static class CpuMemoryBenchmark
{
    private static double _sink;

    public static IReadOnlyList<BenchmarkResult> RunCpu(TimeSpan duration, CancellationToken cancellationToken)
    {
        var single = Work(duration, cancellationToken);

        var threads = Environment.ProcessorCount;
        var counts = new long[threads];
        Parallel.For(
            0,
            threads,
            new ParallelOptions { MaxDegreeOfParallelism = threads, CancellationToken = cancellationToken },
            i => counts[i] = Work(duration, cancellationToken).Operations);

        return
        [
            new BenchmarkResult("cpu.single", "Processador: um núcleo", single.PerSecond / 1e6, "milhões de operações/s", true),
            new BenchmarkResult("cpu.multi", $"Processador: todos os núcleos ({threads} threads)",
                counts.Sum() / duration.TotalSeconds / 1e6, "milhões de operações/s", true),
        ];
    }

    private static (long Operations, double PerSecond) Work(TimeSpan duration, CancellationToken cancellationToken)
    {
        var watch = Stopwatch.StartNew();
        ulong x = 88172645463325252UL;
        var acc = 1.0;
        long operations = 0;

        while (true)
        {
            for (var i = 0; i < 100_000; i++)
            {
                x ^= x << 13;
                x ^= x >> 7;
                x ^= x << 17;
                acc = (acc * 1.0000001) + ((x & 0xFF) * 1e-9);
            }

            operations += 100_000;
            cancellationToken.ThrowIfCancellationRequested();
            if (watch.Elapsed >= duration)
            {
                break;
            }
        }

        _sink = acc + x;
        return (operations, operations / watch.Elapsed.TotalSeconds);
    }

    /// <param name="bufferBytes">Tamanho de cada um dos dois blocos copiados (padrão 128 MB).</param>
    public static IReadOnlyList<BenchmarkResult> RunMemory(
        TimeSpan duration,
        CancellationToken cancellationToken,
        int bufferBytes = 128 * 1024 * 1024)
    {
        var source = new byte[bufferBytes];
        var target = new byte[bufferBytes];
        new Random(1).NextBytes(source);

        long copied = 0;
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < duration)
        {
            Buffer.BlockCopy(source, 0, target, 0, bufferBytes);
            copied += bufferBytes;
            cancellationToken.ThrowIfCancellationRequested();
        }

        var bandwidth = copied / watch.Elapsed.TotalSeconds / 1e9;

        return
        [
            new BenchmarkResult("mem.copy", "Memória: velocidade de cópia", bandwidth, "GB/s", true),
            new BenchmarkResult("mem.latency", "Memória: tempo de acesso", Latency(cancellationToken), "ns", false),
        ];
    }

    /// <summary>Segue uma cadeia aleatória de posições em 64 MB: cada passo depende do anterior, então mede a espera pela memória.</summary>
    private static double Latency(CancellationToken cancellationToken)
    {
        const int count = 16 * 1024 * 1024;
        const int steps = 4_000_000;
        var next = new int[count];
        for (var i = 0; i < count; i++)
        {
            next[i] = i;
        }

        // Sattolo: embaralha formando um único ciclo, com semente fixa para o teste ser repetível.
        var random = new Random(12345);
        for (var i = count - 1; i > 0; i--)
        {
            var j = random.Next(i);
            (next[i], next[j]) = (next[j], next[i]);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var position = 0;
        var watch = Stopwatch.StartNew();
        for (var i = 0; i < steps; i++)
        {
            position = next[position];
        }

        watch.Stop();
        _sink = position;
        return watch.Elapsed.TotalMilliseconds * 1e6 / steps;
    }
}
