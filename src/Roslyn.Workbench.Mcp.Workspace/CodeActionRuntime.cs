using Roslyn.Workbench.Mcp.Plugins;

namespace Roslyn.Workbench.Mcp.Workspace;

/// <summary>
/// Represents the composed code-action runtime used by the workspace host.
/// </summary>
public sealed record CodeActionRuntime
{
    /// <summary>
    /// Gets the code-action service exposed to tool execution contexts.
    /// </summary>
    public ICodeActionService CodeActionService { get; init; } = null!;

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
}
