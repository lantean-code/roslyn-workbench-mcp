using Roslyn.Workbench.Mcp.Contracts.Selectors;

namespace Roslyn.Workbench.Mcp.Contracts.Test.Schema;

public sealed class ContractSchemaProbeRequest
{
    public DocumentSelector? Document { get; set; }

    public LocationSelector? Location { get; set; }

    public ScopeSelector? Scope { get; set; }

    public SnapshotPrecondition? ExpectedSnapshot { get; set; }
}
