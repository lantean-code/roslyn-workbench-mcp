using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Performs thread-safe, process-wide MSBuild toolset registration and caches its result.
/// </summary>
internal sealed class MsBuildRegistrationService : IMsBuildRegistrationService
{
    private readonly Lock _syncRoot = new();
    private ComponentStatus? _status;

    /// <summary>
    /// Gets the cached registration result, or an unavailable status before registration is attempted.
    /// </summary>
    public ComponentStatus CurrentStatus => _status ?? new ComponentStatus
    {
        IsAvailable = false,
        Message = "MSBuild has not been registered.",
    };

    /// <summary>
    /// Registers the default MSBuild toolset once, converting discovery failures into component status.
    /// </summary>
    /// <returns>The cached registration result, including version or failure details.</returns>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "MSBuild discovery is a host startup prerequisite boundary; any locator failure is published as unavailable component status instead of terminating the MCP server.")]
    public ComponentStatus EnsureRegistered()
    {
        lock (_syncRoot)
        {
            if (_status is not null)
            {
                return _status;
            }

            if (MSBuildLocator.IsRegistered)
            {
                _status = CreateAlreadyRegisteredStatus();
                return _status;
            }

            try
            {
                var instance = MSBuildLocator.RegisterDefaults();
                _status = CreateRegisteredStatus(instance);
            }
            catch (InvalidOperationException) when (MSBuildLocator.IsRegistered)
            {
                _status = CreateAlreadyRegisteredStatus();
            }
            catch (Exception exception)
            {
                _status = CreateUnavailableStatus(exception);
            }

            return _status;
        }
    }

    private static ComponentStatus CreateAlreadyRegisteredStatus()
    {
        return new ComponentStatus
        {
            IsAvailable = true,
            Version = null,
            Message = "MSBuild was registered before the server started.",
        };
    }

    private static ComponentStatus CreateRegisteredStatus(VisualStudioInstance instance)
    {
        return new ComponentStatus
        {
            IsAvailable = true,
            Version = instance.Version.ToString(),
            Message = instance.MSBuildPath,
        };
    }

    private static ComponentStatus CreateUnavailableStatus(Exception exception)
    {
        return new ComponentStatus
        {
            IsAvailable = false,
            Version = null,
            Message = exception.Message,
        };
    }
}
