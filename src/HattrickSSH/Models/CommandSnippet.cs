namespace HattrickSSH.Models;

public sealed class CommandSnippet
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Yeni komut";
    public string Command { get; set; } = "";
}
