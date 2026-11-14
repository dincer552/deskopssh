using System.IO;
using Microsoft.Win32.SafeHandles;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace HattrickSSH.Services;

/// <summary>Owns a Windows ConPTY and a child process. All child output is terminal data, never a command simulator.</summary>
public sealed class PseudoConsoleSession : IDisposable
{
    private IntPtr pseudoConsole;
    private IntPtr processHandle;
    private readonly FileStream input;
    private readonly FileStream output;
    private readonly object writeLock = new();
    private bool disposed;
    private int monitoringStarted;

    public event Action<byte[]>? OutputReceived;
    public event Action<int>? Exited;

    private PseudoConsoleSession(IntPtr pseudoConsole, IntPtr processHandle, SafeFileHandle inputWrite, SafeFileHandle outputRead)
    {
        this.pseudoConsole = pseudoConsole;
        this.processHandle = processHandle;
        input = new FileStream(inputWrite, FileAccess.Write, 4096, false);
        output = new FileStream(outputRead, FileAccess.Read, 8192, false);
    }

    public void StartMonitoring()
    {
        if (Interlocked.Exchange(ref monitoringStarted, 1) != 0) return;
        _ = Task.Run(ReadOutputAsync);
        _ = Task.Run(WaitForExit);
    }

    public static PseudoConsoleSession Start(string executable, string arguments, short columns = 120, short rows = 34)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763))
            throw new PlatformNotSupportedException("Windows ConPTY için Windows 10 sürüm 1809 veya üzeri gerekiyor.");

        // Match the native ConPTY sample: anonymous pipe handles are not inheritable.
        if (!CreatePipe(out var inputRead, out var inputWrite, IntPtr.Zero, 0))
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create ConPTY input pipe.");
        if (!CreatePipe(out var outputRead, out var outputWrite, IntPtr.Zero, 0))
        {
            inputRead.Dispose();
            inputWrite.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not create ConPTY output pipe.");
        }

        IntPtr hpc = IntPtr.Zero;
        IntPtr process = IntPtr.Zero;
        IntPtr thread = IntPtr.Zero;
        IntPtr attributeList = IntPtr.Zero;
        try
        {
            var size = new COORD(columns, rows);
            var hr = CreatePseudoConsole(size, inputRead, outputWrite, 0, out hpc);
            if (hr != 0) Marshal.ThrowExceptionForHR(hr);
            inputRead.Dispose();
            outputWrite.Dispose();

            // The size parameter is IN/OUT: the second call must receive the
            // allocation size returned by the initial size-query call.
            var attributeSize = IntPtr.Zero;
            if (InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeSize))
                throw new InvalidOperationException("ConPTY process attribute size query unexpectedly succeeded.");
            var sizeQueryError = Marshal.GetLastWin32Error();
            if (sizeQueryError != 122 || attributeSize == IntPtr.Zero)
                throw new Win32Exception(sizeQueryError, "ConPTY process attribute buffer size could not be determined.");

            attributeList = Marshal.AllocHGlobal(attributeSize);
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeSize))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (!UpdateProcThreadAttribute(attributeList, 0, (IntPtr)0x00020016, hpc, (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var startup = new STARTUPINFOEX
            {
                StartupInfo = new STARTUPINFO { cb = Marshal.SizeOf<STARTUPINFOEX>() },
                lpAttributeList = attributeList
            };
            var command = new StringBuilder(Quote(executable) + " " + arguments);
            if (!CreateProcess(executable, command, IntPtr.Zero, IntPtr.Zero, false,
                0x00080000 | 0x00000400, IntPtr.Zero, Path.GetDirectoryName(executable),
                ref startup, out var info))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            process = info.hProcess;
            thread = info.hThread;
            CloseHandle(thread);
            thread = IntPtr.Zero;
            var session = new PseudoConsoleSession(hpc, process, inputWrite, outputRead);
            hpc = IntPtr.Zero;
            process = IntPtr.Zero;
            inputWrite = null!;
            outputRead = null!;
            return session;
        }
        catch
        {
            if (hpc != IntPtr.Zero) ClosePseudoConsole(hpc);
            if (process != IntPtr.Zero) CloseHandle(process);
            inputRead.Dispose();
            inputWrite?.Dispose();
            outputRead.Dispose();
            outputWrite.Dispose();
            throw;
        }
        finally
        {
            if (thread != IntPtr.Zero) CloseHandle(thread);
            if (attributeList != IntPtr.Zero)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }
        }
    }

    public void WriteInput(string text)
    {
        if (disposed || string.IsNullOrEmpty(text)) return;
        var bytes = Encoding.UTF8.GetBytes(text);
        lock (writeLock) input.Write(bytes, 0, bytes.Length);
        input.Flush();
    }

    public void Resize(short columns, short rows)
    {
        if (disposed || pseudoConsole == IntPtr.Zero) return;
        var result = ResizePseudoConsole(pseudoConsole, new COORD((short)Math.Clamp((int)columns, 20, 400), (short)Math.Clamp((int)rows, 5, 200)));
        if (result != 0) Marshal.ThrowExceptionForHR(result);
    }

    private async Task ReadOutputAsync()
    {
        var buffer = new byte[8192];
        try
        {
            while (!disposed)
            {
                var count = await output.ReadAsync(buffer);
                if (count == 0) break;
                var chunk = new byte[count];
                Buffer.BlockCopy(buffer, 0, chunk, 0, count);
                OutputReceived?.Invoke(chunk);
            }
        }
        catch (ObjectDisposedException) { }
        catch (IOException) { }
    }

    private void WaitForExit()
    {
        var handle = processHandle;
        if (handle == IntPtr.Zero) return;
        WaitForSingleObject(handle, 0xFFFFFFFF);
        GetExitCodeProcess(handle, out var exitCode);
        Exited?.Invoke(unchecked((int)exitCode));
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { input.Dispose(); } catch { }
        try { output.Dispose(); } catch { }
        if (processHandle != IntPtr.Zero)
        {
            GetExitCodeProcess(processHandle, out var code);
            if (code == 259) TerminateProcess(processHandle, 1);
            WaitForSingleObject(processHandle, 1000);
            CloseHandle(processHandle);
            processHandle = IntPtr.Zero;
        }
        if (pseudoConsole != IntPtr.Zero)
        {
            ClosePseudoConsole(pseudoConsole);
            pseudoConsole = IntPtr.Zero;
        }
    }

    // Executable paths cannot contain quotes. Do not double backslashes here:
    // Windows command-line parsing treats ordinary backslashes literally.
    private static string Quote(string value) => "\"" + value + "\"";

    [StructLayout(LayoutKind.Sequential)]
    private struct COORD(short x, short y) { public short X = x; public short Y = y; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb; public IntPtr lpReserved; public IntPtr lpDesktop; public IntPtr lpTitle;
        public uint dwX; public uint dwY; public uint dwXSize; public uint dwYSize; public uint dwXCountChars; public uint dwYCountChars;
        public uint dwFillAttribute; public uint dwFlags; public ushort wShowWindow; public ushort cbReserved2;
        public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX { public STARTUPINFO StartupInfo; public IntPtr lpAttributeList; }
    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION { public IntPtr hProcess; public IntPtr hThread; public uint dwProcessId; public uint dwThreadId; }

    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, IntPtr attributes, int size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern int CreatePseudoConsole(COORD size, SafeFileHandle input, SafeFileHandle output, uint flags, out IntPtr console);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern int ResizePseudoConsole(IntPtr console, COORD size);
    [DllImport("kernel32.dll")] private static extern void ClosePseudoConsole(IntPtr console);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool InitializeProcThreadAttributeList(IntPtr list, int count, int flags, ref IntPtr size);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool UpdateProcThreadAttribute(IntPtr list, uint flags, IntPtr attribute, IntPtr value, IntPtr size, IntPtr previous, IntPtr returnSize);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(IntPtr list);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool CreateProcess(string? applicationName, StringBuilder commandLine, IntPtr processAttributes, IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string? currentDirectory, ref STARTUPINFOEX startupInfo, out PROCESS_INFORMATION processInformation);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool TerminateProcess(IntPtr process, uint exitCode);
}
