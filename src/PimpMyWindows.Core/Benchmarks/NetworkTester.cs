using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace PimpMyWindows.Core.Benchmarks;

public sealed record NetworkStats(
    int Sent,
    int Received,
    double? AverageMs,
    double? MinMs,
    double? MaxMs,
    double? JitterMs,
    double LossPercent)
{
    /// <summary>Calcula as estatísticas a partir dos tempos de resposta (nulo = sem resposta).</summary>
    public static NetworkStats Compute(IReadOnlyList<long?> roundTrips)
    {
        var ok = roundTrips.Where(r => r is not null).Select(r => (double)r!.Value).ToList();
        var loss = roundTrips.Count == 0 ? 0 : (roundTrips.Count - ok.Count) * 100.0 / roundTrips.Count;
        if (ok.Count == 0)
        {
            return new NetworkStats(roundTrips.Count, 0, null, null, null, null, loss);
        }

        // Jitter: quanto o tempo varia de uma resposta para a seguinte.
        double? jitter = ok.Count < 2
            ? null
            : ok.Zip(ok.Skip(1), (a, b) => Math.Abs(b - a)).Average();

        return new NetworkStats(roundTrips.Count, ok.Count, ok.Average(), ok.Min(), ok.Max(), jitter, loss);
    }
}

public sealed record NetworkTarget(string Name, NetworkStats Stats);

public sealed record DnsTiming(string Host, long? Milliseconds);

public sealed record NetworkReport(
    NetworkStats? Gateway,
    IReadOnlyList<NetworkTarget> Targets,
    IReadOnlyList<DnsTiming> Dns);

public interface INetworkProbe
{
    IPAddress? DefaultGateway();

    /// <summary>Tempo de resposta em ms, ou nulo se não respondeu.</summary>
    Task<long?> PingAsync(IPAddress address, int timeoutMs, CancellationToken cancellationToken);

    /// <summary>Tempo para resolver o nome em ms, ou nulo se falhou.</summary>
    Task<long?> ResolveAsync(string host, CancellationToken cancellationToken);
}

/// <summary>
/// Teste de rede: latência, variação e perda até o roteador e até dois servidores públicos de DNS,
/// mais o tempo de resolução de alguns nomes. Só é executado quando o usuário clica; nenhum dado seu é enviado.
/// </summary>
public sealed class NetworkTester
{
    public const int Samples = 15;
    private static readonly (string Name, IPAddress Address)[] PublicTargets =
    [
        ("Cloudflare (1.1.1.1)", IPAddress.Parse("1.1.1.1")),
        ("Google (8.8.8.8)", IPAddress.Parse("8.8.8.8")),
    ];

    private static readonly string[] DnsHosts = ["www.microsoft.com", "www.google.com", "www.wikipedia.org"];

    private readonly INetworkProbe _probe;

    public NetworkTester(INetworkProbe probe)
    {
        _probe = probe;
    }

    public async Task<NetworkReport> RunAsync(IProgress<string>? progress, CancellationToken cancellationToken)
    {
        NetworkStats? gateway = null;
        var gatewayAddress = _probe.DefaultGateway();
        if (gatewayAddress is not null)
        {
            progress?.Report("Testando o seu roteador…");
            gateway = await PingManyAsync(gatewayAddress, cancellationToken);
        }

        var targets = new List<NetworkTarget>();
        foreach (var (name, address) in PublicTargets)
        {
            progress?.Report($"Testando {name}…");
            targets.Add(new NetworkTarget(name, await PingManyAsync(address, cancellationToken)));
        }

        var dns = new List<DnsTiming>();
        foreach (var host in DnsHosts)
        {
            progress?.Report($"Resolvendo {host}…");
            dns.Add(new DnsTiming(host, await _probe.ResolveAsync(host, cancellationToken)));
        }

        return new NetworkReport(gateway, targets, dns);
    }

    private async Task<NetworkStats> PingManyAsync(IPAddress address, CancellationToken cancellationToken)
    {
        var times = new List<long?>();
        for (var i = 0; i < Samples; i++)
        {
            times.Add(await _probe.PingAsync(address, 2000, cancellationToken));
            await Task.Delay(150, cancellationToken);
        }

        return NetworkStats.Compute(times);
    }
}

public sealed class WindowsNetworkProbe : INetworkProbe
{
    public IPAddress? DefaultGateway() =>
        NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .SelectMany(n => n.GetIPProperties().GatewayAddresses)
            .Select(g => g.Address)
            .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !a.Equals(IPAddress.Any));

    public async Task<long?> PingAsync(IPAddress address, int timeoutMs, CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(address, timeoutMs);
            return reply.Status == IPStatus.Success ? reply.RoundtripTime : null;
        }
        catch (Exception ex) when (ex is PingException or SocketException or InvalidOperationException)
        {
            return null;
        }
    }

    public async Task<long?> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await Dns.GetHostAddressesAsync(host, cancellationToken);
            return watch.ElapsedMilliseconds;
        }
        catch (SocketException)
        {
            return null;
        }
    }
}
