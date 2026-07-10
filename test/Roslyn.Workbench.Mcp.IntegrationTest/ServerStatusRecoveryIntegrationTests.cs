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
            CommitRecoveryStore.WriteStatus(stateDirectory, new RecoveryStatus
            {
                CommitId = "commit-id",
                SolutionPath = "/workspace/Sample.csproj",
                State = RecoveryState.RecoveryIncomplete,
                Message = "Message",
            });
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
            var codeActionRuntime = new Mock<ICodeActionRuntime>();
            codeActionRuntime
                .SetupGet(item => item.Status)
                .Returns(new CodeActionRuntimeStatus
                {
                    IsAvailable = true,
                });
            var service = new ServerStatusService(
                Options.Create(options),
                new PluginCatalogSnapshot(),
                new CodeActionCatalogSnapshot(),
                msBuildRegistrationService.Object,
                codeActionRuntime.Object,
                new RecoveryStatusReader());
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
