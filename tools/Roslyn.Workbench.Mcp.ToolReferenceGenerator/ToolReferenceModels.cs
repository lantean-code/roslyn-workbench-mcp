using System.Text.Json.Nodes;

namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Describes one production MCP tool and the documentation metadata associated with it.
/// </summary>
internal sealed class ToolReferenceEntry
{
    /// <summary>
    /// Gets the protocol tool name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the human-readable tool title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the owning publication area.
    /// </summary>
    public required string Area { get; init; }

    /// <summary>
    /// Gets the user-facing documentation category.
    /// </summary>
    public required string Category { get; init; }

    /// <summary>
    /// Gets whether the tool is a query or mutation.
    /// </summary>
    public required string OperationKind { get; init; }

    /// <summary>
    /// Gets the concise purpose published for the tool.
    /// </summary>
    public required string Summary { get; init; }

    /// <summary>
    /// Gets the conditions under which the tool is published.
    /// </summary>
    public required string Availability { get; init; }

    /// <summary>
    /// Gets the exact production MCP tool definition.
    /// </summary>
    public required JsonObject ProtocolTool { get; init; }

    /// <summary>
    /// Gets the canonical examples that invoke this tool.
    /// </summary>
    public required IReadOnlyList<ToolReferenceExample> Examples { get; init; }
}

/// <summary>
/// Captures one canonical tool request within a documented workflow.
/// </summary>
internal sealed class ToolReferenceExample
{
    /// <summary>
    /// Gets the stable workflow identifier that groups related calls.
    /// </summary>
    public required string WorkflowId { get; init; }

    /// <summary>
    /// Gets the human-readable workflow title.
    /// </summary>
    public required string WorkflowTitle { get; init; }

    /// <summary>
    /// Gets the one-based position of this call within its workflow.
    /// </summary>
    public required int Step { get; init; }

    /// <summary>
    /// Gets the stable example identifier.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the example title.
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Gets the reason an agent would make this call.
    /// </summary>
    public required string Purpose { get; init; }

    /// <summary>
    /// Gets the protocol tool name used by the example.
    /// </summary>
    public required string Tool { get; init; }

    /// <summary>
    /// Gets the request arguments validated against the production input schema.
    /// </summary>
    public required JsonObject Request { get; init; }

    /// <summary>
    /// Gets the result or next action the example is intended to demonstrate.
    /// </summary>
    public required string ExpectedOutcome { get; init; }

    /// <summary>
    /// Gets an optional partial response used to illustrate relevant result fields.
    /// </summary>
    public JsonObject? RepresentativeResponse { get; init; }
}

/// <summary>
/// Holds build identity values embedded in the compiled Host assembly.
/// </summary>
internal sealed class ToolReferenceBuildIdentity
{
    /// <summary>
    /// Gets the public product version.
    /// </summary>
    public required string ProductVersion { get; init; }

    /// <summary>
    /// Gets the immutable source tag used by documentation links.
    /// </summary>
    public required string SourceTag { get; init; }

    /// <summary>
    /// Gets the source commit represented by the build.
    /// </summary>
    public required string Commit { get; init; }
}
