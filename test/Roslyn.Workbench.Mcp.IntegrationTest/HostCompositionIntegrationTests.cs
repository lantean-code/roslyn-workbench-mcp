using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using Roslyn.Workbench.Mcp.CodeActions.Discovery;
using Roslyn.Workbench.Mcp.CodeActions.References;
using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.State;

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
        var workspaceOptions = host.Services.GetRequiredService<IOptions<WorkspaceOptions>>().Value;

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
            "--code-action-reference-lifetime", "00:00:09",
            "--state-directory", stateDirectory.DirectoryPath,
            "--tool-output-schema-mode", "Full",
        ]);

        using var host = builder.Build();
        var startupOptions = host.Services.GetRequiredService<IOptions<StartupOptions>>().Value;
        var workspaceOptions = host.Services.GetRequiredService<IOptions<WorkspaceOptions>>().Value;
        var codeActionOptions = host.Services.GetRequiredService<IOptions<CodeActionExecutionOptions>>().Value;

        startupOptions.DefaultMaxResults.Should().Be(123);
        startupOptions.MaxConcurrentQueries.Should().Be(7);
        startupOptions.MaxTransactionRevisions.Should().Be(8);
        startupOptions.CodeActionReferenceLifetime.Should().Be(TimeSpan.FromSeconds(9));
        startupOptions.StateDirectory.Should().Be(stateDirectory.DirectoryPath);
        startupOptions.ToolOutputSchemaMode.Should().Be(ToolOutputSchemaMode.Full);
        workspaceOptions.DefaultMaxResults.Should().Be(123);
        workspaceOptions.MaxConcurrentQueries.Should().Be(7);
        workspaceOptions.MaxTransactionRevisions.Should().Be(8);
        workspaceOptions.StateDirectory.Should().Be(stateDirectory.DirectoryPath);
        codeActionOptions.ReferenceLifetime.Should().Be(TimeSpan.FromSeconds(9));
    }

    [Fact]
    public void GIVEN_ConfiguredBuilder_WHEN_ComposingHost_THEN_ShouldRegisterHostServicesAndAllMcpTools()
    {
        var builder = Host.CreateApplicationBuilder([]);

        builder.AddRoslynWorkbench([]);

        var codeActionCompositionRegistration = builder.Services.Single(
            static descriptor => descriptor.ServiceType == typeof(ICodeActionComposition));

        var codeActionPolicyRegistration = builder.Services.Single(
            static descriptor => descriptor.ServiceType == typeof(ICodeActionPolicy));

        var codeActionProviderSelectionRegistration = builder.Services.Single(
            static descriptor => descriptor.ServiceType == typeof(ICodeActionProviderSelection));

        var builtInAnalyzerIndexRegistration = builder.Services.Single(
            static descriptor => descriptor.ServiceType == typeof(ICodeActionBuiltInAnalyzerIndex));

        var workspaceFactoryRegistration = builder.Services.Single(
            static descriptor => descriptor.ServiceType == typeof(IMsBuildWorkspaceFactory));

        using var host = builder.Build();
        var pluginCatalogSnapshot = host.Services.GetRequiredService<PluginCatalogSnapshot>();
        var codeActionCatalogSnapshot = host.Services.GetRequiredService<CodeActionCatalogSnapshot>();
        var toolExecutionServices = host.Services.GetRequiredService<IToolExecutionServices>();
        var referenceDiscoveryService = host.Services.GetRequiredService<IReferenceDiscoveryService>();
        var workspaceSelectorFactory = host.Services.GetRequiredService<IWorkspaceSelectorFactory>();
        var workspaceQueryCache = host.Services.GetRequiredService<IWorkspaceQueryCache>();
        var referenceStore = host.Services.GetRequiredService<ICodeActionReferenceStore>();
        var lifecycleObservers = host.Services.GetServices<IWorkspaceSnapshotLifecycleObserver>().ToArray();
        var mcpTools = host.Services.GetServices<McpServerTool>().ToArray();
        var cachedValue = new object();

        toolExecutionServices.QueryCache.Store("WorkspaceId", "Key", cachedValue, 1);
        var foundBeforeInvalidation = toolExecutionServices.QueryCache.TryGet<object>("WorkspaceId", "Key", out var valueBeforeInvalidation);
        workspaceQueryCache.InvalidateWorkspace("WorkspaceId");
        var foundAfterInvalidation = toolExecutionServices.QueryCache.TryGet<object>("WorkspaceId", "Key", out _);

        host.Services.GetRequiredService<IMsBuildRegistrationService>().Should().NotBeNull();
        toolExecutionServices.QueryCache.Should().BeOfType<QueryCache>();
        referenceDiscoveryService.Should().BeOfType<ReferenceDiscoveryService>();
        toolExecutionServices.ReferenceDiscoveryService.Should().BeSameAs(referenceDiscoveryService);
        workspaceSelectorFactory.Should().BeOfType<WorkspaceSelectorFactory>();
        toolExecutionServices.WorkspaceSelectorFactory.Should().BeSameAs(workspaceSelectorFactory);
        workspaceQueryCache.Should().BeOfType<WorkspaceQueryCache>();
        toolExecutionServices.QueryCache.Should().NotBeSameAs(workspaceQueryCache);
        host.Services.GetRequiredService<IQueryCache>().Should().BeSameAs(toolExecutionServices.QueryCache);
        foundBeforeInvalidation.Should().BeTrue();
        valueBeforeInvalidation.Should().BeSameAs(cachedValue);
        foundAfterInvalidation.Should().BeFalse();
        host.Services.GetRequiredService<ICodeActionComposition>().Should().BeOfType<MefCodeActionComposition>();
        host.Services.GetRequiredService<ICodeActionPolicy>().Should().BeOfType<CodeActionPolicy>();
        host.Services.GetRequiredService<ICodeActionProviderSelection>().Should().BeOfType<CodeActionProviderSelection>();
        host.Services.GetRequiredService<ICodeActionBuiltInAnalyzerIndex>().Should().BeOfType<CodeActionBuiltInAnalyzerIndex>();
        lifecycleObservers.Should().ContainSingle().Which.Should().BeSameAs(referenceStore);
        host.Services.GetRequiredService<IMsBuildWorkspaceFactory>().Should().BeOfType<HostConfiguredMsBuildWorkspaceFactory>();
        host.Services.GetRequiredService<IToolExecutionContextFactory>().Should().BeOfType<PluginExecutionContextFactory>();
        host.Services.GetRequiredService<ICodeActionExecutionContextFactory>().Should().BeOfType<CodeActionExecutionContextFactory>();
        host.Services.GetRequiredService<McpServer>().Should().NotBeNull();
        host.Services.GetRequiredService<ICommitRecoveryStore>().Should().NotBeNull();
        codeActionCompositionRegistration.ImplementationType.Should().Be<MefCodeActionComposition>();
        codeActionPolicyRegistration.ImplementationType.Should().Be<CodeActionPolicy>();
        codeActionProviderSelectionRegistration.ImplementationType.Should().Be<CodeActionProviderSelection>();
        builtInAnalyzerIndexRegistration.ImplementationType.Should().Be<CodeActionBuiltInAnalyzerIndex>();
        workspaceFactoryRegistration.ImplementationType.Should().Be<HostConfiguredMsBuildWorkspaceFactory>();

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
