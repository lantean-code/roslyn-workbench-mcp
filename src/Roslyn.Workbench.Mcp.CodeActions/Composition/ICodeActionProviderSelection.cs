using System.Collections.Frozen;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal interface ICodeActionProviderSelection
{
    FrozenDictionary<string, CodeRefactoringProvider> RefactoringProviders { get; }

    FrozenDictionary<string, CodeFixProvider> CodeFixProviders { get; }
}
