namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

internal sealed class DocumentSelectorValidator : IWorkspaceContractValidator<DocumentSelector>
{
    public WorkspaceContractValidationResult Validate(DocumentSelector selector)
    {
        var providedCount = 0;
        if (!string.IsNullOrWhiteSpace(selector.Path))
        {
            providedCount++;
        }

        if (!string.IsNullOrWhiteSpace(selector.DocumentId))
        {
            providedCount++;
        }

        if (providedCount == 1)
        {
            return WorkspaceContractValidationResult.Valid();
        }

        var memberNames = new[]
        {
            nameof(DocumentSelector.Path),
            nameof(DocumentSelector.DocumentId),
        };

        var failures = new[]
        {
            new WorkspaceContractValidationFailure(
                "DocumentSelector must provide exactly one of Path or DocumentId.",
                memberNames),
        };

        return WorkspaceContractValidationResult.Invalid(failures);
    }
}
