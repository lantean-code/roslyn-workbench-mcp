namespace Roslyn.Workbench.Mcp.Plugins.Test;

[Trait("Category", "Contract")]
public sealed class PluginSurfaceShapeTests
{
    [Fact]
    public void GIVEN_PluginsAssembly_WHEN_LoadingPublicPluginSurface_THEN_ShouldExposePluginContracts()
    {
        typeof(IRoslynPlugin).FullName.Should().Be("Roslyn.Workbench.Mcp.Plugins.IRoslynPlugin");
        typeof(IPluginRegistry).FullName.Should().Be("Roslyn.Workbench.Mcp.Plugins.IPluginRegistry");
        typeof(IQueryContext).FullName.Should().Be("Roslyn.Workbench.Mcp.Plugins.IQueryContext");
        typeof(IMutationContext).FullName.Should().Be("Roslyn.Workbench.Mcp.Plugins.IMutationContext");
        typeof(MutationProposal).FullName.Should().Be("Roslyn.Workbench.Mcp.Plugins.MutationProposal");
        typeof(RegisteredTool).FullName.Should().Be("Roslyn.Workbench.Mcp.Plugins.Execution.RegisteredTool");
    }
}
