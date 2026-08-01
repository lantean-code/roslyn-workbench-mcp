namespace Roslyn.Workbench.Mcp.Workspace.Selectors;

internal sealed class LocationSelectorValidator : IWorkspaceContractValidator<LocationSelector>
{
    public WorkspaceContractValidationResult Validate(LocationSelector selector)
    {
        if ((selector.Span is null) != (selector.Selection is null))
        {
            return WorkspaceContractValidationResult.Valid();
        }

        var memberNames = new[]
        {
            nameof(LocationSelector.Span),
            nameof(LocationSelector.Selection),
        };

        var failures = new[]
        {
            new WorkspaceContractValidationFailure(
                "LocationSelector must provide exactly one of Span or Selection.",
                memberNames),
        };

        return WorkspaceContractValidationResult.Invalid(failures);
    }
}
