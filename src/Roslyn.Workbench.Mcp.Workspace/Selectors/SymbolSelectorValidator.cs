namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

internal sealed class SymbolSelectorValidator : IWorkspaceContractValidator<SymbolSelector>
{
    public WorkspaceContractValidationResult Validate(SymbolSelector selector)
    {
        var providedCount = 0;
        if (selector.Location is not null)
        {
            providedCount++;
        }

        if (!string.IsNullOrWhiteSpace(selector.DocumentationCommentId))
        {
            providedCount++;
        }

        if (providedCount == 1)
        {
            return WorkspaceContractValidationResult.Valid();
        }

        var memberNames = new[]
        {
            nameof(SymbolSelector.Location),
            nameof(SymbolSelector.DocumentationCommentId),
        };

        var failures = new[]
        {
            new WorkspaceContractValidationFailure(
                "SymbolSelector must provide exactly one of Location or DocumentationCommentId.",
                memberNames),
        };

        return WorkspaceContractValidationResult.Invalid(failures);
    }
}
