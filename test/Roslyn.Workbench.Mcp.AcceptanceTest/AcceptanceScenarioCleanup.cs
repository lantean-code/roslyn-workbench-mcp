namespace Roslyn.Workbench.Mcp.AcceptanceTest;

internal static class AcceptanceScenarioCleanup
{
    private static readonly TimeSpan _cleanupTimeout = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan _cleanupRetryInterval = TimeSpan.FromMilliseconds(50);

    public static async Task DeleteAsync(string scenarioRoot)
    {
        using var retryTimer = new PeriodicTimer(_cleanupRetryInterval);
        var elapsed = System.Diagnostics.Stopwatch.StartNew();
        Exception? lastException = null;

        do
        {
            try
            {
                if (!Directory.Exists(scenarioRoot))
                {
                    return;
                }

                Directory.Delete(scenarioRoot, recursive: true);
                return;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                lastException = exception;
            }
        }
        while (elapsed.Elapsed < _cleanupTimeout
            && await retryTimer.WaitForNextTickAsync());

        throw new IOException($"The acceptance scenario root '{scenarioRoot}' could not be removed.", lastException);
    }
}
