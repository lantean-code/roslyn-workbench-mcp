namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Collections;

internal static class InspectionContractValidator
{
    public static IReadOnlyList<string> Validate(CollectionLimit limit)
    {
        return limit.MaxResults is null or >= 0
            ? []
            : ["CollectionLimit MaxResults must be zero or greater when provided."];
    }
}
