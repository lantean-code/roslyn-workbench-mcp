namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

/// <summary>
/// Projects Roslyn compiler diagnostics into plugin contracts.
/// </summary>
internal static class CompilerDiagnosticHelpers
{
    /// <summary>
    /// Projects a Roslyn diagnostic into the location-aware plugin diagnostic contract.
    /// </summary>
    /// <param name="diagnostic">The Roslyn diagnostic to project.</param>
    /// <param name="context">The query context used to resolve source locations.</param>
    /// <returns>The projected diagnostic information.</returns>
    public static DiagnosticInfo CreateDiagnosticInfo(Diagnostic diagnostic, IQueryContext context)
    {
        return new DiagnosticInfo
        {
            Id = diagnostic.Id,
            Severity = InspectionProjectionFactory.MapSeverity(diagnostic.Severity),
            Message = diagnostic.GetMessage(CultureInfo.InvariantCulture),
            Location = diagnostic.Location.IsInSource ? context.WorkspaceResolver.CreateResolvedLocation(diagnostic.Location) : null,
        };
    }
}
