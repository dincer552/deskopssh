using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace HattrickSSH.Services;

public sealed class CloudflaredInstaller
{
    private const string ReleaseApi = "https://api.github.com/repos/cloudflare/cloudflared/releases/latest";
    private const string AssetName = "cloudflared-windows-amd64.exe";
    private const string OfficialDownloadPrefix = "https://github.com/cloudflare/cloudflared/releases/download/";
    private readonly string installPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HattrickSSH", "bin", "cloudflared.exe");

    public async Task<string> EnsureInstalledAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(installPath)!);

        using var http = CreateClient();
        using var response = await http.GetAsync(ReleaseApi, cancellationToken);
        response.EnsureSuccessStatusCode();
        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var assets = json.RootElement.GetProperty("assets");
        var asset = assets.EnumerateArray().FirstOrDefault(candidate =>
            candidate.GetProperty("name").GetString()?.Equals(AssetName, StringComparison.OrdinalIgnoreCase) == true);
        if (asset.ValueKind == JsonValueKind.Undefined)
            throw new InvalidOperationException("Cloudflare resmi sürümünde Windows x64 cloudflared dosyası bulunamadı.");

        var downloadUrl = asset.GetProperty("browser_download_url").GetString()
            ?? throw new InvalidOperationException("cloudflared indirme adresi bulunamadı.");
        if (!downloadUrl.StartsWith(OfficialDownloadPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("cloudflared indirme adresi resmi Cloudflare GitHub sürümüne ait değil.");

        var expectedHash = ReadExpectedSha256(asset);
        var expectedSize = asset.TryGetProperty("size", out var sizeElement)
            ? sizeElement.GetInt64()
            : 0L;

        if (File.Exists(installPath))
        {
            try
            {
                var existingSize = new FileInfo(installPath).Length;
                if (existingSize > 1_000_000 &&
                    (expectedSize <= 0 || existingSize == expectedSize) &&
                    await MatchesSha256Async(installPath, expectedHash, cancellationToken))
                    return installPath;
            }
            catch (IOException)
            {
                // A missing, locked or unreadable cache is replaced from the official release.
            }

            File.Delete(installPath);
        }

        var tempPath = installPath + "." + Guid.NewGuid().ToString("N") + ".download";
        try
        {
            using (var download = await http.GetAsync(
                downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                download.EnsureSuccessStatusCode();
                await using var input = await download.Content.ReadAsStreamAsync(cancellationToken);
                await using var output = new FileStream(
                    tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
                await input.CopyToAsync(output, cancellationToken);
            }

            var downloadedSize = new FileInfo(tempPath).Length;
            if (downloadedSize < 1_000_000 || (expectedSize > 0 && downloadedSize != expectedSize))
                throw new InvalidDataException("İndirilen cloudflared dosyasının boyutu resmi sürüm bilgisiyle uyuşmuyor.");

            if (!await MatchesSha256Async(tempPath, expectedHash, cancellationToken))
                throw new InvalidDataException("cloudflared SHA-256 doğrulaması başarısız.");

            File.Move(tempPath, installPath, overwrite: true);
            return installPath;
        }
        finally
        {
            try
            {
                if (File.Exists(tempPath)) File.Delete(tempPath);
            }
            catch { }
        }
    }

    private static string ReadExpectedSha256(JsonElement asset)
    {
        if (!asset.TryGetProperty("digest", out var digestElement))
            throw new InvalidDataException("Cloudflare sürümünde cloudflared SHA-256 özeti yayımlanmamış; dosya güvenlik nedeniyle kabul edilmedi.");

        var digest = digestElement.GetString();
        const string prefix = "sha256:";
        if (digest is null ||
            !digest.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ||
            digest.Length != prefix.Length + 64 ||
            !digest[prefix.Length..].All(Uri.IsHexDigit))
            throw new InvalidDataException("Cloudflare cloudflared SHA-256 bilgisi geçersiz.");

        return digest[prefix.Length..];
    }

    private static async Task<bool> MatchesSha256Async(
        string path, string expectedHash, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var actualHash = await SHA256.HashDataAsync(stream, cancellationToken);
        var expectedBytes = Convert.FromHexString(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedBytes);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HattrickSSH/0.1");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
