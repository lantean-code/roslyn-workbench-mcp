using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

internal sealed class ResolvedWorkspaceOpenRequest
{
    private ResolvedWorkspaceOpenRequest(
        string? loadedPath,
        string? alias,
        string? workspaceRoot,
        WorkspaceOperationError? error)
    {
        LoadedPath = loadedPath;
        Alias = alias;
        WorkspaceRoot = workspaceRoot;
        Error = error;
    }

    public string? Alias { get; }

    public WorkspaceOperationError? Error { get; }

    public string? LoadedPath { get; }

    public string? WorkspaceRoot { get; }

    [MemberNotNullWhen(true, nameof(Error))]
    [MemberNotNullWhen(false, nameof(LoadedPath))]
    [MemberNotNullWhen(false, nameof(WorkspaceRoot))]
    public bool HasError => Error is not null;

    public static ResolvedWorkspaceOpenRequest Failure(WorkspaceOperationError error)
    {
        return new ResolvedWorkspaceOpenRequest(
            loadedPath: null,
            alias: null,
            workspaceRoot: null,
            error);
    }

    public static ResolvedWorkspaceOpenRequest Success(string loadedPath, string? alias, string workspaceRoot)
    {
        return new ResolvedWorkspaceOpenRequest(
            loadedPath,
            alias,
            workspaceRoot,
            error: null);
    }
}
