using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.CodeActions.Discovery;
using Roslyn.Workbench.Mcp.CodeActions.References;
using Roslyn.Workbench.Mcp.Workspace.Caching;
using Roslyn.Workbench.Mcp.Workspace.State;
using Sentry;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class HostCompositionIntegrationTests
{
    private const string _sentryDestination = "Sentry project 1000000000000000 at o100000.ingest.us.sentry.io";
    private const string _sentryDsn = "https://0123456789abcdef0123456789abcdef@o100000.ingest.us.sentry.io/1000000000000000";

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

        var errorReportingConsentRegistration = builder.Services.Single(
            static descriptor => descriptor.ServiceType == typeof(IErrorReportingConsentService));

        var errorReportingConsentObserverRegistration = builder.Services.Single(
            static descriptor => descriptor.ServiceType == typeof(IWorkspaceSnapshotLifecycleObserver)
                && descriptor.ImplementationType == typeof(ErrorReportingConsentLifecycleObserver));

        using var host = builder.Build();
        var startupOptions = host.Services.GetRequiredService<IOptions<StartupOptions>>().Value;
        var pluginCatalogSnapshot = host.Services.GetRequiredService<PluginCatalogSnapshot>();
        var codeActionCatalogSnapshot = host.Services.GetRequiredService<CodeActionCatalogSnapshot>();
        var toolExecutionServices = host.Services.GetRequiredService<IToolExecutionServices>();
        var referenceDiscoveryService = host.Services.GetRequiredService<IReferenceDiscoveryService>();
        var typeHierarchyService = host.Services.GetRequiredService<ITypeHierarchyService>();
        var workspaceSelectorFactory = host.Services.GetRequiredService<IWorkspaceSelectorFactory>();
        var workspaceQueryCache = host.Services.GetRequiredService<IWorkspaceQueryCache>();
        var referenceStore = host.Services.GetRequiredService<ICodeActionReferenceStore>();
        var lifecycleObservers = host.Services.GetServices<IWorkspaceSnapshotLifecycleObserver>().ToArray();
        var mcpTools = host.Services.GetServices<McpServerTool>().ToArray();
        host.Services.GetRequiredService<IMsBuildRegistrationService>().Should().NotBeNull();
        referenceDiscoveryService.Should().BeOfType<ReferenceDiscoveryService>();
        toolExecutionServices.ReferenceDiscoveryService.Should().BeSameAs(referenceDiscoveryService);
        typeHierarchyService.Should().BeOfType<TypeHierarchyService>();
        toolExecutionServices.TypeHierarchyService.Should().BeSameAs(typeHierarchyService);
        workspaceSelectorFactory.Should().BeOfType<WorkspaceSelectorFactory>();
        toolExecutionServices.WorkspaceSelectorFactory.Should().BeSameAs(workspaceSelectorFactory);
        workspaceQueryCache.Should().BeOfType<WorkspaceQueryCache>();
        host.Services.GetRequiredService<IWorkspaceQueryCacheState>().Should().BeOfType<WorkspaceQueryCacheState>();
        host.Services.GetRequiredService<IPluginQueryCacheState>().Should().BeOfType<PluginQueryCacheState>();
        host.Services.GetRequiredService<ICodeActionComposition>().Should().BeOfType<MefCodeActionComposition>();
        host.Services.GetRequiredService<ICodeActionPolicy>().Should().BeOfType<CodeActionPolicy>();
        host.Services.GetRequiredService<ICodeActionProviderSelection>().Should().BeOfType<CodeActionProviderSelection>();
        host.Services.GetRequiredService<ICodeActionBuiltInAnalyzerIndex>().Should().BeOfType<CodeActionBuiltInAnalyzerIndex>();
        lifecycleObservers.Should().Contain(item => item is PluginQueryCacheLifecycleObserver);
        lifecycleObservers.Should().Contain(item => item is CodeActionReferenceLifecycleObserver);
        lifecycleObservers.Should().Contain(item => item is ErrorReportingConsentLifecycleObserver);
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
        errorReportingConsentRegistration.ImplementationType.Should().Be<ErrorReportingConsentService>();
        errorReportingConsentObserverRegistration.ImplementationType.Should().Be<ErrorReportingConsentLifecycleObserver>();

        mcpTools.Should().HaveCount(
            pluginCatalogSnapshot.Tools.Count
            + codeActionCatalogSnapshot.Tools.Count
            + ServerOwnedToolRegistration.GetPublishedToolCount(
                startupOptions.ErrorReporting));
        mcpTools.Select(static tool => tool.ProtocolTool.Name).Should().Contain(
        [
            "server-status",
            "get-error-details",
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
    public async Task GIVEN_CustomErrorReportDispatcher_WHEN_ComposingReportingTools_THEN_ShouldUseSubstitutedDispatcher()
    {
        var builder = Host.CreateApplicationBuilder([]);
        var dispatcher = new Mock<IErrorReportDispatcher>();
        dispatcher.SetupGet(item => item.Name).Returns("CustomDispatcher");
        builder.AddRoslynWorkbench(
        [
            "--error-reporting-consent",
            "always",
        ]);
        builder.Services.AddSingleton(dispatcher.Object);

        using var host = builder.Build();
        var reportingTools = host.Services
            .GetServices<McpServerTool>()
            .Where(tool => tool.ProtocolTool.Name is "prepare-error-report" or "submit-error-report")
            .ToArray();

        host.Services.GetRequiredService<IErrorReportDispatcher>().Should().BeSameAs(dispatcher.Object);
        reportingTools.Should().HaveCount(2);
        var statusService = host.Services.GetRequiredService<IServerStatusService>();
        var status = await statusService.GetStatusAsync(StatusDetailLevel.Full, TestContext.Current.CancellationToken);
        status.Data!.Configuration!.ErrorReporting!.Provider.Should().Be("CustomDispatcher");
    }

    [Fact]
    public void GIVEN_NoEmbeddedSentryConfiguration_WHEN_RegisteringDispatcher_THEN_ShouldUseLoggingProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        RoslynWorkbenchServiceCollectionExtensions.AddErrorReportDispatcher(services, sentryConfiguration: null);

        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<IErrorReportDispatcher>().Should().BeOfType<LoggingErrorReportDispatcher>();
        serviceProvider.GetService<ISentryClient>().Should().BeNull();
    }

    [Fact]
    public void GIVEN_EmbeddedSentryConfiguration_WHEN_RegisteringDispatcher_THEN_ShouldUseIsolatedSentryProvider()
    {
        var services = new ServiceCollection();
        var configuration = new SentryProviderConfiguration(_sentryDsn, _sentryDestination);

        RoslynWorkbenchServiceCollectionExtensions.AddErrorReportDispatcher(services, configuration);

        var clientRegistration = services.Single(item => item.ServiceType == typeof(ISentryClient));
        clientRegistration.ImplementationType.Should().Be<SentryClient>();
        clientRegistration.ImplementationFactory.Should().BeNull();
        var optionsRegistration = services.Single(item => item.ServiceType == typeof(SentryOptions));
        optionsRegistration.ImplementationType.Should().Be<RoslynWorkbenchSentryOptions>();
        optionsRegistration.ImplementationFactory.Should().BeNull();
        using var serviceProvider = services.BuildServiceProvider();
        serviceProvider.GetRequiredService<IErrorReportDispatcher>().Should().BeOfType<SentryErrorReportDispatcher>();
        serviceProvider.GetRequiredService<ISentryClient>().Should().BeOfType<SentryClient>();
        serviceProvider.GetRequiredService<ISentryClient>().IsEnabled.Should().BeTrue();
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
