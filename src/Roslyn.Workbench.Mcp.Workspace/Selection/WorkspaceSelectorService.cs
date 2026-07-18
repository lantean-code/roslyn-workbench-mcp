namespace Roslyn.Workbench.Mcp.Workspace.Selection;

internal sealed class WorkspaceSelectorService : IWorkspaceSelector
{
    private const string _workspaceSelectorRequiredCode = "WorkspaceSelectorRequired";
    private const string _workspaceSelectorNotFoundCode = "WorkspaceSelectorNotFound";
    private const string _workspaceSelectorMismatchCode = "WorkspaceSelectorMismatch";

    public WorkspaceSelectionResult Select(WorkspaceHostSnapshot hostSnapshot, WorkspaceSelector? selector)
    {
        if (selector is not null)
        {
            return ResolveSelection(hostSnapshot, selector);
        }

        if (hostSnapshot.Workspaces.Count == 1)
        {
            var pair = hostSnapshot.Workspaces.Single();
            return CreateSuccess(pair.Key, pair.Value);
        }

        return WorkspaceSelectionResult.Failure(
            hostSnapshot.Workspaces.Count == 0
                ? CreateError(_workspaceSelectorNotFoundCode, "Open a workspace before invoking this tool.", RequiredAction.OpenWorkspace)
                : CreateError(_workspaceSelectorRequiredCode, "Select a workspace when more than one workspace is loaded.", RequiredAction.ResolveTargetAgain));
    }

    private static WorkspaceSelectionResult ResolveSelection(
        WorkspaceHostSnapshot hostSnapshot,
        WorkspaceSelector selector)
    {
        string? resolvedWorkspaceId = null;

        void MatchWorkspaceId(string candidateWorkspaceId)
        {
            if (resolvedWorkspaceId is null)
            {
                resolvedWorkspaceId = candidateWorkspaceId;
                return;
            }

            if (!string.Equals(resolvedWorkspaceId, candidateWorkspaceId, StringComparison.Ordinal))
            {
                resolvedWorkspaceId = string.Empty;
            }
        }

        var selectorWorkspaceId = selector.WorkspaceId;
        if (!string.IsNullOrWhiteSpace(selectorWorkspaceId))
        {
            if (!hostSnapshot.Workspaces.ContainsKey(selectorWorkspaceId))
            {
                return CreateNotFoundResult();
            }

            MatchWorkspaceId(selectorWorkspaceId);
        }

        if (!string.IsNullOrWhiteSpace(selector.Alias))
        {
            var aliasMatch = hostSnapshot.Workspaces.SingleOrDefault(pair =>
                string.Equals(pair.Value.Workspace.Alias, selector.Alias, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(aliasMatch.Key))
            {
                return CreateNotFoundResult();
            }

            MatchWorkspaceId(aliasMatch.Key);
        }

        var selectorPath = selector.Path;
        if (!string.IsNullOrWhiteSpace(selectorPath))
        {
            var normalizedPath = NormalizeSelectorPath(selectorPath);
            var pathMatch = hostSnapshot.Workspaces.SingleOrDefault(pair =>
                string.Equals(pair.Value.Workspace.LoadedPath, normalizedPath, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(pathMatch.Key))
            {
                return CreateNotFoundResult();
            }

            MatchWorkspaceId(pathMatch.Key);
        }

        if (resolvedWorkspaceId is null)
        {
            return CreateNotFoundResult();
        }

        if (resolvedWorkspaceId.Length == 0)
        {
            return WorkspaceSelectionResult.Failure(CreateError(
                _workspaceSelectorMismatchCode,
                "The workspace selector fields must resolve to the same loaded workspace.",
                RequiredAction.ResolveTargetAgain));
        }

        return CreateSuccess(resolvedWorkspaceId, hostSnapshot.Workspaces[resolvedWorkspaceId]);
    }

    private static WorkspaceSelectionResult CreateSuccess(string workspaceId, WorkspaceSessionSnapshot session)
    {
        return WorkspaceSelectionResult.Success(new WorkspaceSelection
        {
            WorkspaceId = workspaceId,
            Session = session,
        });
    }

    private static WorkspaceSelectionResult CreateNotFoundResult()
    {
        return WorkspaceSelectionResult.Failure(CreateError(
            _workspaceSelectorNotFoundCode,
            "The workspace selector did not match any loaded workspace.",
            RequiredAction.ResolveTargetAgain));
    }

    private static string NormalizeSelectorPath(string path)
    {
        return Path.IsPathRooted(path) ? Path.GetFullPath(path) : path;
    }

    private static WorkspaceOperationError CreateError(string code, string message, RequiredAction? requiredAction)
    {
        return new WorkspaceOperationError
        {
            Code = code,
            Message = message,
            RequiredAction = requiredAction,
        };
    }
}
