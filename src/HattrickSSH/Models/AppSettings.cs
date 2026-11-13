namespace HattrickSSH.Models;

public sealed class AppSettings
{
    public int TerminalFontSize { get; set; } = 16;
    public int TerminalScrollback { get; set; } = 10000;
    public string Theme { get; set; } = "Codex Dark";
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public bool AutoReconnect { get; set; } = false;
    public int KeepAliveSeconds { get; set; } = 30;
}
