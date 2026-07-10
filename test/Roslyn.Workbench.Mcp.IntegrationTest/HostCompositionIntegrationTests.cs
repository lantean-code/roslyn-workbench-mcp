using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class HostCompositionIntegrationTests
{
    [Fact]
    public void GIVEN_CommandLineOptions_WHEN_ComposingHost_THEN_ShouldProjectDerivedOptionsIntoTheContainer()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-tests", Guid.NewGuid().ToString("n"));
        var builder = Host.CreateApplicationBuilder([]);

        builder.AddRoslynWorkbench(
        [
            "--default-max-results", "123",
            "--max-concurrent-queries", "7",
            "--max-transaction-revisions", "8",
            "--code-action-token-lifetime", "00:00:09",
            "--state-directory", stateDirectory,
            "--tool-output-schema-mode", "Full",
        ]);

        using var host = builder.Build();
        var startupOptions = host.Services.GetRequiredService<IOptions<StartupOptions>>().Value;
        var workspaceOptions = host.Services.GetRequiredService<IOptions<WorkspaceCoordinatorOptions>>().Value;
        var codeActionOptions = host.Services.GetRequiredService<IOptions<CodeActionRuntimeOptions>>().Value;

        startupOptions.DefaultMaxResults.Should().Be(123);
        startupOptions.MaxConcurrentQueries.Should().Be(7);
        startupOptions.MaxTransactionRevisions.Should().Be(8);
        startupOptions.CodeActionTokenLifetime.Should().Be(TimeSpan.FromSeconds(9));
        startupOptions.StateDirectory.Should().Be(stateDirectory);
        startupOptions.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Full);
        workspaceOptions.DefaultMaxResults.Should().Be(123);
        workspaceOptions.MaxConcurrentQueries.Should().Be(7);
        workspaceOptions.MaxTransactionRevisions.Should().Be(8);
        workspaceOptions.StateDirectory.Should().Be(stateDirectory);
        codeActionOptions.TokenLifetime.Should().Be(TimeSpan.FromSeconds(9));
    }

    [Fact]
    public void GIVEN_ConfiguredBuilder_WHEN_ComposingHost_THEN_ShouldRegisterHostServicesAndAllMcpTools()
    {
        var builder = Host.CreateApplicationBuilder([]);

        builder.AddRoslynWorkbench([]);

        using var host = builder.Build();
        var pluginCatalogSnapshot = host.Services.GetRequiredService<PluginCatalogSnapshot>();
        var mcpTools = host.Services.GetServices<McpServerTool>().ToArray();

        host.Services.GetRequiredService<IMsBuildRegistrationService>().Should().NotBeNull();
        host.Services.GetRequiredService<IToolExecutionServices>().Should().NotBeNull();
        host.Services.GetRequiredService<CodeActionRuntime>().Should().NotBeNull();
        host.Services.GetRequiredService<IToolExecutionContextFactory>().Should().NotBeNull();
        host.Services.GetRequiredService<McpServer>().Should().NotBeNull();
        host.Services.GetRequiredService<IRecoveryStatusReader>().Should().NotBeNull();

        mcpTools.Should().HaveCount(pluginCatalogSnapshot.Tools.Count + 11);
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
}
