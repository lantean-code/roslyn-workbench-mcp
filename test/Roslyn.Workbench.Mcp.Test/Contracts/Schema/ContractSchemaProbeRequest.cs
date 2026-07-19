namespace Roslyn.Workbench.Mcp.Test.Contracts.Schema;

#pragma warning disable CA1515 // The MCP SDK reflects this public request through ContractSchemaTestTools.SchemaProbe.
public sealed class ContractSchemaProbeRequest
{
    public DocumentSelector? Document { get; set; }

    public LocationSelector? Location { get; set; }

    public ScopeSelector? Scope { get; set; }

    public SnapshotPrecondition? ExpectedSnapshot { get; set; }
}
#pragma warning restore CA1515
