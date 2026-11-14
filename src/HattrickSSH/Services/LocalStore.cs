using System.Text.Json;
using HattrickSSH.Models;

namespace HattrickSSH.Services;

public sealed class LocalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public string RootPath { get; }
    public string KnownHostsPath => Path.Combine(RootPath, "known_hosts");
    public string KeyDirectory => Path.Combine(RootPath, "keys");

    private string ProfilesPath => Path.Combine(RootPath, "servers.json");
    private string SettingsPath => Path.Combine(RootPath, "settings.json");
    private string HistoryPath => Path.Combine(RootPath, "connection-history.json");

    public LocalStore(string? rootPath = null)
    {
        RootPath = string.IsNullOrWhiteSpace(rootPath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "HattrickSSH")
            : Path.GetFullPath(rootPath);

        Directory.CreateDirectory(RootPath);
        Directory.CreateDirectory(KeyDirectory);
        if (!File.Exists(KnownHostsPath)) File.WriteAllText(KnownHostsPath, "");
    }

    public List<ServerProfile> LoadProfiles()
    {
        try
        {
            if (File.Exists(ProfilesPath))
                return JsonSerializer.Deserialize<List<ServerProfile>>(File.ReadAllText(ProfilesPath), JsonOptions) ?? [];
        }
        catch { /* Keep a broken settings file from preventing application startup. */ }
        return
        [
            new ServerProfile
            {
                Name = "Azure • hattrick-vm",
                Host = "ssh.dinceryuk.com",
                Port = 22,
                Username = "azureuser",
                UseCloudflareAccess = true
            }
        ];
    }

    public void SaveProfiles(IEnumerable<ServerProfile> profiles) =>
        File.WriteAllText(ProfilesPath, JsonSerializer.Serialize(profiles, JsonOptions));

    public AppSettings LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SettingsPath), JsonOptions) ?? new();
        }
        catch { }
        return new AppSettings();
    }

    public void SaveSettings(AppSettings settings) =>
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));

    public List<ConnectionHistoryEntry> LoadHistory()
    {
        try
        {
            if (File.Exists(HistoryPath))
                return JsonSerializer.Deserialize<List<ConnectionHistoryEntry>>(File.ReadAllText(HistoryPath), JsonOptions) ?? [];
        }
        catch { }
        return [];
    }

    public void SaveHistory(IEnumerable<ConnectionHistoryEntry> history)
    {
        var latestEntries = history
            .OrderByDescending(entry => entry.StartedAtUtc)
            .Take(100)
            .ToList();
        File.WriteAllText(HistoryPath, JsonSerializer.Serialize(latestEntries, JsonOptions));
    }
}
