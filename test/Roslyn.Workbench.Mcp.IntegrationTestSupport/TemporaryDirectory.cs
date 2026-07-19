using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public sealed class TemporaryDirectory : IDisposable
{
    private int _isDisposed;

    private TemporaryDirectory(string directoryPath)
    {
        DirectoryPath = directoryPath;
    }

    public string DirectoryPath { get; }

    public static TemporaryDirectory Create(string prefix)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var directoryPath = Path.Combine(
            Path.GetTempPath(),
            prefix,
            Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(directoryPath);
        return new TemporaryDirectory(directoryPath);
    }

    public static TemporaryDirectory Attach(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);

        return new TemporaryDirectory(directoryPath);
    }

    [SuppressMessage(
        "Design",
        "CA1065:Do not raise exceptions in unexpected locations",
        Justification = "Test cleanup failures must fail the owning test with the temporary path and disposal guidance instead of being silently ignored.")]
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0 || !Directory.Exists(DirectoryPath))
        {
            return;
        }

        try
        {
            Directory.Delete(DirectoryPath, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw new IOException(
                $"Failed to delete temporary test directory '{DirectoryPath}'. Ensure every workspace, service provider and child process that uses the directory has been disposed.",
                exception);
        }
    }

}
