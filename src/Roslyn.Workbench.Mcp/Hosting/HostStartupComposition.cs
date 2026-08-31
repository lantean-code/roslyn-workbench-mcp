namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Groups the validated configuration and fixed Code Action catalogue created before dependency injection.
/// </summary>
internal sealed record HostStartupComposition
{
    /// <summary>
    /// Gets the resolved startup configuration and fallback warnings.
    /// </summary>
    public required StartupConfigurationSnapshot Configuration { get; init; }

    /// <summary>
    /// Gets the host-owned Code Action catalogue fixed for this process.
    /// </summary>
    public required CodeActionCatalogSnapshot CodeActions { get; init; }

    /// <summary>
    /// Gets the validated options from the resolved configuration.
    /// </summary>
    public StartupOptions Options => Configuration.Options;
}
