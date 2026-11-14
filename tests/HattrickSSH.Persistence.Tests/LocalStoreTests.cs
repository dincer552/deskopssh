using HattrickSSH.Models;
using HattrickSSH.Services;
using Xunit;

namespace HattrickSSH.Persistence.Tests;

public sealed class LocalStoreTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(), "HattrickSSH.Persistence.Tests", Guid.NewGuid().ToString("N"));

    private LocalStore CreateStore() => new(rootPath);

    [Fact]
    public void LoadProfiles_usesAzureDefaults_whenNoProfileFileExists()
    {
        var profile = Assert.Single(CreateStore().LoadProfiles());

        Assert.Equal("ssh.dinceryuk.com", profile.Host);
        Assert.Equal(22, profile.Port);
        Assert.Equal("azureuser", profile.Username);
        Assert.True(profile.UseCloudflareAccess);
    }

    [Fact]
    public void Profiles_roundTripThroughLocalStorage()
    {
        var store = CreateStore();
        var expected = new ServerProfile
        {
            Name = "Test VM",
            Host = "example.internal",
            Port = 2222,
            Username = "tester",
            UseCloudflareAccess = true,
            Group = "Test"
        };

        store.SaveProfiles([expected]);
        var actual = Assert.Single(store.LoadProfiles());

        Assert.Equal(expected.Id, actual.Id);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Host, actual.Host);
        Assert.Equal(expected.Port, actual.Port);
        Assert.Equal(expected.Username, actual.Username);
        Assert.Equal(expected.UseCloudflareAccess, actual.UseCloudflareAccess);
        Assert.Equal(expected.Group, actual.Group);
    }

    [Fact]
    public void Settings_roundTripIncludingSnippetCommands()
    {
        var store = CreateStore();
        var expected = new AppSettings
        {
            TerminalFontSize = 13,
            TerminalScrollback = 5000,
            Theme = "Midnight Blue",
            AutoReconnect = true,
            KeepAliveSeconds = 45,
            Snippets = [new CommandSnippet { Name = "Disk", Command = "df -h /" }]
        };

        store.SaveSettings(expected);
        var actual = store.LoadSettings();

        Assert.Equal(13, actual.TerminalFontSize);
        Assert.Equal(5000, actual.TerminalScrollback);
        Assert.Equal("Midnight Blue", actual.Theme);
        Assert.True(actual.AutoReconnect);
        Assert.Equal(45, actual.KeepAliveSeconds);
        Assert.Equal("df -h /", Assert.Single(actual.Snippets).Command);
    }

    [Fact]
    public void SaveHistory_keepsNewestOneHundredEntriesInDescendingOrder()
    {
        var store = CreateStore();
        var entries = Enumerable.Range(0, 125)
            .Select(i => new ConnectionHistoryEntry
            {
                ProfileName = $"VM {i}",
                Host = "example.internal",
                Username = "tester",
                StartedAtUtc = DateTime.UnixEpoch.AddMinutes(i)
            })
            .ToArray();

        store.SaveHistory(entries);
        var actual = store.LoadHistory();

        Assert.Equal(100, actual.Count);
        Assert.Equal("VM 124", actual[0].ProfileName);
        Assert.Equal("VM 25", actual[^1].ProfileName);
        Assert.True(actual.Zip(actual.Skip(1), (left, right) =>
            left.StartedAtUtc >= right.StartedAtUtc).All(isDescending => isDescending));
    }

    [Fact]
    public void LoadHistory_returnsEmptyList_whenHistoryFileIsMalformed()
    {
        var store = CreateStore();
        File.WriteAllText(Path.Combine(rootPath, "connection-history.json"), "{ invalid json");

        Assert.Empty(store.LoadHistory());
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
            Directory.Delete(rootPath, recursive: true);
    }
}
