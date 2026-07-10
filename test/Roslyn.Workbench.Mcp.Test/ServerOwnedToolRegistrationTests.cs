using Microsoft.Extensions.DependencyInjection;

using Roslyn.Workbench.Mcp.Tools;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class ServerOwnedToolRegistrationTests
{
    [Fact]
    public void GIVEN_ServiceCollection_WHEN_RegisteringServerOwnedTools_THEN_ShouldRegisterCompleteSingletonCatalogue()
    {
        var services = new ServiceCollection();

        ServerOwnedToolRegistration.AddMcpTools(services);

        var registrations = services.Where(item => item.ServiceType == typeof(McpServerTool)).ToArray();
        registrations.Should().HaveCount(ServerOwnedToolRegistration.ToolCount);
        registrations.Should().OnlyContain(item => item.Lifetime == ServiceLifetime.Singleton);
        registrations.Select(item => item.ImplementationType).Should().BeEquivalentTo(
        [
            typeof(ServerStatusTool),
            typeof(WorkspaceOpenTool),
            typeof(WorkspaceListTool),
            typeof(WorkspaceCloseTool),
            typeof(WorkspaceStatusTool),
            typeof(WorkspaceReloadTool),
            typeof(TransactionStartTool),
            typeof(TransactionPreviewTool),
            typeof(TransactionHistoryTool),
            typeof(TransactionCommitTool),
            typeof(TransactionRollbackTool),
        ]);
    }
}
