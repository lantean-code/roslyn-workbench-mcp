namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Represents the composed code-action runtime used by the workspace host.
/// </summary>
internal sealed record CodeActionRuntime
    : ICodeActionRuntime
{
    /// <summary>
    /// Gets the current code-action component status.
    /// </summary>
    public ComponentStatus Status { get; init; } = null!;

    /// <summary>
    /// Gets the workspace host services used when opening workspaces.
    /// </summary>
    public HostServices? WorkspaceHostServices { get; init; }

    /// <summary>
    /// Gets the composed C# refactoring providers.
    /// </summary>
    public IReadOnlyList<CodeRefactoringProvider> RefactoringProviders { get; init; } = [];

    /// <summary>
    /// Gets the composed C# code-fix providers.
    /// </summary>
    public IReadOnlyList<CodeFixProvider> CodeFixProviders { get; init; } = [];

    /// <summary>
    /// Gets the lifetime used for encoded code-action tokens.
    /// </summary>
    public TimeSpan TokenLifetime { get; init; }
}
