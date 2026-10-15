namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Reports known durable-storage failures before hosted services and the MCP transport start.
/// </summary>
internal static partial class HostStartupStorage
{
    /// <summary>
    /// Creates and validates durable storage at the application boundary before host startup logging begins.
    /// </summary>
    /// <param name="stateDirectory">The secured directory used for durable recovery storage.</param>
    /// <param name="logger">The logger that retains exception details at debug level.</param>
    /// <param name="errorOutput">The writer that receives the concise startup diagnostic.</param>
    /// <returns><see langword="true"/> when storage is available; otherwise, <see langword="false"/>.</returns>
    public static bool TryInitialize(IWorkspaceStateDirectory stateDirectory, ILogger logger, TextWriter errorOutput)
    {
        try
        {
            stateDirectory.Initialize();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException
            or InvalidOperationException { InnerException: IOException or UnauthorizedAccessException })
        {
            errorOutput.WriteLine($"Cannot initialise durable recovery storage at '{stateDirectory.RecoveryDirectory}'. {exception.Message} Configure --state-directory with a writable local directory that permits durable file creation and deletion. Enable Logging__LogLevel__Default=Debug for exception details.");
            LogInitializationFailure(logger, exception);
            return false;
        }
    }

    [LoggerMessage(1, LogLevel.Debug, "Durable recovery storage could not be initialised.")]
    private static partial void LogInitializationFailure(ILogger logger, Exception exception);
}
