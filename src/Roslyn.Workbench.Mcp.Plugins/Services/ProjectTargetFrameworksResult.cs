using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Services;

/// <summary>
/// Represents the result of evaluating a project's declared target frameworks.
/// </summary>
public sealed record ProjectTargetFrameworksResult
{
    /// <summary>
    /// Gets the evaluated target frameworks.
    /// </summary>
    public IReadOnlyList<string> TargetFrameworks { get; }

    /// <summary>
    /// Gets the evaluation failure message, when evaluation did not succeed.
    /// </summary>
    public string? ErrorMessage { get; }

    /// <summary>
    /// Gets a value indicating whether evaluation succeeded.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsSucceeded => ErrorMessage is null;

    private ProjectTargetFrameworksResult(
        IReadOnlyList<string> targetFrameworks,
        string? errorMessage)
    {
        TargetFrameworks = targetFrameworks;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    /// <param name="targetFrameworks">The evaluated target frameworks.</param>
    /// <returns>The successful result.</returns>
    public static ProjectTargetFrameworksResult Succeeded(IReadOnlyList<string>? targetFrameworks = null)
    {
        return new ProjectTargetFrameworksResult(targetFrameworks ?? [], errorMessage: null);
    }

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    /// <param name="errorMessage">The evaluation failure message.</param>
    /// <returns>The failed result.</returns>
    public static ProjectTargetFrameworksResult Failed(string errorMessage)
    {
        return new ProjectTargetFrameworksResult(targetFrameworks: [], errorMessage);
    }
}
