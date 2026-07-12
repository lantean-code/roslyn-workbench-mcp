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

        var loadedWorkspace = await _workspaceLoader.LoadAsync(loadedPath, cancellationToken).ConfigureAwait(false);
        if (loadedWorkspace.Solution is null || loadedWorkspace.Workspace is null)
        {
            loadedWorkspace.Workspace?.Dispose();
            return ValidatedWorkspaceLoadResult.Failed(
                ValidatedWorkspaceLoadFailure.LoadFailed,
                loadedWorkspace.Diagnostics);
        }

        try
        {
            if (HasInputOutsideRoot(loadedWorkspace.Solution, workspaceRoot))
            {
                loadedWorkspace.Workspace.Dispose();
                return ValidatedWorkspaceLoadResult.Failed(ValidatedWorkspaceLoadFailure.OutsideWorkspaceRoot);
            }

            foreach (var projectPath in GetCSharpProjectPaths(loadedWorkspace.Solution))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var compatibilityFailure = InspectCompatibility(projectPath);
                if (compatibilityFailure is not null)
                {
                    loadedWorkspace.Workspace.Dispose();
                    return compatibilityFailure;
                }
            }

            return ValidatedWorkspaceLoadResult.Succeeded(
                loadedWorkspace.Workspace,
                loadedWorkspace.Solution,
                loadedWorkspace.Diagnostics);
        }
        catch
        {
            loadedWorkspace.Workspace.Dispose();
            throw;
        }
    }

    private bool HasInputOutsideRoot(Solution solution, string workspaceRoot)
    {
        return solution.Projects
            .SelectMany(static project => project.Documents
                .Select(static document => document.FilePath)
                .Prepend(project.FilePath))
            .OfType<string>()
            .Where(static path => !string.IsNullOrWhiteSpace(path))
            .Any(path => !_workspaceRootResolver.Contains(workspaceRoot, path));
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

    private static IEnumerable<string> GetCSharpProjectPaths(Solution solution)
    {
        return solution.Projects
            .Where(static project => string.Equals(project.Language, LanguageNames.CSharp, StringComparison.Ordinal))
            .Select(static project => project.FilePath)
            .OfType<string>()
            .Where(static path => !string.IsNullOrWhiteSpace(path));
    }

    private static bool IsProjectPath(string path)
    {
        return string.Equals(Path.GetExtension(path), ".csproj", StringComparison.OrdinalIgnoreCase);
    }
}
