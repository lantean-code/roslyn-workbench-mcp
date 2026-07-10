using Roslyn.Workbench.Mcp.Workspace.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Test.Contracts.Schema;

public sealed class ContractSchemaProbeRequest
{
    public DocumentSelector? Document { get; set; }

    public LocationSelector? Location { get; set; }

    public ScopeSelector? Scope { get; set; }

    public SnapshotPrecondition? ExpectedSnapshot { get; set; }
}
