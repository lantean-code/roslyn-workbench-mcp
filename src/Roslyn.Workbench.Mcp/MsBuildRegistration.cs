using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp;

internal static class MsBuildRegistration
{
    private static readonly Lock _syncRoot = new();
    private static ComponentStatus? _status;

    public static ComponentStatus EnsureRegistered()
    {
        lock (_syncRoot)
        {
            if (_status is not null)
            {
                return _status;
            }

            try
            {
                var instance = MSBuildLocator.RegisterDefaults();
                _status = new ComponentStatus
                {
                    IsAvailable = true,
                    Version = instance.Version.ToString(),
                    Message = instance.MSBuildPath,
                };
            }
            catch (InvalidOperationException exception) when (MSBuildLocator.IsRegistered)
            {
                _status = new ComponentStatus
                {
                    IsAvailable = true,
                    Version = null,
                    Message = exception.Message,
                };
            }
            catch (Exception exception)
            {
                _status = new ComponentStatus
                {
                    IsAvailable = false,
                    Version = null,
                    Message = exception.Message,
                };
            }

            return _status;
        }
    }

    public static ComponentStatus CurrentStatus => _status ?? new ComponentStatus
    {
        IsAvailable = false,
        Message = "MSBuild has not been registered.",
    };
}
