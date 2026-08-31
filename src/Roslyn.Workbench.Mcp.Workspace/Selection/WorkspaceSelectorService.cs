namespace Roslyn.Workbench.Mcp.Workspace.Selection;

/// <summary>
/// Selects a loaded workspace by identifier, alias, path, or unambiguous default.
/// </summary>
internal sealed class WorkspaceSelectorService : IWorkspaceSelector
{
    private const string _workspaceSelectorRequiredCode = "WorkspaceSelectorRequired";
    private const string _workspaceSelectorNotFoundCode = "WorkspaceSelectorNotFound";
    private const string _workspaceSelectorMismatchCode = "WorkspaceSelectorMismatch";
    private const string _workspaceSelectorInvalidCode = "WorkspaceSelectorInvalid";
    private readonly IWorkspacePathComparison _workspacePathComparison;
    private readonly IWorkspacePathNormalizer _pathNormalizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkspaceSelectorService"/> class.
    /// </summary>
    /// <param name="workspacePathComparison">The comparison rules used for workspace path.</param>
    /// <param name="pathNormalizer">The service used to normalize workspace paths.</param>
    public WorkspaceSelectorService(
        IWorkspacePathComparison workspacePathComparison,
        IWorkspacePathNormalizer pathNormalizer)
    {
        _workspacePathComparison = workspacePathComparison;
        _pathNormalizer = pathNormalizer;
    }

    /// <summary>
    /// Selects a workspace from the host snapshot using the supplied selector.
    /// </summary>
    /// <param name="hostSnapshot">The immutable host catalogue snapshot used for tool selection.</param>
    /// <param name="selector">The selector that identifies the requested workspace scope.</param>
    /// <returns>The selected session or a structured selection error.</returns>
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

        WorkspaceOperationError error;
        if (hostSnapshot.Workspaces.Count == 0)
        {
            error = CreateError(
                _workspaceSelectorNotFoundCode,
                "Open a workspace before invoking this tool.",
                RequiredAction.OpenWorkspace);
        }
        else
        {
            error = CreateError(
                _workspaceSelectorRequiredCode,
                "Select a workspace when more than one workspace is loaded.",
                RequiredAction.ResolveTargetAgain);
        }

        return WorkspaceSelectionResult.Failure(error);
    }

    private WorkspaceSelectionResult ResolveSelection(
        WorkspaceHostSnapshot hostSnapshot,
        WorkspaceSelector selector)
    {
        Guid? resolvedWorkspaceId = null;
        var hasMismatch = false;

        void MatchWorkspaceId(Guid candidateWorkspaceId)
        {
            if (resolvedWorkspaceId is null)
            {
                resolvedWorkspaceId = candidateWorkspaceId;
                return;
            }

            if (resolvedWorkspaceId != candidateWorkspaceId)
            {
                hasMismatch = true;
            }
        }

        var selectorWorkspaceId = selector.WorkspaceId;
        if (selectorWorkspaceId is not null)
        {
            if (!hostSnapshot.Workspaces.ContainsKey(selectorWorkspaceId.Value))
            {
                return CreateNotFoundResult();
            }

            MatchWorkspaceId(selectorWorkspaceId.Value);
        }

        if (!string.IsNullOrWhiteSpace(selector.Alias))
        {
            var aliasMatch = hostSnapshot.Workspaces.SingleOrDefault(pair =>
                string.Equals(pair.Value.Workspace.Alias, selector.Alias, StringComparison.Ordinal));

            if (aliasMatch.Value is null)
            {
                return CreateNotFoundResult();
            }

            MatchWorkspaceId(aliasMatch.Key);
        }

        var selectorPath = selector.Path;
        if (!string.IsNullOrWhiteSpace(selectorPath))
        {
            if (!Path.IsPathFullyQualified(selectorPath)
                || !_pathNormalizer.TryGetFullPath(selectorPath, out var normalizedPath))
            {
                return CreateInvalidPathResult();
            }

            var pathMatch = hostSnapshot.Workspaces.SingleOrDefault(pair =>
                PathsEqual(pair.Value.Workspace.LoadedPath, normalizedPath));

            if (pathMatch.Value is null)
            {
                return CreateNotFoundResult();
            }

            MatchWorkspaceId(pathMatch.Key);
        }

        if (resolvedWorkspaceId is null)
        {
            return CreateNotFoundResult();
        }

        if (hasMismatch)
        {
            var error = CreateError(
                _workspaceSelectorMismatchCode,
                "The workspace selector fields must resolve to the same loaded workspace.",
                RequiredAction.ResolveTargetAgain);

            return WorkspaceSelectionResult.Failure(error);
        }

        return CreateSuccess(resolvedWorkspaceId.Value, hostSnapshot.Workspaces[resolvedWorkspaceId.Value]);
    }

    private static WorkspaceSelectionResult CreateSuccess(Guid workspaceId, WorkspaceSessionSnapshot session)
    {
        var selection = new WorkspaceSelection
        {
            WorkspaceId = workspaceId,
            Session = session,
        };

        return WorkspaceSelectionResult.Success(selection);
    }

    private static WorkspaceSelectionResult CreateNotFoundResult()
    {
        var error = CreateError(
            _workspaceSelectorNotFoundCode,
            "The workspace selector did not match any loaded workspace.",
            RequiredAction.ResolveTargetAgain);

        return WorkspaceSelectionResult.Failure(error);
    }

    private static WorkspaceSelectionResult CreateInvalidPathResult()
    {
        var error = CreateError(
            _workspaceSelectorInvalidCode,
            "The workspace selector path must be a valid absolute path.",
            RequiredAction.ResolveTargetAgain);

        return WorkspaceSelectionResult.Failure(error);
    }

    private bool PathsEqual(string first, string second)
    {
        return string.Equals(first, second, _workspacePathComparison.GetComparison(first));
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
