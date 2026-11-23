using System.Diagnostics;

namespace PimpMyWindows.Core.Benchmarks;

public enum StressVerdict
{
    Stable,
    Throttled,
    MemoryErrors,
    Cancelled,
}

public sealed record StressResult(
    TimeSpan Duration,
    IReadOnlyList<double> OperationsPerSecond,
    double DropPercent,
    long MemoryErrors,
    StressVerdict Verdict)
{
    public string Text => Verdict switch
    {
        StressVerdict.MemoryErrors =>
            $"Foram encontrados {MemoryErrors} erros ao conferir a memória. Isso indica problema de hardware (pente de memória, " +
            "overclock ou XMP instável). Teste cada pente separadamente e volte a memória à configuração padrão da placa-mãe.",
        StressVerdict.Throttled =>
            $"O desempenho caiu {DropPercent:0}% do começo para o fim do teste. É o sinal típico de aquecimento: o processador " +
            "reduz a velocidade para se proteger. Limpe a poeira, confira o cooler e a pasta térmica e, em notebook, o plano de energia.",
        StressVerdict.Cancelled => "Teste interrompido antes do fim: não dá para concluir nada com esse resultado.",
        _ => $"Desempenho estável (variação de {DropPercent:0}%) e memória sem erros. O computador aguentou a carga.",
    };
}

/// <summary>Regras que transformam as medições do teste de estresse em um veredito.</summary>
public static class StressAnalysis
{
    public const double ThrottleThresholdPercent = 15;

    public static StressResult Evaluate(
        TimeSpan duration,
        IReadOnlyList<double> perSecond,
        long memoryErrors,
        bool completed)
    {
        var drop = DropPercent(perSecond);
        var verdict = !completed ? StressVerdict.Cancelled
            : memoryErrors > 0 ? StressVerdict.MemoryErrors
            : drop >= ThrottleThresholdPercent ? StressVerdict.Throttled
            : StressVerdict.Stable;
        return new StressResult(duration, perSecond, drop, memoryErrors, verdict);
    }

    /// <summary>
    /// Queda do desempenho do começo para o fim. Ignora os 2 primeiros segundos (o teste ainda está esquentando)
    /// e compara o início restante com os últimos 20% do teste. Com poucas amostras, devolve 0.
    /// </summary>
    public static double DropPercent(IReadOnlyList<double> perSecond)
    {
        var samples = perSecond.Skip(2).ToList();
        if (samples.Count < 5)
        {
            return 0;
        }

        var window = Math.Max(2, samples.Count / 5);
        var start = samples.Take(window).Average();
        var end = samples.Skip(samples.Count - window).Average();
        return start <= 0 ? 0 : Math.Max(0, (start - end) / start * 100);
    }
}

/// <summary>
/// Teste de estresse: todos os núcleos trabalhando ao mesmo tempo e um bloco de memória sendo escrito e conferido
/// sem parar, por um tempo limitado. Não grava nada no disco nem altera o sistema.
/// </summary>
public static class StressTest
{
    private static long _sink;

    /// <param name="progress">Recebe o segundo atual e as operações por segundo do último segundo.</param>
    public static StressResult Run(
        TimeSpan duration,
        int memoryBytes,
        Action<int, double>? progress,
        CancellationToken cancellationToken)
    {
        var threads = Environment.ProcessorCount;
        var seconds = (int)Math.Ceiling(duration.TotalSeconds);
        var buckets = new long[seconds + 1];
        long memoryErrors = 0;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var watch = Stopwatch.StartNew();

        var workers = new List<Task>();
        for (var t = 0; t < threads; t++)
        {
            workers.Add(Task.Factory.StartNew(
                () => CpuWorker(watch, buckets, stop.Token),
                TaskCreationOptions.LongRunning));
        }

        workers.Add(Task.Factory.StartNew(
            () => Interlocked.Add(ref memoryErrors, MemoryWorker(memoryBytes, stop.Token)),
            TaskCreationOptions.LongRunning));

        var reported = 0;
        try
        {
            while (watch.Elapsed < duration)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Thread.Sleep(200);
                var done = Math.Min((int)watch.Elapsed.TotalSeconds, seconds);
                while (reported < done)
                {
                    progress?.Invoke(reported + 1, Interlocked.Read(ref buckets[reported]));
                    reported++;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Segue para a parada ordenada abaixo.
        }

        var completed = watch.Elapsed >= duration && !cancellationToken.IsCancellationRequested;
        stop.Cancel();
        try
        {
            Task.WaitAll(workers.ToArray());
        }
        catch (AggregateException ex) when (ex.InnerExceptions.All(e => e is OperationCanceledException))
        {
            // Esperado: os trabalhadores terminam por cancelamento.
        }

        var perSecond = Enumerable.Range(0, Math.Min(reported, seconds))
            .Select(i => (double)Interlocked.Read(ref buckets[i]))
            .ToList();
        return StressAnalysis.Evaluate(duration, perSecond, Interlocked.Read(ref memoryErrors), completed);
    }

    private static void CpuWorker(Stopwatch watch, long[] buckets, CancellationToken token)
    {
        ulong x = 88172645463325252UL ^ (ulong)Environment.CurrentManagedThreadId;
        var acc = 1.0;
        while (!token.IsCancellationRequested)
        {
            for (var i = 0; i < 50_000; i++)
            {
                x ^= x << 13;
                x ^= x >> 7;
                x ^= x << 17;
                acc = (acc * 1.0000001) + ((x & 0xFF) * 1e-9);
            }

            var second = (int)watch.Elapsed.TotalSeconds;
            if (second < buckets.Length)
            {
                Interlocked.Add(ref buckets[second], 50_000);
            }
        }

        Interlocked.Exchange(ref _sink, (long)(acc + x));
    }

    /// <summary>Escreve um padrão no bloco inteiro e confere. Retorna quantas posições vieram diferentes.</summary>
    private static long MemoryWorker(int bytes, CancellationToken token)
    {
        var words = Math.Max(1024, bytes / sizeof(ulong));
        var buffer = new ulong[words];
        long errors = 0;
        ulong seed = 0x9E3779B97F4A7C15UL;

        while (!token.IsCancellationRequested)
        {
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = ((ulong)i * 0xBF58476D1CE4E5B9UL) ^ seed;
            }

            for (var i = 0; i < buffer.Length; i++)
            {
                if (buffer[i] != (((ulong)i * 0xBF58476D1CE4E5B9UL) ^ seed))
                {
                    errors++;
                }
            }

            // Padrão diferente a cada volta, para pegar bits "presos" em 0 ou em 1.
            seed = ~seed + 0x632BE59BD9B4E019UL;
        }

        return errors;
    }
}
