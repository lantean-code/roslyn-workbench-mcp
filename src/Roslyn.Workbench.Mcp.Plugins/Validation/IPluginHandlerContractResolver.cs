using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Validation;

internal interface IPluginHandlerContractResolver
{
    bool TryResolve(
        ConfiguredToolDefinition definition,
        [NotNullWhen(true)] out Type? contract,
        [NotNullWhen(false)] out DiagnosticInfo? diagnostic);
}
