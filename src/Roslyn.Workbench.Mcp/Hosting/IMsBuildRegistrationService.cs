namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Registers the process-wide MSBuild toolset once and exposes its availability.
/// </summary>
internal interface IMsBuildRegistrationService
{
    /// <summary>
    /// Ensures an MSBuild toolset has been registered for Roslyn workspace loading.
    /// </summary>
    /// <returns>The cached registration result, including version or failure details.</returns>
    ComponentStatus EnsureRegistered();

    /// <summary>
    /// Gets the cached registration result, or an unavailable status before registration is attempted.
    /// </summary>
    ComponentStatus CurrentStatus { get; }
}
