using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Lifecycle;

/// <summary>
/// Represents either the canonical inputs for opening a workspace or their validation error.
/// </summary>
internal sealed class ResolvedWorkspaceOpenRequest
{
    private ResolvedWorkspaceOpenRequest(
        string? loadedPath,
        string? alias,
        string? workspaceRoot,
        WorkspaceMsBuildProperties? msBuildProperties,
        WorkspaceOperationError? error)
    {
        LoadedPath = loadedPath;
        Alias = alias;
        WorkspaceRoot = workspaceRoot;
        MsBuildProperties = msBuildProperties;
        Error = error;
    }

    /// <summary>
    /// Gets the normalised caller-friendly alias when resolution succeeded.
    /// </summary>
    public string? Alias { get; }

    /// <summary>
    /// Gets the validation error when input resolution failed.
    /// </summary>
    public WorkspaceOperationError? Error { get; }

    /// <summary>
    /// Gets the canonical solution or project path when resolution succeeded.
    /// </summary>
    public string? LoadedPath { get; }

    /// <summary>
    /// Gets the normalised MSBuild properties when resolution succeeded.
    /// </summary>
    public WorkspaceMsBuildProperties? MsBuildProperties { get; }

    /// <summary>
    /// Gets the canonical workspace root when resolution succeeded.
    /// </summary>
    public string? WorkspaceRoot { get; }

    /// <summary>
    /// Gets a value indicating whether input resolution failed.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Error))]
    [MemberNotNullWhen(false, nameof(LoadedPath))]
    [MemberNotNullWhen(false, nameof(WorkspaceRoot))]
    public bool HasError => Error is not null;

    /// <summary>
    /// Creates a failed input resolution.
    /// </summary>
    /// <param name="error">The validation error.</param>
    /// <returns>The failed request resolution.</returns>
    public static ResolvedWorkspaceOpenRequest Failure(WorkspaceOperationError error)
    {
        return new ResolvedWorkspaceOpenRequest(
            loadedPath: null,
            alias: null,
            workspaceRoot: null,
            msBuildProperties: null,
            error);
    }

    /// <summary>
    /// Creates a successful input resolution.
    /// </summary>
    /// <param name="loadedPath">The canonical solution or project path.</param>
    /// <param name="alias">The normalised optional alias.</param>
    /// <param name="workspaceRoot">The canonical workspace root.</param>
    /// <param name="msBuildProperties">The normalised optional MSBuild properties.</param>
    /// <returns>The resolved open request.</returns>
    public static ResolvedWorkspaceOpenRequest Success(
        string loadedPath,
        string? alias,
        string workspaceRoot,
        WorkspaceMsBuildProperties? msBuildProperties)
    {
        return new ResolvedWorkspaceOpenRequest(
            loadedPath,
            alias,
            workspaceRoot,
            msBuildProperties,
            error: null);
    }
}
