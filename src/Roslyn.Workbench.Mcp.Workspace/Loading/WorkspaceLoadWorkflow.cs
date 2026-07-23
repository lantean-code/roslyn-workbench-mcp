using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslyn.Workbench.Mcp.Workspace.Loading;

internal sealed class WorkspaceLoadWorkflow : IWorkspaceLoadWorkflow
{
    private readonly IWorkspaceLoader _workspaceLoader;
    private readonly IWorkspaceRootResolver _workspaceRootResolver;

    public WorkspaceLoadWorkflow(
        IWorkspaceLoader workspaceLoader,
        IWorkspaceRootResolver workspaceRootResolver)
    {
        _workspaceLoader = workspaceLoader;
        _workspaceRootResolver = workspaceRootResolver;
    }

    public async ValueTask<ValidatedWorkspaceLoadResult> LoadAsync(
        string loadedPath,
        string workspaceRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsProjectPath(loadedPath))
        {
            var preflightFailure = InspectCompatibility(loadedPath);
            if (preflightFailure is not null)
            {
                return preflightFailure;
            }
        }

        WorkspaceLoadResult loadedWorkspace;
        using (WorkbenchPerformanceEventSource.Log.StartPhase(
            "workspace-open",
            "msbuild-load"))
        {
            loadedWorkspace = await _workspaceLoader.LoadAsync(loadedPath, cancellationToken);
        }

        if (loadedWorkspace.Solution is null || loadedWorkspace.Workspace is null)
        {
            loadedWorkspace.Workspace?.Dispose();
            return ValidatedWorkspaceLoadResult.Failed(
                ValidatedWorkspaceLoadFailure.LoadFailed,
                loadedWorkspace.Diagnostics);
        }

        try
        {
            using var compatibilityPhase = WorkbenchPerformanceEventSource.Log.StartPhase(
                "workspace-open",
                WorkbenchPerformanceEventSource.WorkspaceCompatibilityPhase);

            var solution = loadedWorkspace.Solution;
            var diagnostics = new List<DiagnosticInfo>(loadedWorkspace.Diagnostics);
            var unsupportedProjectIds = new List<ProjectId>();
            var hasCompatibilityFailure = false;

            foreach (var project in solution.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!string.Equals(project.Language, LanguageNames.CSharp, StringComparison.Ordinal))
                {
                    unsupportedProjectIds.Add(project.Id);
                    diagnostics.Add(CreateSkippedProjectDiagnostic(project, $"language '{project.Language}' is not supported"));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(project.FilePath))
                {
                    unsupportedProjectIds.Add(project.Id);
                    diagnostics.Add(CreateSkippedProjectDiagnostic(project, "its project file path is unavailable"));
                    continue;
                }

                var compatibility = _workspaceLoader.InspectCompatibility(project.FilePath);
                if (compatibility.Diagnostics.Count > 0)
                {
                    unsupportedProjectIds.Add(project.Id);
                    diagnostics.AddRange(compatibility.Diagnostics);
                    hasCompatibilityFailure = true;
                    continue;
                }

                if (!compatibility.IsSdkStyle)
                {
                    unsupportedProjectIds.Add(project.Id);
                    diagnostics.Add(CreateSkippedProjectDiagnostic(project, "it is not SDK-style"));
                }
            }

            foreach (var projectId in unsupportedProjectIds)
            {
                solution = solution.RemoveProject(projectId);
            }

            if (!solution.Projects.Any())
            {
                loadedWorkspace.Workspace.Dispose();
                return ValidatedWorkspaceLoadResult.Failed(
                    hasCompatibilityFailure
                        ? ValidatedWorkspaceLoadFailure.LoadFailed
                        : ValidatedWorkspaceLoadFailure.NotSupported,
                    diagnostics);
            }

            solution = RemoveUnresolvedAnalyzerReferences(solution, diagnostics, cancellationToken);

            var outsideRootInput = FindInputOutsideRoot(solution, workspaceRoot);
            if (outsideRootInput is not null)
            {
                diagnostics.Add(new DiagnosticInfo
                {
                    Id = "WorkspaceInputOutsideRoot",
                    Severity = Contracts.Results.DiagnosticSeverity.Error,
                    Message = $"Loaded workspace input '{outsideRootInput}' is outside the workspace root '{workspaceRoot}'.",
                });

                loadedWorkspace.Workspace.Dispose();
                return ValidatedWorkspaceLoadResult.Failed(
                    ValidatedWorkspaceLoadFailure.OutsideWorkspaceRoot,
                    diagnostics);
            }

            cancellationToken.ThrowIfCancellationRequested();

            return ValidatedWorkspaceLoadResult.Succeeded(
                loadedWorkspace.Workspace,
                solution,
                diagnostics);
        }
        catch
        {
            loadedWorkspace.Workspace.Dispose();
            throw;
        }
    }

    private string? FindInputOutsideRoot(Solution solution, string workspaceRoot)
    {
        foreach (var project in solution.Projects)
        {
            if (!string.IsNullOrWhiteSpace(project.FilePath)
                && !_workspaceRootResolver.Contains(workspaceRoot, project.FilePath))
            {
                return project.FilePath;
            }

            foreach (var document in project.Documents)
            {
                if (!string.IsNullOrWhiteSpace(document.FilePath)
                    && !_workspaceRootResolver.Contains(workspaceRoot, document.FilePath))
                {
                    return document.FilePath;
                }
            }
        }

        return null;
    }

    private ValidatedWorkspaceLoadResult? InspectCompatibility(string projectPath)
    {
        var compatibility = _workspaceLoader.InspectCompatibility(projectPath);
        if (compatibility.Diagnostics.Count > 0)
        {
            return ValidatedWorkspaceLoadResult.Failed(
                ValidatedWorkspaceLoadFailure.LoadFailed,
                compatibility.Diagnostics);
        }

        return compatibility.IsSdkStyle
            ? null
            : ValidatedWorkspaceLoadResult.Failed(ValidatedWorkspaceLoadFailure.NotSupported);
    }

    private static Solution RemoveUnresolvedAnalyzerReferences(
        Solution solution,
        List<DiagnosticInfo> diagnostics,
        CancellationToken cancellationToken)
    {
        foreach (var analyzerReference in solution.AnalyzerReferences.OfType<UnresolvedAnalyzerReference>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            solution = solution.RemoveAnalyzerReference(analyzerReference);
            diagnostics.Add(CreateSkippedAnalyzerDiagnostic(analyzerReference, projectName: null));
        }

        foreach (var project in solution.Projects)
        {
            foreach (var analyzerReference in project.AnalyzerReferences.OfType<UnresolvedAnalyzerReference>())
            {
                cancellationToken.ThrowIfCancellationRequested();
                solution = solution.RemoveAnalyzerReference(project.Id, analyzerReference);
                diagnostics.Add(CreateSkippedAnalyzerDiagnostic(analyzerReference, project.Name));
            }
        }

        return solution;
    }

    private static DiagnosticInfo CreateSkippedAnalyzerDiagnostic(
        UnresolvedAnalyzerReference analyzerReference,
        string? projectName)
    {
        var owner = projectName is null ? "the solution" : $"project '{projectName}'";
        var display = analyzerReference.Display ?? analyzerReference.GetType().Name;

        return new DiagnosticInfo
        {
            Id = "WorkspaceAnalyzerReferenceSkipped",
            Severity = Contracts.Results.DiagnosticSeverity.Warning,
            Message = $"Analyzer reference '{display}' was skipped from {owner} because it could not be resolved.",
        };
    }

    private static DiagnosticInfo CreateSkippedProjectDiagnostic(Project project, string reason)
    {
        return new DiagnosticInfo
        {
            Id = "WorkspaceProjectSkipped",
            Severity = Contracts.Results.DiagnosticSeverity.Warning,
            Message = $"Project '{project.Name}' was skipped because {reason}.",
        };
    }

    private static bool IsProjectPath(string path)
    {
        return string.Equals(Path.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase);
    }
}
