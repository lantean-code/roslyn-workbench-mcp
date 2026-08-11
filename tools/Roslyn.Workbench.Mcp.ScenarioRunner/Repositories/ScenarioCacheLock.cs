namespace Roslyn.Workbench.Mcp.ScenarioRunner.Repositories;

internal sealed class ScenarioCacheLock : IDisposable
{
    private const string _lockFileName = ".scenario-runner.lock";

    private FileStream? _stream;

    private ScenarioCacheLock(FileStream stream)
    {
        _stream = stream;
    }

    public static ScenarioCacheLock Acquire(string cacheDirectory)
    {
        Directory.CreateDirectory(cacheDirectory);
        var lockPath = GetLockPath(cacheDirectory);
        try
        {
            var stream = new FileStream(
                lockPath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None);
            return new ScenarioCacheLock(stream);
        }
        catch (IOException exception)
        {
            throw new InvalidOperationException(
                $"Could not acquire exclusive access to scenario cache '{cacheDirectory}'. Another runner may be using it; wait for that run to finish or specify a different --cache path. {exception.Message}",
                exception);
        }
    }

    public static string GetLockPath(string cacheDirectory)
    {
        return Path.Combine(cacheDirectory, _lockFileName);
    }

    public void Dispose()
    {
        var stream = Interlocked.Exchange(ref _stream, null);
        if (stream is null)
        {
            return;
        }

        stream.Dispose();
    }
}
