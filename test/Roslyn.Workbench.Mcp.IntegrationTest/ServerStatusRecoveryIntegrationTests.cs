using System.Text.Json;
using Microsoft.Extensions.Options;
using Roslyn.Workbench.Mcp.CodeActions.Contracts;
using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class ServerStatusRecoveryIntegrationTests
{
    [Fact]
    public async Task GIVEN_UnfinishedRecoveryRecord_WHEN_RequestingFullServerStatus_THEN_ShouldReturnPersistedRecoveryDiagnostics()
    {
        var stateDirectory = Path.Combine(Path.GetTempPath(), "roslyn-workbench-mcp-status-tests", Guid.NewGuid().ToString("n"));
        Directory.CreateDirectory(stateDirectory);

        try
        {
            var fileSystem = new FileSystem();
            var recoveryStore = new CommitRecoveryStore(
                Options.Create(new WorkspaceCoordinatorOptions { StateDirectory = stateDirectory }),
                fileSystem,
                new AtomicFileWriter(fileSystem, new NativeAtomicFileCommitter()),
                new WorkspacePathComparison());
            await recoveryStore.WriteStatusAsync(new RecoveryStatus
            {
                CommitId = "commit-id",
                SolutionPath = "/workspace/Sample.csproj",
                State = RecoveryState.RecoveryIncomplete,
                Message = "Message",
            }, TestContext.Current.CancellationToken);
            var options = new StartupOptions
            {
                StateDirectory = stateDirectory,
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
                new PluginCatalogSnapshot(),
                new CodeActionCatalogSnapshot(),
                msBuildRegistrationService.Object,
                codeActionProviderCatalog.Object,
                recoveryStore);
            var tool = new ServerStatusTool(Options.Create(options), service);

            var result = await McpIntegrationTestHost.InvokeServerToolAsync(tool, "server-status", new Dictionary<string, JsonElement>
            {
                ["detail"] = JsonSerializer.SerializeToElement(StatusDetailLevel.Full),
            });

            result.IsError.Should().BeFalse();
            result.StructuredContent!.Value.GetProperty("recovery").EnumerateArray().Should().ContainSingle(
                static status => status.GetProperty("commitId").GetString() == "commit-id");
        }
        finally
        {
            Directory.Delete(stateDirectory, recursive: true);
        }
    }
}
