using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Plugins.Analyzers;

internal sealed class MarkedPluginEntryPoint
{
    public INamedTypeSymbol Type { get; }

    public Location AttributeLocation { get; }

    public MarkedPluginEntryPoint(
        INamedTypeSymbol type,
        Location attributeLocation)
    {
        Type = type;
        AttributeLocation = attributeLocation;
    }
}
