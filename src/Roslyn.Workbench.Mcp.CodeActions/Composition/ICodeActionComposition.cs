namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Exposes Code Action composition status, Roslyn host services, and discovered providers.
/// </summary>
internal interface ICodeActionComposition
{
    /// <summary>
    /// Gets the availability and diagnostic status of composition.
    /// </summary>
    CodeActionCompositionStatus Status { get; }

    /// <summary>
    /// Gets the Roslyn host services created by successful composition.
    /// </summary>
    HostServices? WorkspaceHostServices { get; }

    /// <summary>
    /// Gets the composed C# refactoring providers.
    /// </summary>
    IReadOnlyList<CodeRefactoringProvider> RefactoringProviders { get; }

    /// <summary>
    /// Gets the composed C# Code Fix providers.
    /// </summary>
    IReadOnlyList<CodeFixProvider> CodeFixProviders { get; }
}
