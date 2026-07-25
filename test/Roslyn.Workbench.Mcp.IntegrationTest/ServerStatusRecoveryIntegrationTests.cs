using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class ServerStatusRecoveryIntegrationTests
{
    [Fact]
    public async Task GIVEN_UnfinishedRecoveryRecord_WHEN_RequestingFullServerStatus_THEN_ShouldMapPersistedRecoveryDiagnostics()
    {
        using var stateDirectory = TemporaryDirectory.Create("roslyn-workbench-mcp-status-tests");
        var fileSystem = new FileSystem();
        var pathComparison = new WorkspacePathComparison();
        var recoveryStore = new CommitRecoveryStore(
            Options.Create(new WorkspaceOptions { StateDirectory = stateDirectory.DirectoryPath }),
            fileSystem,
            new AtomicFileWriter(fileSystem, new NativeAtomicFileCommitter()),
            pathComparison,
            new PhysicalPathContainment(fileSystem, pathComparison));

        await recoveryStore.WriteStatusAsync(new RecoveryStatus
        {
            CommitId = "commit-id",
            SolutionPath = "/workspace/Sample.csproj",
            State = RecoveryState.RecoveryIncomplete,
            Message = "Message",
        }, TestContext.Current.CancellationToken);

        var options = new StartupOptions
        {
            StateDirectory = stateDirectory.DirectoryPath,
        };

        var msBuildRegistrationService = new Mock<IMsBuildRegistrationService>();
        msBuildRegistrationService
            .SetupGet(item => item.CurrentStatus)
            .Returns(new ComponentStatus
            {
                IsAvailable = true,
            });

        var codeActionProviderCatalog = new Mock<ICodeActionProviderCatalog>();
        codeActionProviderCatalog
            .SetupGet(item => item.Status)
            .Returns(new CodeActionProviderCatalogStatus
            {
                IsAvailable = true,
            });

        var service = new ServerStatusService(
            Options.Create(options),
            new StartupConfigurationSnapshot
            {
                Options = options,
            },
            new PluginCatalogSnapshot(),
            new CodeActionCatalogSnapshot(),
            msBuildRegistrationService.Object,
            codeActionProviderCatalog.Object,
            recoveryStore);

        var result = await service.GetStatusAsync(StatusDetailLevel.Full, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Recovery.Should().ContainSingle(static status => status.CommitId == "commit-id");
    }
}
