namespace HattrickSSH.Models;

public sealed class ServerProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Yeni Sunucu";
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "";
    public bool UseCloudflareAccess { get; set; }
    public string? PrivateKeyId { get; set; }
    public string Group { get; set; } = "Genel";
    public string Notes { get; set; } = "";
    public override string ToString() => Name;
}
