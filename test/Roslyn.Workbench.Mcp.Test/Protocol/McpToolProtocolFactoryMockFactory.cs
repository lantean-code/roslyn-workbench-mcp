namespace Roslyn.Workbench.Mcp.TestSupport;

internal static class McpToolProtocolFactoryMockFactory
{
    public static Mock<IMcpToolProtocolFactory> Create()
    {
        var protocolFactory = new Mock<IMcpToolProtocolFactory>();
        protocolFactory.SetReturnsDefault(new Tool
        {
            Name = "Name",
        });
        return protocolFactory;
    }
}
