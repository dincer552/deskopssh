namespace HattrickSSH.Models;

public sealed class ConnectionHistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProfileName { get; set; } = "";
    public string Host { get; set; } = "";
    public string Username { get; set; } = "";
    public string Transport { get; set; } = "SSH";
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndedAtUtc { get; set; }
    public string Status { get; set; } = "Başlatıldı";
    public string TimeLabel => StartedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
    public string DetailLabel => $"{Username}@{Host} • {Transport}";
    public string EndLabel => EndedAtUtc is null ? "Devam ediyor" : EndedAtUtc.Value.ToLocalTime().ToString("HH:mm:ss");
}
