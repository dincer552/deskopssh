using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;

namespace HattrickSSH.Services;

public sealed record UpdateInfo(string Version, string NotesUrl, string InstallerUrl, string Sha256);

public sealed class UpdateService
{
    private const string Repo = "https://api.github.com/repos/dincer552/deskopssh";
    private static readonly HttpClient Http = CreateClient();

    public async Task<UpdateInfo?> CheckAsync(string currentVersion, CancellationToken token = default)
    {
        using var response = await Http.GetAsync(Repo + "/releases/latest", token);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(token), cancellationToken: token);
        var root = doc.RootElement;
        var version = root.GetProperty("tag_name").GetString()?.TrimStart('v') ?? "0.0.0";
        if (!Version.TryParse(version, out var remote) || !Version.TryParse(currentVersion, out var local) || remote <= local)
            return null;
        var assets = root.GetProperty("assets").EnumerateArray().ToDictionary(
            a => a.GetProperty("name").GetString() ?? "", a => a.GetProperty("browser_download_url").GetString() ?? "");
        if (!assets.TryGetValue("latest.json", out var manifestUrl) ||
            !assets.TryGetValue("HattrickSSH-Setup.exe", out var installerUrl)) return null;
        using var manifestResponse = await Http.GetAsync(manifestUrl, token);
        manifestResponse.EnsureSuccessStatusCode();
        using var manifest = await JsonDocument.ParseAsync(await manifestResponse.Content.ReadAsStreamAsync(token), cancellationToken: token);
        var hash = manifest.RootElement.GetProperty("installerSha256").GetString() ?? "";
        var notes = root.GetProperty("html_url").GetString() ?? "https://github.com/dincer552/deskopssh/releases";
        if (hash.Length != 64 || !hash.All(Uri.IsHexDigit)) throw new InvalidDataException("Release güncelleme manifesti geçersiz.");
        return new UpdateInfo(version, notes, installerUrl, hash);
    }

    public async Task DownloadAndLaunchInstallerAsync(UpdateInfo update, CancellationToken token = default)
    {
        var installer = Path.Combine(Path.GetTempPath(), "HattrickSSH-Setup-" + update.Version + ".exe");
        using (var response = await Http.GetAsync(update.InstallerUrl, HttpCompletionOption.ResponseHeadersRead, token))
        {
            response.EnsureSuccessStatusCode();
            await using var source = await response.Content.ReadAsStreamAsync(token);
            await using var destination = new FileStream(installer, FileMode.Create, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(destination, token);
        }

        await using var file = File.OpenRead(installer);
        var actual = Convert.ToHexString(await SHA256.HashDataAsync(file, token));
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(actual), Convert.FromHexString(update.Sha256)))
        {
            File.Delete(installer);
            throw new InvalidDataException("İndirilen kurulum dosyasının SHA-256 değeri uyuşmuyor. Güncelleme durduruldu.");
        }

        Process.Start(new ProcessStartInfo(installer,
            "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /RESTARTAPPLICATIONS")
        { UseShellExecute = true });
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("HattrickSSH-Updater/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return client;
    }
}
