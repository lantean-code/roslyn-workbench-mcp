namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Holds the result of Code Action composition and the providers available to the Host.
/// </summary>
internal sealed record CodeActionCompositionState
{
    /// <summary>
    /// Gets the availability and diagnostic status of composition.
    /// </summary>
    public CodeActionCompositionStatus Status { get; }

    /// <summary>
    /// Gets the Roslyn host services created by successful composition.
    /// </summary>
    public HostServices? WorkspaceHostServices { get; }

    /// <summary>
    /// Gets the composed C# refactoring providers.
    /// </summary>
    public IReadOnlyList<CodeRefactoringProvider> RefactoringProviders { get; }

    /// <summary>
    /// Gets the composed C# Code Fix providers.
    /// </summary>
    public IReadOnlyList<CodeFixProvider> CodeFixProviders { get; }

    private CodeActionCompositionState(
        CodeActionCompositionStatus status,
        HostServices? workspaceHostServices,
        IReadOnlyList<CodeRefactoringProvider> refactoringProviders,
        IReadOnlyList<CodeFixProvider> codeFixProviders)
    {
        Status = status;
        WorkspaceHostServices = workspaceHostServices;
        RefactoringProviders = refactoringProviders;
        CodeFixProviders = codeFixProviders;
    }

    /// <summary>
    /// Creates an available composition state containing the discovered providers.
    /// </summary>
    /// <param name="workspaceHostServices">The Roslyn host services used by the available composition.</param>
    /// <param name="refactoringProviders">The refactoring providers discovered by composition.</param>
    /// <param name="codeFixProviders">The Code Fix providers discovered by composition.</param>
    /// <param name="version">The Roslyn workspace assembly version.</param>
    /// <param name="message">A diagnostic summary of the composed providers.</param>
    /// <returns>The Code Action composition state.</returns>
    public static CodeActionCompositionState Available(
        HostServices workspaceHostServices,
        IReadOnlyList<CodeRefactoringProvider> refactoringProviders,
        IReadOnlyList<CodeFixProvider> codeFixProviders,
        string? version,
        string? message)
    {
        var status = CodeActionCompositionStatus.Available(version, message);

        return new CodeActionCompositionState(
            status,
            workspaceHostServices,
            refactoringProviders,
            codeFixProviders);
    }

    /// <summary>
    /// Creates an unavailable composition state without providers or host services.
    /// </summary>
    /// <param name="message">The message that describes the reported condition.</param>
    /// <returns>The Code Action composition state.</returns>
    public static CodeActionCompositionState Unavailable(string message)
    {
        var status = CodeActionCompositionStatus.Unavailable(message);

        return new CodeActionCompositionState(
            status,
            workspaceHostServices: null,
            refactoringProviders: [],
            codeFixProviders: []);
    }
}
