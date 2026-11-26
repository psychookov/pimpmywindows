using System.Text.Json;

namespace PimpMyWindows.Core.Updates;

/// <summary>Busca um texto na internet. Existe separado para o teste não precisar de rede.</summary>
public interface IHttpFetcher
{
    Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken);
}

public enum UpdateStatus
{
    UpToDate,
    UpdateAvailable,
    Failed,
}

public sealed record UpdateResult(UpdateStatus Status, string Message, string? LatestVersion = null, string? PageUrl = null);

/// <summary>Versão no formato 1.2.3 ou 1.2.3-beta, com a regra: versão final é maior que a pré-versão do mesmo número.</summary>
public sealed record AppVersion(Version Number, string? PreRelease) : IComparable<AppVersion>
{
    public static AppVersion? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var value = text.Trim().TrimStart('v', 'V');
        var plus = value.IndexOf('+', StringComparison.Ordinal);
        if (plus >= 0)
        {
            value = value[..plus];
        }

        string? pre = null;
        var dash = value.IndexOf('-', StringComparison.Ordinal);
        if (dash >= 0)
        {
            pre = value[(dash + 1)..];
            value = value[..dash];
        }

        var parts = value.Split('.');
        if (parts.Length is < 2 or > 3 || !Version.TryParse(value, out var version))
        {
            return null;
        }

        return new AppVersion(version, string.IsNullOrEmpty(pre) ? null : pre);
    }

    public int CompareTo(AppVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        var byNumber = Number.CompareTo(other.Number);
        if (byNumber != 0)
        {
            return byNumber;
        }

        return (PreRelease, other.PreRelease) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            _ => string.CompareOrdinal(PreRelease, other.PreRelease),
        };
    }

    public override string ToString() => Number.ToString(3) + (PreRelease is null ? string.Empty : "-" + PreRelease);
}

/// <summary>
/// Pergunta ao GitHub se existe uma versão mais nova. Só roda quando a pessoa clica, faz uma única consulta de leitura
/// e não envia nada além do cabeçalho padrão. Nunca baixa nem executa nada: no máximo mostra o link da página da versão.
/// </summary>
public sealed class UpdateChecker
{
    public const string RepositoryPage = "https://github.com/psychookov/pimpmywindows";
    public static readonly Uri ReleasesApi = new("https://api.github.com/repos/psychookov/pimpmywindows/releases?per_page=10");

    private readonly IHttpFetcher _http;
    private readonly AppVersion _current;

    public UpdateChecker(IHttpFetcher http, AppVersion current)
    {
        _http = http;
        _current = current;
    }

    public async Task<UpdateResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        string json;
        try
        {
            json = await _http.GetStringAsync(ReleasesApi, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException or InvalidOperationException)
        {
            return new UpdateResult(UpdateStatus.Failed, "Não foi possível consultar o GitHub agora. Verifique a conexão e tente de novo.");
        }

        return Evaluate(json, _current);
    }

    public static UpdateResult Evaluate(string json, AppVersion current)
    {
        AppVersion? best = null;
        string? bestUrl = null;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                return Unreadable();
            }

            foreach (var release in document.RootElement.EnumerateArray())
            {
                if (release.ValueKind != JsonValueKind.Object || Flag(release, "draft"))
                {
                    continue;
                }

                var version = AppVersion.TryParse(Text(release, "tag_name"));
                var url = Text(release, "html_url");
                if (version is null || !IsRepositoryUrl(url))
                {
                    continue;
                }

                if (best is null || version.CompareTo(best) > 0)
                {
                    best = version;
                    bestUrl = url;
                }
            }
        }
        catch (JsonException)
        {
            return Unreadable();
        }

        if (best is null)
        {
            return new UpdateResult(UpdateStatus.UpToDate, "Ainda não há versões publicadas além da sua.");
        }

        return best.CompareTo(current) > 0
            ? new UpdateResult(UpdateStatus.UpdateAvailable, $"Há uma versão nova: {best}. A sua é a {current}.", best.ToString(), bestUrl)
            : new UpdateResult(UpdateStatus.UpToDate, $"Você já está na versão mais recente ({current}).", best.ToString());
    }

    /// <summary>O link só é aceito se for uma página deste repositório, para nunca abrir um endereço qualquer.</summary>
    public static bool IsRepositoryUrl(string? url) =>
        url is not null && url.StartsWith(RepositoryPage + "/", StringComparison.Ordinal);

    private static UpdateResult Unreadable() =>
        new(UpdateStatus.Failed, "O GitHub respondeu algo que o programa não entendeu. Tente de novo mais tarde.");

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool Flag(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}

public sealed class GitHubHttpFetcher : IHttpFetcher
{
    private static readonly HttpClient Client = Create();

    public async Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken)
    {
        // Só fala com a API do GitHub, por https, mesmo que alguém altere o endereço no código chamador.
        if (uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "api.github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Endereço não permitido.");
        }

        return await Client.GetStringAsync(uri, cancellationToken);
    }

    private static HttpClient Create()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("PimpMyWindows-update-check");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
