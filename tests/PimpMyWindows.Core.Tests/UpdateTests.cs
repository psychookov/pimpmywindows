using PimpMyWindows.Core.Updates;

namespace PimpMyWindows.Core.Tests;

public class UpdateTests
{
    private const string Repo = "https://github.com/psychookov/pimpmywindows";

    private sealed class FakeFetcher : IHttpFetcher
    {
        public string? Json { get; set; }

        public Exception? Fail { get; set; }

        public List<Uri> Requested { get; } = [];

        public Task<string> GetStringAsync(Uri uri, CancellationToken cancellationToken)
        {
            Requested.Add(uri);
            return Fail is not null ? Task.FromException<string>(Fail) : Task.FromResult(Json ?? "[]");
        }
    }

    private static string Releases(params (string Tag, bool Draft)[] items) =>
        "[" + string.Join(",", items.Select(i =>
            $"{{\"tag_name\":\"{i.Tag}\",\"draft\":{i.Draft.ToString().ToLower()},\"html_url\":\"{Repo}/releases/tag/{i.Tag}\"}}")) + "]";

    private static AppVersion V(string text) => AppVersion.TryParse(text)!;

    [Theory]
    [InlineData("v0.1.0", "0.1.0", null)]
    [InlineData("1.2", "1.2", null)]
    [InlineData("v2.0.1-beta.2", "2.0.1", "beta.2")]
    [InlineData("1.0.0+abc", "1.0.0", null)]
    public void Parse_AcceptsCommonTagFormats(string text, string number, string? pre)
    {
        var version = AppVersion.TryParse(text)!;
        Assert.Equal(Version.Parse(number), version.Number);
        Assert.Equal(pre, version.PreRelease);
    }

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("v1")]
    [InlineData("1.2.3.4")]
    [InlineData("v-1.0")]
    public void Parse_RejectsGarbage(string text) => Assert.Null(AppVersion.TryParse(text));

    [Fact]
    public void Comparison_FollowsSemanticOrdering()
    {
        Assert.True(V("0.2.0").CompareTo(V("0.1.9")) > 0);
        Assert.True(V("1.0.0").CompareTo(V("1.0.0-rc.1")) > 0);
        Assert.True(V("1.0.0-rc.2").CompareTo(V("1.0.0-rc.1")) > 0);
        Assert.Equal(0, V("v0.1.0").CompareTo(V("0.1.0")));
    }

    [Fact]
    public async Task NewerRelease_IsReported_WithItsPageLink()
    {
        var fetcher = new FakeFetcher { Json = Releases(("v0.1.0", false), ("v0.2.0", false)) };
        var result = await new UpdateChecker(fetcher, V("0.1.0")).CheckAsync();

        Assert.Equal(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Equal("0.2.0", result.LatestVersion);
        Assert.Equal($"{Repo}/releases/tag/v0.2.0", result.PageUrl);
        Assert.Equal(UpdateChecker.ReleasesApi, Assert.Single(fetcher.Requested));
    }

    [Fact]
    public async Task SameVersion_IsUpToDate()
    {
        var result = await new UpdateChecker(new FakeFetcher { Json = Releases(("v0.1.0", false)) }, V("0.1.0")).CheckAsync();
        Assert.Equal(UpdateStatus.UpToDate, result.Status);
        Assert.Null(result.PageUrl);
    }

    [Fact]
    public async Task Drafts_AreIgnored()
    {
        var result = await new UpdateChecker(new FakeFetcher { Json = Releases(("v9.0.0", true), ("v0.1.0", false)) }, V("0.1.0")).CheckAsync();
        Assert.Equal(UpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task NoReleases_IsNotAnError()
    {
        var result = await new UpdateChecker(new FakeFetcher { Json = "[]" }, V("0.1.0")).CheckAsync();
        Assert.Equal(UpdateStatus.UpToDate, result.Status);
    }

    [Fact]
    public async Task NetworkFailure_IsFriendly_NotAnException()
    {
        var result = await new UpdateChecker(new FakeFetcher { Fail = new HttpRequestException("sem rede") }, V("0.1.0")).CheckAsync();
        Assert.Equal(UpdateStatus.Failed, result.Status);
        Assert.DoesNotContain("sem rede", result.Message);
    }

    [Theory]
    [InlineData("isto não é json")]
    [InlineData("{\"message\":\"rate limit\"}")]
    [InlineData("[1,2,3]")]
    public async Task UnexpectedReplies_FailCleanly(string json)
    {
        var result = await new UpdateChecker(new FakeFetcher { Json = json }, V("0.1.0")).CheckAsync();
        Assert.NotEqual(UpdateStatus.UpdateAvailable, result.Status);
    }

    [Fact]
    public void ForeignLinks_AreNeverOffered()
    {
        const string json = "[{\"tag_name\":\"v9.9.9\",\"draft\":false,\"html_url\":\"https://evil.example/download.exe\"}]";
        var result = UpdateChecker.Evaluate(json, V("0.1.0"));
        Assert.NotEqual(UpdateStatus.UpdateAvailable, result.Status);
        Assert.Null(result.PageUrl);
        Assert.True(UpdateChecker.IsRepositoryUrl($"{Repo}/releases/tag/v1"));
        Assert.False(UpdateChecker.IsRepositoryUrl("https://github.com/psychookov/pimpmywindows-evil/releases"));
        Assert.False(UpdateChecker.IsRepositoryUrl(null));
    }

    [Fact]
    public async Task RealFetcher_RefusesOtherHosts()
    {
        var fetcher = new GitHubHttpFetcher();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fetcher.GetStringAsync(new Uri("https://example.com/x"), CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fetcher.GetStringAsync(new Uri("http://api.github.com/x"), CancellationToken.None));
    }
}
