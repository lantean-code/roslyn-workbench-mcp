namespace Roslyn.Workbench.Mcp.CodeActions.Resolution.Descriptors;

internal sealed record CodeActionProviderCapability
{
    public required bool ShouldDiscover { get; init; }

    public required CodeActionDescriptorEntry Descriptor { get; init; }

    public bool RequiresActionResolution { get; init; }
}
