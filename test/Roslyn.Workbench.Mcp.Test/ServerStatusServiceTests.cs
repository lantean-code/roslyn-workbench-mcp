using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.CodeActions.Contracts;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class ServerStatusServiceTests
{
    private readonly Mock<ICodeActionRuntime> _codeActionRuntime = new();
    private readonly Mock<IMsBuildRegistrationService> _msBuildRegistrationService = new();
    private readonly Mock<IRecoveryStatusReader> _recoveryStatusReader = new();

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
        _codeActionRuntime
            .SetupGet(item => item.Status)
            .Returns(new CodeActionRuntimeStatus
            {
                IsAvailable = true,
            });
    }

    [Fact]
    public async Task GIVEN_StandardDetail_WHEN_GettingStatus_THEN_ShouldReturnSummaryWithoutExpandedBranches()
    {
        var pluginSnapshot = CreatePluginSnapshot();
        var target = CreateTarget(new StartupOptions(), pluginSnapshot);

        var result = await target.GetStatusAsync(StatusDetailLevel.Standard, CancellationToken.None);

        var data = result.Data ?? throw new InvalidOperationException("The status response did not contain data.");
        data.ToolCount.Should().Be(pluginSnapshot.Tools.Count + ServerOwnedToolRegistration.ToolCount);
        var msBuild = data.MsBuild ?? throw new InvalidOperationException("The status response did not contain MSBuild status.");
        var codeActions = data.CodeActions ?? throw new InvalidOperationException("The status response did not contain code-action status.");
        msBuild.IsAvailable.Should().BeTrue();
        codeActions.IsAvailable.Should().BeTrue();
        data.Plugins.Should().BeNull();
        data.Configuration.Should().BeNull();
        data.Recovery.Should().BeNull();
        _recoveryStatusReader.Verify(item => item.GetStatuses(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_FullDetail_WHEN_GettingStatus_THEN_ShouldReturnConfigurationPluginsAndRecovery()
    {
        var options = new StartupOptions
        {
            DefaultMaxResults = 100,
            MaxConcurrentQueries = 2,
            MaxTransactionRevisions = 20,
            CodeActionTokenLifetime = TimeSpan.FromMinutes(5),
            StateDirectory = "/state",
        };
        var recovery = new RecoveryStatus
        {
            CommitId = "CommitId",
        };
        _recoveryStatusReader.Setup(item => item.GetStatuses("/state")).Returns([recovery]);
        var pluginSnapshot = CreatePluginSnapshot();
        var target = CreateTarget(options, pluginSnapshot);

        var result = await target.GetStatusAsync(StatusDetailLevel.Full, CancellationToken.None);

        var data = result.Data ?? throw new InvalidOperationException("The status response did not contain data.");
        data.Configuration.Should().NotBeNull();
        data.Configuration!.DefaultMaxResults.Should().Be(100);
        data.Plugins.Should().BeEquivalentTo(pluginSnapshot.Plugins);
        data.Recovery.Should().ContainSingle().Which.Should().Be(recovery);
    }

    [Fact]
    public async Task GIVEN_UnavailableCodeActions_WHEN_GettingStatus_THEN_ShouldReturnDisablementDiagnostics()
    {
        _codeActionRuntime
            .SetupGet(item => item.Status)
            .Returns(new CodeActionRuntimeStatus
            {
                IsAvailable = false,
                Message = "Code-action composition is unavailable.",
            });
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

        result.Data!.ToolCount.Should().Be(3 + ServerOwnedToolRegistration.ToolCount);
    }

    [Fact]
    public async Task GIVEN_RepeatedFullDetail_WHEN_GettingStatus_THEN_ShouldReuseConfigurationProjection()
    {
        _recoveryStatusReader.Setup(item => item.GetStatuses(It.IsAny<string>())).Returns([]);
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());

        var first = await target.GetStatusAsync(StatusDetailLevel.Full, CancellationToken.None);
        var second = await target.GetStatusAsync(StatusDetailLevel.Full, CancellationToken.None);

        first.Data!.Configuration.Should().BeSameAs(second.Data!.Configuration);
    }

    [Fact]
    public async Task GIVEN_CancelledToken_WHEN_GettingStatus_THEN_ShouldThrowOperationCanceledException()
    {
        var target = CreateTarget(new StartupOptions(), new PluginCatalogSnapshot());
        using var cancellationSource = new CancellationTokenSource();
        cancellationSource.Cancel();

        var action = async () => await target.GetStatusAsync(StatusDetailLevel.Standard, cancellationSource.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private ServerStatusService CreateTarget(
        StartupOptions options,
        PluginCatalogSnapshot pluginSnapshot,
        CodeActionCatalogSnapshot? codeActionSnapshot = null)
    {
        return new ServerStatusService(
            Options.Create(options),
            pluginSnapshot,
            codeActionSnapshot ?? new CodeActionCatalogSnapshot(),
            _msBuildRegistrationService.Object,
            _codeActionRuntime.Object,
            _recoveryStatusReader.Object);
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
