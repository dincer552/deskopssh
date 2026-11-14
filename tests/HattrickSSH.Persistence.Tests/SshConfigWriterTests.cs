using HattrickSSH.Models;
using HattrickSSH.Services;
using Xunit;

namespace HattrickSSH.Persistence.Tests;

public sealed class SshConfigWriterTests : IDisposable
{
    private readonly string rootPath = Path.Combine(
        Path.GetTempPath(), "HattrickSSH.SshConfig.Tests", Guid.NewGuid().ToString("N"));

    private LocalStore CreateStore() => new(rootPath);

    [Fact]
    public void Write_requiresHostKeyVerificationAndDedicatedKnownHostsFile()
    {
        var store = CreateStore();
        var profile = new ServerProfile
        {
            Id = "test-profile",
            Host = "ssh.example.internal",
            Username = "tester",
            Port = 22
        };

        var configPath = new SshConfigWriter(store).Write(profile, null, null);
        var config = File.ReadAllText(configPath);

        Assert.Contains("StrictHostKeyChecking ask", config);
        Assert.Contains("UpdateHostKeys yes", config);
        Assert.Contains(store.KnownHostsPath.Replace("\\", "/"), config);
        Assert.DoesNotContain("StrictHostKeyChecking no", config);
    }

    [Fact]
    public void Write_addsCloudflareAccessProxyCommand()
    {
        var store = CreateStore();
        var profile = new ServerProfile
        {
            Id = "cloudflare-profile",
            Host = "ssh.example.internal",
            Username = "tester",
            Port = 22,
            UseCloudflareAccess = true
        };

        var configPath = new SshConfigWriter(store).Write(
            profile, null, @"C:\Program Files\cloudflared\cloudflared.exe");
        var config = File.ReadAllText(configPath);

        Assert.Contains("ProxyCommand", config);
        Assert.Contains("access ssh --hostname %h", config);
        Assert.Contains("C:/Program Files/cloudflared/cloudflared.exe", config);
    }

    [Fact]
    public void Write_rejectsUnsafeProfileId()
    {
        var store = CreateStore();
        var profile = new ServerProfile
        {
            Id = "../outside",
            Host = "ssh.example.internal",
            Username = "tester",
            Port = 22
        };

        Assert.Throws<InvalidDataException>(() => new SshConfigWriter(store).Write(profile, null, null));
    }

    [Theory]
    [InlineData("ssh.example.internal;ProxyCommand evil", "tester", 22)]
    [InlineData("ssh.example.internal", "tester;rm -rf /", 22)]
    [InlineData("ssh.example.internal", "tester", 0)]
    [InlineData("ssh.example.internal", "tester", 65536)]
    public void Write_rejectsInvalidConnectionFields(string host, string username, int port)
    {
        var store = CreateStore();
        var profile = new ServerProfile
        {
            Id = "invalid-profile",
            Host = host,
            Username = username,
            Port = port
        };

        Assert.Throws<InvalidDataException>(() => new SshConfigWriter(store).Write(profile, null, null));
    }

    [Fact]
    public void Write_requiresCloudflaredExecutableWhenAccessIsEnabled()
    {
        var store = CreateStore();
        var profile = new ServerProfile
        {
            Id = "missing-cloudflared",
            Host = "ssh.example.internal",
            Username = "tester",
            UseCloudflareAccess = true
        };

        Assert.Throws<InvalidOperationException>(() =>
            new SshConfigWriter(store).Write(profile, null, null));
    }

    public void Dispose()
    {
        if (Directory.Exists(rootPath))
            Directory.Delete(rootPath, recursive: true);
    }
}
