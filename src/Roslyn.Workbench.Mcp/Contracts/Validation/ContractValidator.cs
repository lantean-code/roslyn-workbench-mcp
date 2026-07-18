namespace Roslyn.Workbench.Mcp.Protocol.Validation;

/// <summary>
/// Provides non-throwing validation for Host-owned MCP result-envelope invariants.
/// </summary>
internal static class ContractValidator
{
    /// <summary>
    /// Validates a tool result.
    /// </summary>
    /// <typeparam name="TData">The tool-specific payload type.</typeparam>
    /// <param name="result">The result to validate.</param>
    /// <returns>The validation errors, if any.</returns>
    public static IReadOnlyList<string> Validate<TData>(ToolResult<TData> result)
    {
        var errors = new List<string>();

        switch (result.Outcome)
        {
            case ToolOutcome.Succeeded:
                if (result.Data is null)
                {
                    errors.Add("ToolResult Succeeded outcome requires Data.");
                }
                break;

            case ToolOutcome.NoChange:
                if (result.Changes is not null)
                {
                    errors.Add("ToolResult NoChange outcome must not include Changes.");
                }

                if (result.Error is not null)
                {
                    errors.Add("ToolResult NoChange outcome must not include Error.");
                }
                break;

            case ToolOutcome.Rejected:
            case ToolOutcome.Conflict:
            case ToolOutcome.Faulted:
                if (result.Error is null)
                {
                    errors.Add($"ToolResult {result.Outcome} outcome requires Error.");
                }

                if (result.Data is not null)
                {
                    errors.Add($"ToolResult {result.Outcome} outcome must not include Data.");
                }

                if (result.Changes is not null)
                {
                    errors.Add($"ToolResult {result.Outcome} outcome must not include Changes.");
                }
                break;
        }

        return errors;
    }
}
