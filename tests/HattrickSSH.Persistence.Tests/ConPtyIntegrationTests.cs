using System.Text;
using HattrickSSH.Services;
using Xunit;

namespace HattrickSSH.Persistence.Tests;

public sealed class ConPtyIntegrationTests
{
    [Fact]
    public async Task ConPty_roundtrips_input_output_and_resize()
    {
        const string marker = "HATTRICK_CONPTY_INPUT_OUTPUT_OK";
        var output = new StringBuilder();
        var outputLock = new object();
        var markerReceived = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var commandProcessor = Environment.GetEnvironmentVariable("ComSpec")
            ?? @"C:\Windows\System32\cmd.exe";

        using var session = PseudoConsoleSession.Start(
            commandProcessor, "/d /q", columns: 100, rows: 30);

        session.OutputReceived += chunk =>
        {
            var text = Encoding.UTF8.GetString(chunk);
            lock (outputLock)
            {
                output.Append(text);
                if (output.ToString().Contains(marker, StringComparison.Ordinal))
                    markerReceived.TrySetResult(true);
            }
        };

        session.StartMonitoring();
        session.Resize(columns: 132, rows: 42);
        session.WriteInput($"echo {marker}\r\n");

        await markerReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));

        string captured;
        lock (outputLock) captured = output.ToString();

        Assert.Contains(marker, captured, StringComparison.Ordinal);
        session.WriteInput("exit\r\n");
    }
}
