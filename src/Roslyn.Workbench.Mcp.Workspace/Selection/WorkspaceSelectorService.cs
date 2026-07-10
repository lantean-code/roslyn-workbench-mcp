using Roslyn.Workbench.Mcp.Workspace.Contracts.Results;
using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Workspace.Selection;

internal sealed class WorkspaceSelectorService : IWorkspaceSelector
{
    private const string _workspaceSelectorRequiredCode = "WorkspaceSelectorRequired";
    private const string _workspaceSelectorNotFoundCode = "WorkspaceSelectorNotFound";
    private const string _workspaceSelectorMismatchCode = "WorkspaceSelectorMismatch";

    public WorkspaceSelectionResult Select(WorkspaceHostSnapshot hostSnapshot, WorkspaceSelector? selector)
    {
        ArgumentNullException.ThrowIfNull(hostSnapshot);

        if (selector is null)
        {
            if (hostSnapshot.Workspaces.Count == 1)
            {
                var pair = hostSnapshot.Workspaces.Single();
                return WorkspaceSelectionResult.Success(
                    new WorkspaceSelection
                    {
                        WorkspaceId = pair.Key,
                        Session = pair.Value,
                    });
            }

            return WorkspaceSelectionResult.Failure(
                hostSnapshot.Workspaces.Count == 0
                    ? CreateError(_workspaceSelectorNotFoundCode, "Open a workspace before invoking this tool.", RequiredAction.OpenWorkspace)
                    : CreateError(_workspaceSelectorRequiredCode, "Select a workspace when more than one workspace is loaded.", RequiredAction.ResolveTargetAgain));
        }

        var resolution = ResolveWorkspaceId(hostSnapshot, selector);
        if (resolution.Error is not null)
        {
            return WorkspaceSelectionResult.Failure(resolution.Error);
        }

        var workspaceId = resolution.WorkspaceId!;
        return WorkspaceSelectionResult.Success(
            new WorkspaceSelection
            {
                WorkspaceId = workspaceId,
                Session = hostSnapshot.Workspaces[workspaceId],
            });
    }

    private static (string? WorkspaceId, WorkspaceOperationError? Error) ResolveWorkspaceId(
        WorkspaceHostSnapshot hostSnapshot,
        WorkspaceSelector selector)
    {
        string? resolvedWorkspaceId = null;

        static bool IsProvided(string? value)
        {
            return !string.IsNullOrWhiteSpace(value);
        }

        void MatchWorkspaceId(string? candidateWorkspaceId)
        {
            if (candidateWorkspaceId is null)
            {
                return;
            }

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

        if (IsProvided(selector.WorkspaceId))
        {
            if (!hostSnapshot.Workspaces.ContainsKey(selector.WorkspaceId!))
            {
                return (null, CreateError(_workspaceSelectorNotFoundCode, "The workspace selector did not match any loaded workspace.", RequiredAction.ResolveTargetAgain));
            }

            MatchWorkspaceId(selector.WorkspaceId);
        }

        if (IsProvided(selector.Alias))
        {
            var aliasMatch = hostSnapshot.Workspaces.SingleOrDefault(pair => string.Equals(pair.Value.Workspace.Alias, selector.Alias, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(aliasMatch.Key))
            {
                return (null, CreateError(_workspaceSelectorNotFoundCode, "The workspace selector did not match any loaded workspace.", RequiredAction.ResolveTargetAgain));
            }

            MatchWorkspaceId(aliasMatch.Key);
        }

        if (IsProvided(selector.Path))
        {
            var normalizedPath = NormalizeSelectorPath(selector.Path!);
            var pathMatch = hostSnapshot.Workspaces.SingleOrDefault(pair => string.Equals(pair.Value.Workspace.LoadedPath, normalizedPath, StringComparison.Ordinal));
            if (string.IsNullOrEmpty(pathMatch.Key))
            {
                return (null, CreateError(_workspaceSelectorNotFoundCode, "The workspace selector did not match any loaded workspace.", RequiredAction.ResolveTargetAgain));
            }

            MatchWorkspaceId(pathMatch.Key);
        }

        if (resolvedWorkspaceId is null)
        {
            return (null, CreateError(_workspaceSelectorNotFoundCode, "The workspace selector did not match any loaded workspace.", RequiredAction.ResolveTargetAgain));
        }

        if (resolvedWorkspaceId.Length == 0)
        {
            return (null, CreateError(_workspaceSelectorMismatchCode, "The workspace selector fields must resolve to the same loaded workspace.", RequiredAction.ResolveTargetAgain));
        }

        return (resolvedWorkspaceId, null);
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
