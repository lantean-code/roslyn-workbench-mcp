using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceLoader : IWorkspaceLoader
{
    private readonly IMsBuildWorkspaceFactory _workspaceFactory;
    private readonly IWorkspaceProjectCompatibilityInspector _compatibilityInspector;
    private readonly IWorkspacePathNormalizer _pathNormalizer;

    public WorkspaceLoader(
        IMsBuildWorkspaceFactory workspaceFactory,
        IWorkspaceProjectCompatibilityInspector compatibilityInspector,
        IWorkspacePathNormalizer pathNormalizer)
    {
        _workspaceFactory = workspaceFactory;
        _compatibilityInspector = compatibilityInspector;
        _pathNormalizer = pathNormalizer;
    }

    public string? NormalizeOpenPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || !Path.IsPathFullyQualified(path)
            || !_pathNormalizer.TryGetFullPath(path, out var normalizedPath))
        {
            return null;
        }

        var extension = Path.GetExtension(normalizedPath);
        return string.Equals(extension, ".sln", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".slnx", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase)
            ? normalizedPath
            : null;
    }

    public string? NormalizeAlias(string? alias)
    {
        return string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
    }

    public (bool IsSdkStyle, IReadOnlyList<DiagnosticInfo> Diagnostics) InspectCompatibility(string projectPath)
    {
        return _compatibilityInspector.Inspect(projectPath);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "MSBuild workspace loading is an external compatibility boundary; non-cancellation failures are returned as workspace diagnostics after the partially loaded workspace is disposed.")]
    public async ValueTask<WorkspaceLoadResult> LoadAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workspace = _workspaceFactory.Create();
        var diagnostics = new List<DiagnosticInfo>();

        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            diagnostics.Add(new DiagnosticInfo
            {
                Id = "WorkspaceLoad",
                Severity = args.Diagnostic.Kind == WorkspaceDiagnosticKind.Failure
                    ? Results.DiagnosticSeverity.Error
                    : Results.DiagnosticSeverity.Warning,
                Message = args.Diagnostic.Message,
            });
        });

        try
        {
            Solution solution;
            if (string.Equals(Path.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                var project = await workspace.OpenProjectAsync(
                    path,
                    cancellationToken: cancellationToken);

                solution = project.Solution;
            }
            else
            {
                solution = await workspace.OpenSolutionAsync(
                    path,
                    cancellationToken: cancellationToken);
            }

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
            Severity = Results.DiagnosticSeverity.Error,
            Message = message,
        };
    }
}
