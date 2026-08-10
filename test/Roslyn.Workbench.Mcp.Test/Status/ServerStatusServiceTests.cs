using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test.Status;

public sealed class ServerStatusServiceTests
{
    private readonly Mock<ICodeActionComposition> _codeActionComposition = new Mock<ICodeActionComposition>();
    private readonly Mock<IMsBuildRegistrationService> _msBuildRegistrationService = new Mock<IMsBuildRegistrationService>();
    private readonly Mock<ICommitRecoveryStore> _recoveryStore = new Mock<ICommitRecoveryStore>();
    private readonly Mock<IErrorReportingConsentService> _errorReportingConsentService = new Mock<IErrorReportingConsentService>();
    private readonly Mock<IErrorReportDispatcher> _errorReportDispatcher = new Mock<IErrorReportDispatcher>();

    public ServerStatusServiceTests()
    {
        _msBuildRegistrationService
            .SetupGet(item => item.CurrentStatus)
            .Returns(new ComponentStatus
            {
                IsAvailable = true,
                Version = "1.0.0",
                Message = "MSBuildPath",
            });

        _codeActionComposition
            .SetupGet(item => item.Status)
            .Returns(CodeActionCompositionStatus.Available());
        _errorReportDispatcher
            .SetupGet(item => item.Name)
            .Returns("Dispatcher");
    }

    [Fact]
    public async Task GIVEN_StandardDetail_WHEN_GettingStatus_THEN_ShouldReturnSummaryWithoutExpandedBranches()
    {
        var pluginSnapshot = CreatePluginSnapshot();
        var target = CreateTarget(new StartupOptions(), pluginSnapshot);

        var result = await target.GetStatusAsync(StatusDetailLevel.Standard, CancellationToken.None);

        var data = result.Data ?? throw new InvalidOperationException("The status response did not contain data.");
        data.ToolCount.Should().Be(
            pluginSnapshot.Tools.Count
            + ServerOwnedToolRegistration.GetPublishedToolCount(new ErrorReportingOptions()));
        var msBuild = data.MsBuild ?? throw new InvalidOperationException("The status response did not contain MSBuild status.");
        var codeActions = data.CodeActions ?? throw new InvalidOperationException("The status response did not contain code-action status.");
        msBuild.IsAvailable.Should().BeTrue();
        codeActions.IsAvailable.Should().BeTrue();
        data.Plugins.Should().BeNull();
        data.Configuration.Should().BeNull();
        data.StartupWarnings.Should().BeNull();
        data.Recovery.Should().BeNull();
        _recoveryStore.Verify(item => item.GetStatusesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_FullDetail_WHEN_GettingStatus_THEN_ShouldReturnConfigurationPluginsAndRecovery()
    {
        var options = new StartupOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxTransactionRevisions = 20,
            CodeActionReferenceLifetime = TimeSpan.FromMinutes(5),
            StateDirectory = "/state",
        };

        var recovery = new RecoveryStatus
        {
            CommitId = "CommitId",
        };

        _recoveryStore.Setup(item => item.GetStatusesAsync(TestContext.Current.CancellationToken)).ReturnsAsync([recovery]);
        var pluginSnapshot = CreatePluginSnapshot();
        var startupWarning = new WarningInfo
        {
            Code = "Code",
            Message = "Message",
        };

        var target = CreateTarget(options, pluginSnapshot, startupWarnings: [startupWarning]);

        var result = await target.GetStatusAsync(StatusDetailLevel.Full, TestContext.Current.CancellationToken);

        var data = result.Data ?? throw new InvalidOperationException("The status response did not contain data.");
        data.Configuration.Should().NotBeNull();
        data.Configuration!.DefaultMaxResults.Should().Be(100);
        data.Configuration.ErrorReporting!.Provider.Should().Be("Dispatcher");
        data.StartupWarnings.Should().ContainSingle().Which.Should().Be(startupWarning);
        data.Plugins.Should().BeEquivalentTo(pluginSnapshot.Plugins);
        data.Recovery.Should().ContainSingle().Which.Should().Be(recovery);
    }

    [Fact]
    public async Task GIVEN_UnavailableCodeActions_WHEN_GettingStatus_THEN_ShouldReturnDisablementDiagnostics()
    {
        _codeActionComposition
            .SetupGet(item => item.Status)
            .Returns(CodeActionCompositionStatus.Unavailable("Code-action composition is unavailable."));

        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());

        var result = await target.GetStatusAsync(StatusDetailLevel.Standard, CancellationToken.None);

        var data = result.Data ?? throw new InvalidOperationException("The status response did not contain data.");
        var codeActions = data.CodeActions ?? throw new InvalidOperationException("The status response did not contain code-action status.");
        codeActions.IsAvailable.Should().BeFalse();
        codeActions.Message.Should().Be("Code-action composition is unavailable.");
    }

    [Fact]
    public async Task GIVEN_PluginAndCodeActionTools_WHEN_GettingStatus_THEN_ShouldCountEveryToolFamily()
    {
        var pluginTool = new Mock<IRegisteredPluginTool>();
        var firstCodeActionTool = new Mock<IRegisteredCodeActionTool>();
        var secondCodeActionTool = new Mock<IRegisteredCodeActionTool>();
        var pluginSnapshot = new PluginCatalogSnapshot
        {
            Tools = [pluginTool.Object],
        };

        var codeActionSnapshot = new CodeActionCatalogSnapshot
        {
            Tools =
            [
                firstCodeActionTool.Object,
                secondCodeActionTool.Object,
            ],
        };

        var target = CreateTarget(new StartupOptions(), pluginSnapshot, codeActionSnapshot);

        var result = await target.GetStatusAsync(StatusDetailLevel.Standard, CancellationToken.None);

        result.Data!.ToolCount.Should().Be(
            3 + ServerOwnedToolRegistration.GetPublishedToolCount(new ErrorReportingOptions()));
    }

    [Fact]
    public async Task GIVEN_RepeatedFullDetail_WHEN_GettingStatus_THEN_ShouldRefreshRuntimeConfigurationProjection()
    {
        _recoveryStore.Setup(item => item.GetStatusesAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());

        var first = await target.GetStatusAsync(StatusDetailLevel.Full, CancellationToken.None);
        var second = await target.GetStatusAsync(StatusDetailLevel.Full, CancellationToken.None);

        first.Data!.Configuration.Should().NotBeSameAs(second.Data!.Configuration);
        first.Data.Configuration.Should().BeEquivalentTo(second.Data.Configuration);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_GettingStatus_THEN_ShouldThrowOperationCanceledException()
    {
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();

        var action = async () => await target.GetStatusAsync(StatusDetailLevel.Standard, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private ServerStatusService CreateTarget(
        StartupOptions options,
        PluginCatalogSnapshot pluginSnapshot,
        CodeActionCatalogSnapshot? codeActionSnapshot = null,
        IReadOnlyList<WarningInfo>? startupWarnings = null)
    {
        var configuration = new StartupConfigurationSnapshot
        {
            Options = options,
            Warnings = startupWarnings ?? [],
        };

        codeActionSnapshot ??= new CodeActionCatalogSnapshot();
        var pluginRuntimeCatalog = new PluginRuntimeCatalogSnapshot
        {
            Catalog = pluginSnapshot,
        };
        var pluginCatalogState = new Mock<IPluginCatalogState>();
        pluginCatalogState.SetupGet(static state => state.Current).Returns(pluginRuntimeCatalog);

        return new ServerStatusService(
            Options.Create(options),
            configuration,
            pluginCatalogState.Object,
            codeActionSnapshot,
            _msBuildRegistrationService.Object,
            _codeActionComposition.Object,
            _recoveryStore.Object,
            _errorReportingConsentService.Object,
            _errorReportDispatcher.Object);
    }

    private static PluginCatalogSnapshot CreatePluginSnapshot()
    {
        return new PluginCatalogSnapshot
        {
            Plugins =
            [
                new PluginStatus
                {
                    PluginId = "PluginId",
                    DisplayName = "DisplayName",
                    Version = "1.0.0",
                    SupportedApiVersion = "1.0",
                    Enabled = true,
                },
            ],
        };
    }
}
