namespace Roslyn.Workbench.Mcp.Plugins;

/// <summary>
/// Identifies an object-valued successful query response that can be published to an MCP client.
/// </summary>
/// <remarks>
/// Implementations must use an object JSON contract. Top-level scalar, collection, dictionary, and custom-converter contracts are not supported.
/// </remarks>
#pragma warning disable CA1040 // This marker constrains plugin query responses to Host-validated object-valued transport contracts.
public interface IQueryResponse
{
}
#pragma warning restore CA1040
