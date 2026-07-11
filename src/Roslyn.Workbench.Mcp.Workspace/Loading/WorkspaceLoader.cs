using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceLoader : IWorkspaceLoader
{
    private readonly HostServices? _workspaceHostServices;

    public WorkspaceLoader(WorkspaceHostServicesAccessor workspaceHostServicesAccessor)
    {
        _workspaceHostServices = workspaceHostServicesAccessor.WorkspaceHostServices;
    }

    public string? NormalizeOpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
        {
            return null;
        }

        try
        {
            var normalizedPath = Path.GetFullPath(path);
            var extension = Path.GetExtension(normalizedPath);
            return string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase)
                || string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase)
                ? normalizedPath
                : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    public string? NormalizeAlias(string? alias)
    {
        return string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
    }

    public (bool IsSdkStyle, IReadOnlyList<DiagnosticInfo> Diagnostics) InspectCompatibility(string projectPath)
    {
        return MsBuildProjectUtilities.InspectCompatibility(projectPath);
    }

    public async ValueTask<WorkspaceLoadResult> LoadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workspace = _workspaceHostServices is null
            ? MSBuildWorkspace.Create()
            : MSBuildWorkspace.Create(_workspaceHostServices);
        var diagnostics = new List<DiagnosticInfo>();

        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            diagnostics.Add(new DiagnosticInfo
            {
                Id = "WorkspaceLoad",
                Severity = args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure
                    ? Contracts.Results.DiagnosticSeverity.Error
                    : Contracts.Results.DiagnosticSeverity.Warning,
                Message = args.Diagnostic.Message,
            });
        });

        try
        {
            var solution = string.Equals(Path.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase)
                ? (await workspace.OpenProjectAsync(path, cancellationToken: cancellationToken)).Solution
                : await workspace.OpenSolutionAsync(path, cancellationToken: cancellationToken);

            return new WorkspaceLoadResult
            {
                Workspace = new LoadedWorkspace(workspace),
                Solution = solution,
                Diagnostics = diagnostics,
            };
        }
        catch (OperationCanceledException)
        {
            workspace.Dispose();
            throw;
        }
        catch (Exception exception)
        {
            diagnostics.Add(CreateLoadDiagnostic(exception.Message));
            workspace.Dispose();
            return new WorkspaceLoadResult
            {
                Diagnostics = diagnostics,
            };
        }
    }

    private static DiagnosticInfo CreateLoadDiagnostic(string message)
    {
        return new DiagnosticInfo
        {
            Id = "WorkspaceLoad",
            Severity = Contracts.Results.DiagnosticSeverity.Error,
            Message = message,
        };
    }
}
