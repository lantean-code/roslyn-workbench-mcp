namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal interface ICodeActionRuntime
{
    CodeActionRuntimeStatus Status { get; }

    HostServices? WorkspaceHostServices { get; }

    IReadOnlyList<CodeRefactoringProvider> RefactoringProviders { get; }

    IReadOnlyList<CodeFixProvider> CodeFixProviders { get; }

    TimeSpan TokenLifetime { get; }
}
