using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Validation;

/// <summary>
/// Resolves the single closed handler contract that agrees with a configured tool family.
/// </summary>
internal interface IPluginHandlerContractResolver
{
    /// <summary>
    /// Attempts to resolve and validate a configured handler's closed query or mutation contract.
    /// </summary>
    /// <param name="definition">The configured handler definition.</param>
    /// <param name="contractAccessibility">The accessibility required of serialized contract types.</param>
    /// <param name="contract">The resolved closed contract when validation succeeds.</param>
    /// <param name="diagnostic">The contract diagnostic when validation fails.</param>
    /// <returns><see langword="true"/> when exactly one valid contract is resolved; otherwise <see langword="false"/>.</returns>
    bool TryResolve(
        ConfiguredToolDefinition definition,
        PluginContractAccessibility contractAccessibility,
        [NotNullWhen(true)] out Type? contract,
        [NotNullWhen(false)] out DiagnosticInfo? diagnostic);
}
