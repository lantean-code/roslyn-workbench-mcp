using System.Collections.Frozen;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

/// <summary>
/// Exposes composed Code Action providers that pass Host policy.
/// </summary>
internal interface ICodeActionProviderSelection
{
    /// <summary>
    /// Gets eligible refactoring providers keyed by stable provider identifier.
    /// </summary>
    FrozenDictionary<string, CodeRefactoringProvider> RefactoringProviders { get; }

    /// <summary>
    /// Gets eligible Code Fix providers keyed by stable provider identifier.
    /// </summary>
    FrozenDictionary<string, CodeFixProvider> CodeFixProviders { get; }
}
