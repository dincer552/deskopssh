using System.Text;
using System.Text.RegularExpressions;
using HattrickSSH.Models;

namespace HattrickSSH.Services;

public sealed class SshConfigWriter(LocalStore store)
{
    public string Write(ServerProfile profile, string? identityPath, string? cloudflaredPath)
    {
        if (!Guid.TryParseExact(profile.Id, "N", out _) ||
            !Regex.IsMatch(profile.Host, @"^[A-Za-z0-9:][A-Za-z0-9._:-]{0,252}$") ||
            !Regex.IsMatch(profile.Username, @"^[A-Za-z0-9_][A-Za-z0-9._-]{0,63}$") ||
            profile.Port is < 1 or > 65535)
            throw new InvalidDataException("Profil kimliği, SSH host, kullanıcı adı veya port bilgisi geçersiz.");
        var dir = Path.Combine(store.RootPath, "ssh");
        Directory.CreateDirectory(dir);
        var configPath = Path.Combine(dir, profile.Id + ".conf");
        var alias = "hssh-" + profile.Id;
        var lines = new List<string>
        {
            $"Host {alias}",
            $"  HostName {profile.Host}",
            $"  User {profile.Username}",
            $"  Port {profile.Port}",
            "  ServerAliveInterval 30",
            "  ServerAliveCountMax 3",
            "  ConnectTimeout 25",
            $"  UserKnownHostsFile \"{store.KnownHostsPath.Replace("\\", "/")}\"",
            "  StrictHostKeyChecking ask",
            "  UpdateHostKeys yes"
        };
        if (!string.IsNullOrWhiteSpace(identityPath))
        {
            lines.Add($"  IdentityFile \"{identityPath.Replace("\\", "/")}\"");
            lines.Add("  IdentitiesOnly yes");
        }
        if (profile.UseCloudflareAccess)
        {
            if (string.IsNullOrWhiteSpace(cloudflaredPath))
                throw new InvalidOperationException("Cloudflared yolu bulunamadı.");
            lines.Add($"  ProxyCommand \"{cloudflaredPath.Replace("\\", "/")}\" access ssh --hostname %h");
        }
        File.WriteAllText(configPath, string.Join(Environment.NewLine, lines) + Environment.NewLine, new UTF8Encoding(false));
        return configPath;
    }
}
