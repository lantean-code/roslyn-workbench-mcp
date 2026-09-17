using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Workspace.Transactions;

/// <summary>
/// Represents either a complete project compiler-error set or an incomplete compilation evaluation.
/// </summary>
internal sealed class ProjectCompilerDiagnostics
{
    /// <summary>
    /// Gets whether compiler diagnostics were evaluated completely.
    /// </summary>
    [MemberNotNullWhen(false, nameof(ErrorMessage))]
    public bool IsComplete { get; }

    /// <summary>
    /// Gets the compiler errors when evaluation completed.
    /// </summary>
    public IReadOnlyList<CachedCompilerDiagnostic> Diagnostics { get; }

    /// <summary>
    /// Gets the reason compiler evaluation was incomplete.
    /// </summary>
    public string? ErrorMessage { get; }

    private ProjectCompilerDiagnostics(
        bool isComplete,
        IReadOnlyList<CachedCompilerDiagnostic> diagnostics,
        string? errorMessage)
    {
        IsComplete = isComplete;
        Diagnostics = diagnostics;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// Creates a complete project compiler-error result.
    /// </summary>
    /// <param name="diagnostics">The evaluated compiler errors.</param>
    /// <returns>A complete result.</returns>
    public static ProjectCompilerDiagnostics Complete(IReadOnlyList<CachedCompilerDiagnostic> diagnostics)
    {
        return new ProjectCompilerDiagnostics(isComplete: true, diagnostics, errorMessage: null);
    }

    /// <summary>
    /// Creates an incomplete project compiler-error result.
    /// </summary>
    /// <param name="errorMessage">The reason evaluation was incomplete.</param>
    /// <returns>An incomplete result.</returns>
    public static ProjectCompilerDiagnostics Incomplete(string errorMessage)
    {
        return new ProjectCompilerDiagnostics(isComplete: false, diagnostics: [], errorMessage);
    }
}
