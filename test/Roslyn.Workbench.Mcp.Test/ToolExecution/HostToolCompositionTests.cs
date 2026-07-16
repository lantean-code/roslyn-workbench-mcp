using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

public sealed class HostToolCompositionTests
{
    [Fact]
    public async Task GIVEN_CompleteHostComposition_WHEN_ValidatingContainer_THEN_ShouldResolveEveryRegisteredMcpTool()
    {
        var builder = Host.CreateApplicationBuilder();
        _ = builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);

        await using var serviceProvider = builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });
        var pluginCatalog = serviceProvider.GetRequiredService<PluginCatalogSnapshot>();
        var codeActionCatalog = serviceProvider.GetRequiredService<CodeActionCatalogSnapshot>();
        var mcpServerOptions = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var tools = serviceProvider.GetServices<McpServerTool>().ToArray();

        tools.Should().HaveCount(
            pluginCatalog.Tools.Count
            + codeActionCatalog.Tools.Count
            + ServerOwnedToolRegistration.ToolCount);
        tools.Select(static tool => tool.ProtocolTool.Name).Should().OnlyHaveUniqueItems();
        mcpServerOptions.Filters.Request.CallToolFilters.Should().ContainSingle();

        var server = new Mock<McpServer>();
        server.SetupGet(item => item.Services).Returns(serviceProvider);
        var context = new RequestContext<CallToolRequestParams>(
            server.Object,
            new JsonRpcRequest
            {
                Method = "tools/call",
            },
            new CallToolRequestParams
            {
                Name = "tool-name",
            });
        var expected = new CallToolResult
        {
            Content = [],
        };
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) => ValueTask.FromResult(expected);
        var result = await mcpServerOptions.Filters.Request.CallToolFilters.Single()(next)(context, CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GIVEN_CallToolFilterWithoutRequestServices_WHEN_InvokingFilter_THEN_ShouldRejectInvalidServerComposition()
    {
        var builder = Host.CreateApplicationBuilder();
        _ = builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);
        await using var serviceProvider = builder.Services.BuildServiceProvider();
        var mcpServerOptions = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
        var context = new RequestContext<CallToolRequestParams>(
            new Mock<McpServer>().Object,
            new JsonRpcRequest
            {
                Method = "tools/call",
            },
            new CallToolRequestParams
            {
                Name = "tool-name",
            });
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromResult(new CallToolResult());

        var action = async () => await mcpServerOptions.Filters.Request.CallToolFilters.Single()(next)(
            context,
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }
}
