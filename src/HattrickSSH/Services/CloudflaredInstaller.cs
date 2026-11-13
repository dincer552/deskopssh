using System.Net;
using System.Text.Json;

namespace HattrickSSH.Services;

public sealed class CloudflaredInstaller
{
    private const string ReleaseApi = "https://api.github.com/repos/cloudflare/cloudflared/releases/latest";
    private readonly string installPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HattrickSSH", "bin", "cloudflared.exe");

    public async Task<string> EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        if (File.Exists(installPath) && new FileInfo(installPath).Length > 1_000_000)
            return installPath;

        Directory.CreateDirectory(Path.GetDirectoryName(installPath)!);
        using var http = CreateClient();
        using var response = await http.GetAsync(ReleaseApi, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
        var assets = json.RootElement.GetProperty("assets");
        var asset = assets.EnumerateArray().FirstOrDefault(a =>
            a.GetProperty("name").GetString()?.Equals("cloudflared-windows-amd64.exe", StringComparison.OrdinalIgnoreCase) == true);
        if (asset.ValueKind == JsonValueKind.Undefined)
            throw new InvalidOperationException("Cloudflare resmi sürümünde Windows x64 cloudflared dosyası bulunamadı.");

        var downloadUrl = asset.GetProperty("browser_download_url").GetString()
            ?? throw new InvalidOperationException("cloudflared indirme adresi bulunamadı.");
        var tempPath = installPath + ".download";
        using (var download = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            download.EnsureSuccessStatusCode();
            await using var input = await download.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
            await input.CopyToAsync(output, cancellationToken);
        }
        if (new FileInfo(tempPath).Length < 1_000_000)
        {
            File.Delete(tempPath);
            throw new InvalidDataException("İndirilen cloudflared dosyası beklenenden küçük; dosya reddedildi.");
        }
        File.Move(tempPath, installPath, true);
        return installPath;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HattrickSSH/0.1");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
