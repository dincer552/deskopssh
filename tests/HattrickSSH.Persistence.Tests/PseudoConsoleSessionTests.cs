using System.Text;
using HattrickSSH.Services;
using Xunit;

namespace HattrickSSH.Persistence.Tests;

public sealed class PseudoConsoleSessionTests
{
    [Fact]
    public async Task StartMonitoring_delivers_interactive_output_and_exit_events()
    {
        var commandProcessor = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var output = new StringBuilder();
        var outputLock = new object();
        var outputReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var processExited = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        // Keep cmd.exe interactive so the test verifies the complete terminal
        // path (PTY input -> shell -> PTY output), not just a process that exits
        // before the output reader has been started.
        using var session = PseudoConsoleSession.Start(
            commandProcessor, "/d /q", columns: 80, rows: 25);

        session.OutputReceived += bytes =>
        {
            string snapshot;
            lock (outputLock)
            {
                output.Append(Encoding.UTF8.GetString(bytes));
                snapshot = output.ToString();
            }

            if (snapshot.Contains("HATTRICK_SSH_CONPTY_PROBE", StringComparison.Ordinal))
                outputReceived.TrySetResult(true);
        };
        session.Exited += exitCode => processExited.TrySetResult(exitCode);

        session.StartMonitoring();
        session.StartMonitoring(); // Starting readers twice must not duplicate event delivery.
        session.WriteInput("echo HATTRICK_SSH_CONPTY_PROBE\r\nexit\r\n");

        await outputReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var exitCode = await processExited.Task.WaitAsync(TimeSpan.FromSeconds(15));

        string captured;
        lock (outputLock) captured = output.ToString();

        Assert.Contains("HATTRICK_SSH_CONPTY_PROBE", captured, StringComparison.Ordinal);
        Assert.Equal(0, exitCode);
    }
}
