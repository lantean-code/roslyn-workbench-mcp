namespace Roslyn.Workbench.Mcp.Workspace.State;

/// <summary>
/// Resolves selector failures to the stable error codes owned by the Workspace boundary.
/// </summary>
internal static class SelectorErrorCodeResolver
{
    /// <summary>
    /// Gets the stable error code for a selector target and resolution status.
    /// </summary>
    /// <param name="targetCode">The supported selector target name.</param>
    /// <param name="status">The unsuccessful selector resolution status.</param>
    /// <returns>The stable selector error code.</returns>
    public static string Resolve(string targetCode, SelectorResolveStatus status)
    {
        return (targetCode, status) switch
        {
            ("Document", SelectorResolveStatus.Ambiguous) => WorkspaceErrorCodes.DocumentAmbiguous,
            ("Document", SelectorResolveStatus.Invalid) => WorkspaceErrorCodes.DocumentSelectorInvalid,
            ("Document", _) => WorkspaceErrorCodes.DocumentNotFound,
            ("Project", SelectorResolveStatus.Ambiguous) => WorkspaceErrorCodes.ProjectAmbiguous,
            ("Project", SelectorResolveStatus.Invalid) => WorkspaceErrorCodes.ProjectSelectorInvalid,
            ("Project", _) => WorkspaceErrorCodes.ProjectNotFound,
            ("Symbol", SelectorResolveStatus.Ambiguous) => WorkspaceErrorCodes.SymbolAmbiguous,
            ("Symbol", SelectorResolveStatus.Invalid) => WorkspaceErrorCodes.SymbolSelectorInvalid,
            ("Symbol", _) => WorkspaceErrorCodes.SymbolNotFound,
            ("Location", SelectorResolveStatus.Ambiguous) => WorkspaceErrorCodes.LocationAmbiguous,
            ("Location", SelectorResolveStatus.Invalid) => WorkspaceErrorCodes.LocationSelectorInvalid,
            ("Location", _) => WorkspaceErrorCodes.LocationNotFound,
            _ => throw new InvalidOperationException($"Selector target '{targetCode}' is not supported."),
        };
    }
}
