using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

/// <summary>
/// Associates a plugin entry-point type with the source location of its marker attribute.
/// </summary>
internal sealed class MarkedPluginEntryPoint
{
    /// <summary>
    /// Gets the marked plugin entry-point type.
    /// </summary>
    public INamedTypeSymbol Type { get; }

    /// <summary>
    /// Gets the marker attribute location used when reporting assembly-wide conflicts.
    /// </summary>
    public Location AttributeLocation { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkedPluginEntryPoint"/> class.
    /// </summary>
    /// <param name="type">The marked plugin entry-point type.</param>
    /// <param name="attributeLocation">The source location of the marker attribute.</param>
    public MarkedPluginEntryPoint(
        INamedTypeSymbol type,
        Location attributeLocation)
    {
        Type = type;
        AttributeLocation = attributeLocation;
    }
}
