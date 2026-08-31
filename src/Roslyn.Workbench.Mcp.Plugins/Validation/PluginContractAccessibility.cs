namespace Roslyn.Workbench.Mcp.Plugins.Validation;

/// <summary>
/// Specifies the contract visibility permitted by a plugin loading mode.
/// </summary>
internal enum PluginContractAccessibility
{
    /// <summary>
    /// Requires request, response and component types to be publicly accessible for external plugins.
    /// </summary>
    PublicOnly,

    /// <summary>
    /// Allows non-public contract types for trusted first-party plugin registration.
    /// </summary>
    AllowNonPublic,
}
