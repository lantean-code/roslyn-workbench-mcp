using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.LockFixture;

internal static class Program
{
    [SuppressMessage(
        "Performance",
        "CA1849:Call async methods when in an async method",
        Justification = "FileStream.FlushAsync does not expose flushToDisk; the synchronous flush is required before taking the cross-process file lock.")]
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 1)
        {
            return 1;
        }

        if (!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
        {
            return 2;
        }

        using var stream = new FileStream(
            args[0],
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.ReadWrite,
            bufferSize: 1,
            FileOptions.WriteThrough);
        if (stream.Length == 0)
        {
            stream.SetLength(1);
            stream.Flush(flushToDisk: true);
        }

        stream.Lock(0, 1);
        try
        {
            await Console.Out.WriteLineAsync("LOCKED");
            await Console.Out.FlushAsync();
            _ = await Console.In.ReadLineAsync();
            return 0;
        }
        finally
        {
            stream.Unlock(0, 1);
        }
    }
}
