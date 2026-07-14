using System.Reflection;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed class CodeActionCompositionOptions
{
    public bool IncludeBuiltInAssemblies { get; set; } = true;

    public IReadOnlyList<Assembly> AdditionalAssemblies { get; set; } = [];
}
