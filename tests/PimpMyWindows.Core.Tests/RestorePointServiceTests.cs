using PimpMyWindows.Core.Safety;

namespace PimpMyWindows.Core.Tests;

public class RestorePointServiceTests
{
    [Theory]
    [InlineData("Pimp my Windows - antes", "Pimp my Windows - antes")]
    [InlineData("x'; Remove-Item C:\\ -Recurse; '", "x Remove-Item C -Recurse")]
    [InlineData("", "Pimp my Windows")]
    [InlineData("$(calc)", "calc")]
    public void Sanitize_KeepsOnlySafeCharacters(string input, string expected)
    {
        Assert.Equal(expected, RestorePointService.Sanitize(input));
    }

    [Fact]
    public void Sanitize_LimitsLength()
    {
        Assert.Equal(100, RestorePointService.Sanitize(new string('a', 500)).Length);
    }

    [Fact]
    public async Task CreateAsync_WithoutAdministrator_FailsWithoutRunningAnything()
    {
        var service = new RestorePointService(() => false, new NullActionLog());

        var result = await service.CreateAsync("teste");

        Assert.False(result.Success);
        Assert.Contains("administrador", result.Message);
    }
}
