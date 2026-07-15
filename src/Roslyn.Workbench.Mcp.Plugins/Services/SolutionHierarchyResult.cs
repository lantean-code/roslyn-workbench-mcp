using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Represents the result of loading solution-folder hierarchy information.
/// </summary>
public sealed record SolutionHierarchyResult
{
    /// <summary>
    /// Gets the solution folders.
    /// </summary>
    public IReadOnlyList<SolutionFolderInfo> Folders { get; init; } = [];

    /// <summary>
    /// Gets project paths mapped to their containing solution-folder paths.
    /// </summary>
    public IReadOnlyDictionary<string, string?> ProjectFolderPaths { get; init; } =
        new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>
    /// Gets the hierarchy-loading failure message, when loading did not succeed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets a value indicating whether hierarchy loading succeeded.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsSucceeded => ErrorMessage is null;

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="folders">The solution folders.</param>
    /// <param name="projectFolderPaths">Project paths mapped to their containing solution-folder paths.</param>
    /// <returns>The successful result.</returns>
    public static SolutionHierarchyResult Succeeded(
        IReadOnlyList<SolutionFolderInfo>? folders = null,
        IReadOnlyDictionary<string, string?>? projectFolderPaths = null)
    {
        return new SolutionHierarchyResult
        {
            Folders = folders ?? [],
            ProjectFolderPaths = projectFolderPaths ?? new Dictionary<string, string?>(StringComparer.Ordinal),
        };
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorMessage">The hierarchy-loading failure message.</param>
    /// <returns>The failed result.</returns>
    public static SolutionHierarchyResult Failed(string errorMessage)
    {
        return new SolutionHierarchyResult
        {
            ErrorMessage = errorMessage,
        };
    }
}
