namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Creates bounded structured warnings for generated-looking checked-in source mutations.
/// </summary>
internal static class GeneratedSourceMutationWarnings
{
    /// <summary>
    /// Identifies warnings raised for generated-looking source mutations.
    /// </summary>
    public const string WarningCode = "GeneratedSourceMutation";

    private const int _displayPathLimit = 5;

    /// <summary>
    /// Creates a warning for the supplied generated-looking paths when any are present.
    /// </summary>
    /// <param name="paths">The canonical Workspace-relative paths.</param>
    /// <returns>A single bounded warning, or an empty collection when no path appears generated.</returns>
    public static IReadOnlyList<WarningInfo> Create(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return [];
        }

        var displayedPaths = string.Join(", ", paths.Take(_displayPathLimit).Select(static path => $"'{path}'"));
        var omittedPathCount = paths.Count - _displayPathLimit;
        var omittedSuffix = omittedPathCount > 0
            ? $" and {omittedPathCount} more"
            : string.Empty;

        return
        [
            new WarningInfo
            {
                Code = WarningCode,
                Message = $"The transaction changes generated-looking checked-in source: {displayedPaths}{omittedSuffix}. Review these files because regeneration may overwrite the changes.",
            },
        ];
    }
}
