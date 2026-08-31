namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Represents exclusive cross-process ownership of a workspace commit lock until disposal.
/// </summary>
internal interface IWorkspaceCommitLock : IDisposable;
