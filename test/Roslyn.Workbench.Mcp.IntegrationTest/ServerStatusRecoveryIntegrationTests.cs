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
        var stateDirectorySecurity = new WorkspaceStateDirectorySecurity(fileSystem);
        var workspaceStateDirectory = new WorkspaceStateDirectory(
            Options.Create(new WorkspaceOptions { StateDirectory = stateDirectory.DirectoryPath }),
            fileSystem,
            stateDirectorySecurity);

        workspaceStateDirectory.Initialize();
        var recoveryStore = new CommitRecoveryStore(
            fileSystem,
            new AtomicFileWriter(fileSystem, new NativeAtomicFileCommitter()),
            pathComparison,
            new PhysicalPathContainment(fileSystem, pathComparison),
            workspaceStateDirectory,
            stateDirectorySecurity,
            CommitRecoveryLimits.Default);

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

        var codeActionComposition = new Mock<ICodeActionComposition>();
        var errorReportingConsentService = new Mock<IErrorReportingConsentService>();
        var errorReportDispatcher = new Mock<IErrorReportDispatcher>();
        codeActionComposition
            .SetupGet(item => item.Status)
            .Returns(CodeActionCompositionStatus.Available());

        var service = new ServerStatusService(
            Options.Create(options),
            new StartupConfigurationSnapshot
            {
                Options = options,
            },
            new PluginCatalogSnapshot(),
            new CodeActionCatalogSnapshot(),
            msBuildRegistrationService.Object,
            codeActionComposition.Object,
            recoveryStore,
            errorReportingConsentService.Object,
            errorReportDispatcher.Object);

        var result = await service.GetStatusAsync(StatusDetailLevel.Full, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Recovery.Should().ContainSingle(static status => status.CommitId == "commit-id");
    }
}
