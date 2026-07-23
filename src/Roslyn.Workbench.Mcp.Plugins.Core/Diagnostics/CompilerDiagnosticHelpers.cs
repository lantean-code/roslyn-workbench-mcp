namespace Roslyn.Workbench.Mcp.Plugins.Core.Diagnostics;

internal static class CompilerDiagnosticHelpers
{
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
