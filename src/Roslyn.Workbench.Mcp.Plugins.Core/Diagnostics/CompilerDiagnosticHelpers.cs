namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

/// <summary>
/// Projects Roslyn compiler diagnostics into plugin contracts and identifies generated documents.
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

    /// <summary>
    /// Determines whether a document path follows a recognised generated-code naming convention.
    /// </summary>
    /// <param name="document">The document to classify.</param>
    /// <returns><see langword="true"/> when the document name identifies generated code; otherwise <see langword="false"/>.</returns>
    public static bool IsGeneratedDocument(Document document)
    {
        var path = document.FilePath ?? document.Name;
        return path.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase)
            || path.Contains("TemporaryGeneratedFile_", StringComparison.OrdinalIgnoreCase);
    }
}
