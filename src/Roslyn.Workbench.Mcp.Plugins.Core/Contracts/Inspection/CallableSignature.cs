namespace Roslyn.Workbench.Mcp.Plugins.Core.Contracts.Inspection;

/// <summary>
/// Represents one callable overload signature.
/// </summary>
internal sealed record CallableSignature
{
    /// <summary>
    /// Gets the callable display name.
    /// </summary>
    [Description("The callable display name.")]
    public required string DisplayName { get; init; }

    /// <summary>
    /// Gets the callable kind.
    /// </summary>
    [Description("The callable kind.")]
    public required string Kind { get; init; }

    /// <summary>
    /// Gets the ordered parameters for the callable.
    /// </summary>
    [Description("The ordered parameters for the callable.")]
    public IReadOnlyList<ParameterInfo> Parameters { get; init; } = [];

    /// <summary>
    /// Gets the return type, when applicable.
    /// </summary>
    [Description("The return type, when applicable.")]
    public TypeInfo? ReturnType { get; init; }
}
