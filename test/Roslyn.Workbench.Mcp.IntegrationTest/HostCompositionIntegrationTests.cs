using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class HostCompositionIntegrationTests
{
    [Fact]
    public void GIVEN_InvalidCommandLineOption_WHEN_ComposingHost_THEN_ShouldRegisterFallbackOptionsAndWarning()
    {
        var builder = Host.CreateApplicationBuilder([]);

        builder.AddRoslynWorkbench(["--default-max-results", "invalid"]);

        using var host = builder.Build();
        var startupOptions = host.Services.GetRequiredService<IOptions<StartupOptions>>().Value;
        var startupConfiguration = host.Services.GetRequiredService<StartupConfigurationSnapshot>();
        var workspaceOptions = host.Services.GetRequiredService<IOptions<WorkspaceCoordinatorOptions>>().Value;

        startupOptions.DefaultMaxResults.Should().Be(100);
        workspaceOptions.DefaultMaxResults.Should().Be(100);
        startupConfiguration.Warnings.Should().ContainSingle().Which.Should().BeEquivalentTo(new WarningInfo
        {
            Code = "StartupConfigurationFallback",
            Message = "Configuration '--default-max-results' is invalid; using default '100'.",
        });
    }

    [Fact]
    public void GIVEN_CommandLineOptions_WHEN_ComposingHost_THEN_ShouldProjectDerivedOptionsIntoTheContainer()
    {
        using var stateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-tests");
        var builder = Host.CreateApplicationBuilder([]);

        builder.AddRoslynWorkbench(
        [
            "--default-max-results", "123",
            "--max-concurrent-queries", "7",
            "--max-transaction-revisions", "8",
            "--code-action-token-lifetime", "00:00:09",
            "--state-directory", stateDirectory.DirectoryPath,
            "--tool-output-schema-mode", "Full",
        ]);

        using var host = builder.Build();
        var startupOptions = host.Services.GetRequiredService<IOptions<StartupOptions>>().Value;
        var workspaceOptions = host.Services.GetRequiredService<IOptions<WorkspaceCoordinatorOptions>>().Value;
        var codeActionOptions = host.Services.GetRequiredService<IOptions<CodeActionExecutionOptions>>().Value;

        startupOptions.DefaultMaxResults.Should().Be(123);
        startupOptions.MaxConcurrentQueries.Should().Be(7);
        startupOptions.MaxTransactionRevisions.Should().Be(8);
        startupOptions.CodeActionTokenLifetime.Should().Be(TimeSpan.FromSeconds(9));
        startupOptions.StateDirectory.Should().Be(stateDirectory.DirectoryPath);
        startupOptions.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Full);
        workspaceOptions.DefaultMaxResults.Should().Be(123);
        workspaceOptions.MaxConcurrentQueries.Should().Be(7);
        workspaceOptions.MaxTransactionRevisions.Should().Be(8);
        workspaceOptions.StateDirectory.Should().Be(stateDirectory.DirectoryPath);
        codeActionOptions.TokenLifetime.Should().Be(TimeSpan.FromSeconds(9));
    }

    [Fact]
    public void GIVEN_ConfiguredBuilder_WHEN_ComposingHost_THEN_ShouldRegisterHostServicesAndAllMcpTools()
    {
        var builder = Host.CreateApplicationBuilder([]);

        builder.AddRoslynWorkbench([]);

        var codeActionProviderCatalogRegistration = builder.Services.Single(
            static descriptor => descriptor.ServiceType == typeof(ICodeActionProviderCatalog));
        var workspaceFactoryRegistration = builder.Services.Single(
            static descriptor => descriptor.ServiceType == typeof(IMsBuildWorkspaceFactory));

        using var host = builder.Build();
        var pluginCatalogSnapshot = host.Services.GetRequiredService<PluginCatalogSnapshot>();
        var codeActionCatalogSnapshot = host.Services.GetRequiredService<CodeActionCatalogSnapshot>();
        var mcpTools = host.Services.GetServices<McpServerTool>().ToArray();

        host.Services.GetRequiredService<IMsBuildRegistrationService>().Should().NotBeNull();
        host.Services.GetRequiredService<IToolExecutionServices>().Should().NotBeNull();
        host.Services.GetRequiredService<ICodeActionProviderCatalog>().Should().BeOfType<MefCodeActionProviderCatalog>();
        host.Services.GetRequiredService<IMsBuildWorkspaceFactory>().Should().BeOfType<HostConfiguredMsBuildWorkspaceFactory>();
        host.Services.GetRequiredService<IToolExecutionContextFactory>().Should().BeOfType<PluginExecutionContextFactory>();
        host.Services.GetRequiredService<ICodeActionExecutionContextFactory>().Should().BeOfType<CodeActionExecutionContextFactory>();
        host.Services.GetRequiredService<McpServer>().Should().NotBeNull();
        host.Services.GetRequiredService<ICommitRecoveryStore>().Should().NotBeNull();
        codeActionProviderCatalogRegistration.ImplementationType.Should().Be(typeof(MefCodeActionProviderCatalog));
        workspaceFactoryRegistration.ImplementationType.Should().Be(typeof(HostConfiguredMsBuildWorkspaceFactory));

        mcpTools.Should().HaveCount(pluginCatalogSnapshot.Tools.Count + codeActionCatalogSnapshot.Tools.Count + 11);
        mcpTools.Select(static tool => tool.ProtocolTool.Name).Should().Contain(
        [
            "server-status",
            "workspace-open",
            "workspace-list",
            "workspace-close",
            "workspace-status",
            "workspace-reload",
            "transaction-start",
            "transaction-preview",
            "transaction-history",
            "transaction-commit",
            "transaction-rollback",
        ]);
    }

    [Fact]
    public async Task GIVEN_ComposedCodeActions_WHEN_RequestingFullServerStatus_THEN_ShouldNotPublishCodeActionsAsPlugin()
    {
        var builder = Host.CreateApplicationBuilder([]);
        builder.AddRoslynWorkbench([]);

        using var host = builder.Build();
        var pluginCatalogSnapshot = host.Services.GetRequiredService<PluginCatalogSnapshot>();
        var codeActionCatalogSnapshot = host.Services.GetRequiredService<CodeActionCatalogSnapshot>();
        var target = host.Services.GetRequiredService<IServerStatusService>();

        var result = await target.GetStatusAsync(StatusDetailLevel.Full, TestContext.Current.CancellationToken);

        codeActionCatalogSnapshot.Tools.Should().NotBeEmpty();
        result.Data!.CodeActions.Should().NotBeNull();
        result.Data.Plugins.Should().BeEquivalentTo(pluginCatalogSnapshot.Plugins);
        result.Data.Plugins.Should().NotContain(static plugin =>
            string.Equals(plugin.PluginId, "roslyn.workbench.codeactions", StringComparison.Ordinal));
    }
}
