using Microsoft.CodeAnalysis;

using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;

using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

internal sealed class WorkspaceQueryContext : IQueryContext
{
    public WorkspaceQueryContext(Solution currentSolution, WorkspaceIdentity? workspaceIdentity, int? transactionRevision, ResultLimit effectiveResultLimit, int maxResponseBytes, IWorkspaceResolver resolver, ICodeActionService codeActionService)
    {
        CurrentSolution = currentSolution;
        WorkspaceIdentity = workspaceIdentity;
        TransactionRevision = transactionRevision;
        EffectiveResultLimit = effectiveResultLimit;
        MaxResponseBytes = maxResponseBytes;
        Resolver = resolver;
        CodeActionService = codeActionService;
    }

    public Solution CurrentSolution { get; }

    public WorkspaceIdentity? WorkspaceIdentity { get; }

    public int? TransactionRevision { get; }

    public ResultLimit EffectiveResultLimit { get; }

    public int MaxResponseBytes { get; }

    public IWorkspaceResolver Resolver { get; }

    public ICodeActionService CodeActionService { get; }
}
