namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one callable overload signature.
/// </summary>
public sealed record CallableSignature
{
    /// <summary>
    /// Gets the callable display name.
    /// </summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// Gets the callable kind.
    /// </summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>
    /// Gets the ordered parameters for the callable.
    /// </summary>
    public IReadOnlyList<ParameterInfo> Parameters { get; init; } = [];

    /// <summary>
    /// Gets the return type, when applicable.
    /// </summary>
    public TypeInfo? ReturnType { get; init; }
}
