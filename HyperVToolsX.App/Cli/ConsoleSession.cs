using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace HyperVToolsX.App.Cli;

/// <summary>
/// Gives this WPF (GUI subsystem) process a usable console for command-line mode: attaches to the console
/// of the launching shell, or opens a new window when there is none (for example when the UAC prompt
/// started an elevated copy). If the output was redirected (<c>&gt; file</c>) the redirection is kept.
/// </summary>
internal sealed class ConsoleSession : IDisposable
{
    private const uint AttachParentProcess = 0xFFFFFFFF;
    private const int StdInput = -10;
    private const int StdOutput = -11;
    private const int StdError = -12;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareReadWrite = 3;
    private const uint OpenExisting = 3;

    private readonly bool _ownsConsole;

    private ConsoleSession(bool ownsConsole) => _ownsConsole = ownsConsole;

    public static ConsoleSession Open()
    {
        var attached = AttachConsole(AttachParentProcess);
        var owns = !attached && AllocConsole();

        BindIfMissing(StdInput, "CONIN$", GenericRead);
        BindIfMissing(StdOutput, "CONOUT$", GenericWrite);
        BindIfMissing(StdError, "CONOUT$", GenericWrite);

        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), encoding) { AutoFlush = true });
        Console.SetError(new StreamWriter(Console.OpenStandardError(), encoding) { AutoFlush = true });

        return new ConsoleSession(owns);
    }

    /// <summary>
    /// A window we created closes as soon as the process exits, taking the output with it, so in an
    /// interactive session wait for a key first. Never waits without a person there to press it.
    /// </summary>
    public void Dispose()
    {
        if (!_ownsConsole)
        {
            return;
        }

        try
        {
            if (Environment.UserInteractive && !Console.IsInputRedirected)
            {
                Console.WriteLine();
                Console.Write("Press any key to close...");
                Console.ReadKey(intercept: true);
            }
        }
        catch (InvalidOperationException)
        {
        }

        FreeConsole();
    }

    private static void BindIfMissing(int standardHandle, string device, uint access)
    {
        var current = GetStdHandle(standardHandle);

        if (current != IntPtr.Zero && current != new IntPtr(-1))
        {
            return;
        }

        var handle = CreateFile(device, access, FileShareReadWrite, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);

        if (!handle.IsInvalid)
        {
            SetStdHandle(standardHandle, handle.DangerousGetHandle());
            // Intentionally never closed: the process-wide standard handle refers to it for the rest of the run.
            GC.SuppressFinalize(handle);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool FreeConsole();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int standardHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetStdHandle(int standardHandle, IntPtr handle);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);
}
