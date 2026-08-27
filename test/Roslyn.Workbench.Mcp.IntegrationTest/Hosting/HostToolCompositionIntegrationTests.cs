using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.ErrorReporting.Capture;

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
        var serverInstructions = mcpServerOptions.ServerInstructions;
        serverInstructions.Should().NotBeNull();
        serverInstructions.Should().Contain(
            "fully trusted C# workspaces");

        serverInstructions.Should().Contain(
            "analysers run unsandboxed with Host permissions");

        serverInstructions.Should().Contain(
            "Prefer queries before mutations");

        serverInstructions.Should().Contain(
            "Start transactions only when ready");

        serverInstructions.Should().Contain(
            "keep each to one coherent change or tightly related set");

        serverInstructions.Should().Contain(
            "inspect transaction-preview");

        serverInstructions.Should().Contain(
            "transaction-commit or transaction-rollback promptly");

        serverInstructions.Should().Contain(
            "does not create a Git commit");

        serverInstructions.Should().Contain(
            "https://raw.githubusercontent.com/lantean-code/roslyn-workbench-mcp/v1.0.0/docs/AgentGuide.md");

        serverInstructions.Length.Should().BeLessThanOrEqualTo(512);

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

    [Fact]
    public async Task GIVEN_InstalledCallToolFilter_WHEN_HandlerThrowsUnrelatedCancellation_THEN_ShouldCaptureCorrelatedFailure()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddRoslynWorkbench(["--state-directory", Path.GetTempPath()]);
        await using var serviceProvider = builder.Services.BuildServiceProvider();
        var mcpServerOptions = serviceProvider.GetRequiredService<IOptions<McpServerOptions>>().Value;
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

        using var unrelatedCancellation = new CancellationTokenSource();
        await unrelatedCancellation.CancelAsync();
        var exception = new OperationCanceledException(
            "Sensitive unrelated cancellation",
            innerException: null,
            unrelatedCancellation.Token);
        McpRequestHandler<CallToolRequestParams, CallToolResult> next = (_, _) =>
            ValueTask.FromException<CallToolResult>(exception);

        var result = await mcpServerOptions.Filters.Request.CallToolFilters.Single()(next)(
            context,
            CancellationToken.None);

        result.IsError.Should().BeTrue();
        result.StructuredContent.Should().NotBeNull();
        var structuredContent = result.StructuredContent.GetValueOrDefault();
        var error = structuredContent.GetProperty("error");
        error.GetProperty("code").GetString().Should().Be("UnhandledException");
        structuredContent.GetRawText().Should().NotContain("Sensitive unrelated cancellation");
        var correlationId = error.GetProperty("correlationId").GetGuid();
        var capturedErrorStore = serviceProvider.GetRequiredService<ICapturedErrorStore>();
        if (!capturedErrorStore.TryGet(correlationId, out var record))
        {
            throw new InvalidOperationException("The installed call-tool filter did not retain its correlated failure.");
        }

        record.CancellationRequested.Should().BeFalse();
        record.Exceptions.Should().ContainSingle().Which.Type.Should().Be(typeof(OperationCanceledException).FullName);
    }
}
