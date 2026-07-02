using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace;

internal static class MsBuildProjectUtilities
{
    public static IReadOnlyList<string> GetEvaluatedInputPaths(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath) || !File.Exists(projectPath))
        {
            return [];
        }

        try
        {
            using var projectCollection = new ProjectCollection();
            var project = projectCollection.LoadProject(projectPath);

            return project.Imports
                .Select(static import => import.ImportedProject?.FullPath)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Select(static path => Path.GetFullPath(path!))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    public static (bool IsSdkStyle, IReadOnlyList<DiagnosticInfo> Diagnostics) InspectCompatibility(string projectPath)
    {
        try
        {
            using var projectCollection = new ProjectCollection();
            var project = projectCollection.LoadProject(projectPath);
            var root = project.Xml;

            var isSdkStyle = !string.IsNullOrWhiteSpace(root.Sdk)
                || root.Children.OfType<ProjectSdkElement>().Any();
            return (isSdkStyle, []);
        }
        catch (Exception exception) when (exception is InvalidProjectFileException or IOException or UnauthorizedAccessException)
        {
            return (false, [CreateLoadDiagnostic(exception.Message)]);
        }
    }

    private static DiagnosticInfo CreateLoadDiagnostic(string message)
    {
        return new DiagnosticInfo
        {
            Id = "WorkspaceLoad",
            Severity = DiagnosticSeverity.Error,
            Message = message,
        };
    }
}
