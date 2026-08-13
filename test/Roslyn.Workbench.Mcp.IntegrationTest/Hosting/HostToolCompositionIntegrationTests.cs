using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class HostToolCompositionIntegrationTests
{
    [Fact]
    public async Task GIVEN_CompleteHostComposition_WHEN_ValidatingContainer_THEN_ShouldResolveEveryRegisteredMcpTool()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);

        await using var serviceProvider = builder.Services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true,
        });

        var pluginCatalogState = serviceProvider.GetRequiredService<IPluginCatalogState>();
        var pluginStartup = serviceProvider.GetServices<IHostedService>()
            .OfType<PluginCatalogStartupLifecycleService>()
            .Single();
        await pluginStartup.StartingAsync(TestContext.Current.CancellationToken);

        var pluginCatalog = pluginCatalogState.Current.Catalog;
        var codeActionCatalog = serviceProvider.GetRequiredService<CodeActionCatalogSnapshot>();
        var startupConfiguration = serviceProvider.GetRequiredService<StartupConfigurationSnapshot>();
        var mcpServerOptions = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;

        var tools = serviceProvider.GetServices<McpServerTool>().ToArray();

        tools.Should().HaveCount(
            codeActionCatalog.Tools.Count
            + ServerOwnedToolRegistration.GetPublishedToolCount(
                startupConfiguration.Options.ErrorReporting));

        pluginCatalogState.Current.Tools.Should().HaveCount(pluginCatalog.Tools.Count);
        tools.Select(static tool => tool.ProtocolTool.Name)
            .Concat(pluginCatalogState.Current.Tools.Keys)
            .Should()
            .OnlyHaveUniqueItems();
        var dispatcher = serviceProvider.GetRequiredService<IErrorReportDispatcher>();
        if (SentrySdkPolicy.EmbeddedConfiguration is null)
        {
            dispatcher.Should().BeOfType<LoggingErrorReportDispatcher>();
            serviceProvider.GetService<ISentryClient>().Should().BeNull();
        }
        else
        {
            dispatcher.Should().BeOfType<SentryErrorReportDispatcher>();
            serviceProvider.GetRequiredService<ISentryClient>().Should().BeOfType<SentryClient>();
            serviceProvider.GetRequiredService<ISentryClient>().IsEnabled.Should().BeTrue();
        }

        mcpServerOptions.Filters.Request.CallToolFilters.Should().ContainSingle();
        mcpServerOptions.Handlers.ListToolsHandler.Should().NotBeNull();
        mcpServerOptions.Handlers.CallToolHandler.Should().NotBeNull();
        mcpServerOptions.ServerInstructions.Should().Contain(
            "keep it to one coherent change or tightly related set");

        mcpServerOptions.ServerInstructions.Should().Contain(
            "transaction-commit or transaction-rollback promptly");

        mcpServerOptions.ServerInstructions.Should().Contain(
            "does not create a Git commit");

        mcpServerOptions.ServerInstructions.Should().Contain(
            "https://raw.githubusercontent.com/lantean-code/roslyn-workbench-mcp/v1.0.0/docs/AgentGuide.md");

        startupConfiguration.Options.StateDirectory.Should().Be(Path.GetTempPath());
        serviceProvider.GetRequiredService<IWorkspaceLifecycleService>()
            .Should()
            .BeSameAs(serviceProvider.GetRequiredService<IWorkspaceLifecycleService>());

        serviceProvider.GetRequiredService<IToolExecutionContextFactory>()
            .Should()
            .BeSameAs(serviceProvider.GetRequiredService<IToolExecutionContextFactory>());

        serviceProvider.GetRequiredService<ICodeActionExecutionContextFactory>()
            .Should()
            .BeSameAs(serviceProvider.GetRequiredService<ICodeActionExecutionContextFactory>());

        serviceProvider.GetRequiredService<IServerStatusService>()
            .Should()
            .BeSameAs(serviceProvider.GetRequiredService<IServerStatusService>());

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
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);
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
