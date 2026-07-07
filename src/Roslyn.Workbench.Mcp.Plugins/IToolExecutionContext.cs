using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Provides the host-owned context shared by every plugin tool invocation.
/// </summary>
public interface IToolExecutionContext
{
    /// <summary>
    /// Gets the effective immutable solution for this invocation.
    /// </summary>
    Solution CurrentSolution { get; }

    /// <summary>
    /// Gets the effective workspace identity, when available.
    /// </summary>
    WorkspaceIdentity WorkspaceIdentity { get; }

    /// <summary>
    /// Gets the effective transaction revision, when available.
    /// </summary>
    int? TransactionRevision { get; }

    /// <summary>
    /// Gets the default maximum collection size for bounded query operations.
    /// </summary>
    int DefaultMaxResults { get; }

    /// <summary>
    /// Gets the host-owned workspace resolution services for this invocation.
    /// </summary>
    IWorkspaceResolver WorkspaceResolver { get; }

    /// <summary>
    /// Gets the host-composed execution services available to plugin tools.
    /// </summary>
    IToolExecutionServices ToolExecutionServices { get; }
}
