namespace Roslyn.Workbench.Mcp.CodeActions.References;

internal sealed class CodeActionReferenceCacheOptions
{
    public long SizeLimit { get; set; } = 75_000;
}
