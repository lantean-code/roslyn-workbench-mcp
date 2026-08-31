using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

/// <summary>
/// Opens MSBuild workspaces and captures Roslyn load diagnostics and target-framework identities.
/// </summary>
internal sealed class WorkspaceLoader : IWorkspaceLoader
{
    private readonly IMsBuildWorkspaceFactory _workspaceFactory;
    private readonly IWorkspaceProjectCompatibilityInspector _compatibilityInspector;
    private readonly IWorkspacePathComparison _pathComparison;
    private readonly IWorkspacePathNormalizer _pathNormalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceLoader"/> class.
    /// </summary>
    /// <param name="workspaceFactory">The factory that creates configured MSBuild workspaces.</param>
    /// <param name="compatibilityInspector">The service that evaluates project compatibility.</param>
    /// <param name="pathComparison">The platform-aware path comparison service.</param>
    /// <param name="pathNormalizer">The service that canonicalises workspace paths.</param>
    public WorkspaceLoader(
        IMsBuildWorkspaceFactory workspaceFactory,
        IWorkspaceProjectCompatibilityInspector compatibilityInspector,
        IWorkspacePathComparison pathComparison,
        IWorkspacePathNormalizer pathNormalizer)
    {
        _workspaceFactory = workspaceFactory;
        _compatibilityInspector = compatibilityInspector;
        _pathComparison = pathComparison;
        _pathNormalizer = pathNormalizer;
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    public string? NormalizeAlias(string? alias)
    {
        return string.IsNullOrWhiteSpace(alias) ? null : alias.Trim();
    }

    /// <inheritdoc/>
    public (bool IsSdkStyle, IReadOnlyList<DiagnosticInfo> Diagnostics) InspectCompatibility(
        string projectPath,
        WorkspaceMsBuildProperties? msBuildProperties)
    {
        return _compatibilityInspector.Inspect(projectPath, msBuildProperties);
    }

    /// <inheritdoc/>
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "MSBuild workspace loading is an external compatibility boundary; non-cancellation failures are returned as workspace diagnostics after the partially loaded workspace is disposed.")]
    public async ValueTask<WorkspaceLoadResult> LoadAsync(
        string path,
        WorkspaceMsBuildProperties? msBuildProperties,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var globalProperties = msBuildProperties?.ToGlobalProperties();
        var workspace = _workspaceFactory.Create(globalProperties);
        var targetFrameworkCollector = new WorkspaceProjectTargetFrameworkCollector(_pathComparison);
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
                    progress: targetFrameworkCollector,
                    cancellationToken: cancellationToken);

                solution = project.Solution;
            }
            else
            {
                solution = await workspace.OpenSolutionAsync(
                    path,
                    progress: targetFrameworkCollector,
                    cancellationToken: cancellationToken);
            }

            var projectTargetFrameworks = targetFrameworkCollector.CreateMap(solution);

            return new WorkspaceLoadResult
            {
                Workspace = new LoadedWorkspace(workspace),
                Solution = solution,
                ProjectTargetFrameworks = projectTargetFrameworks,
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
