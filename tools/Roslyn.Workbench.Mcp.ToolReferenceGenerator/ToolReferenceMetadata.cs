namespace Roslyn.Workbench.Mcp.ToolReferenceGenerator;

/// <summary>
/// Maps the fixed built-in tool surface to stable documentation classifications.
/// </summary>
internal static class ToolReferenceMetadata
{
    private static readonly IReadOnlySet<string> _codeActionTools = new HashSet<string>(StringComparer.Ordinal)
    {
        "list-code-actions",
        "prepare-fix-all",
        "stage-code-action",
    };

    /// <summary>
    /// Determines the publication area for a built-in tool.
    /// </summary>
    /// <param name="name">The protocol tool name.</param>
    /// <returns>The stable machine-readable area.</returns>
    public static string GetArea(string name)
    {
        if (ServerOwnedToolRegistration.ToolNames.Contains(name))
        {
            return "Server";
        }

        return _codeActionTools.Contains(name) ? "CodeAction" : "CorePlugin";
    }

    /// <summary>
    /// Determines the user-facing category for a built-in tool.
    /// </summary>
    /// <param name="name">The protocol tool name.</param>
    /// <returns>The documentation category.</returns>
    public static string GetCategory(string name)
    {
        return name switch
        {
            "server-status" => "Server lifecycle",
            "workspace-close" or "workspace-list" or "workspace-open" or "workspace-reload" or "workspace-status" => "Workspaces",
            "transaction-commit" or "transaction-history" or "transaction-preview" or "transaction-rollback" or "transaction-start" => "Transactions",
            "get-error-details" or "prepare-error-report" or "submit-error-report" => "Error reporting",
            "list-code-actions" or "prepare-fix-all" or "stage-code-action" => "Code Actions",
            "format-document" or "rename-symbol" => "Code mutation",
            "analyze-async" or "analyze-control-flow" or "analyze-data-flow" or "analyze-disposables" or "analyze-nullability" or "find-dependency-cycles" or "find-duplicate-code" or "find-unused-symbols" or "get-change-impact" or "get-control-flow-graph" or "get-dependency-graph" or "get-diagnostics" or "get-document-options" or "get-operation-tree" or "get-symbol-dependencies" or "get-symbol-dependents" or "get-test-impact" => "Code analysis",
            "find-callees" or "find-callers" or "find-derived-types" or "find-implementations" or "find-overloads" or "find-overrides" or "find-references" or "get-api-surface" or "get-code-context" or "get-document-outline" or "get-partial-declarations" or "get-project-details" or "get-solution-structure" or "get-symbol-attributes" or "get-symbol-info" or "get-symbol-members" or "get-type-hierarchy" or "go-to-definition" or "resolve-symbol" or "search-symbols" => "Code discovery and navigation",
            _ => throw new InvalidOperationException($"Tool '{name}' does not have a documentation category."),
        };
    }

    /// <summary>
    /// Describes the configuration that controls publication of a built-in tool.
    /// </summary>
    /// <param name="name">The protocol tool name.</param>
    /// <returns>The availability statement.</returns>
    public static string GetAvailability(string name)
    {
        return name is "prepare-error-report" or "submit-error-report"
            ? "Published when external error-report consent is configured as Prompt or Always."
            : "Built in and published by default.";
    }
}
