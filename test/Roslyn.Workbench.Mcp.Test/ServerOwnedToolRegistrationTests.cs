using Microsoft.Extensions.DependencyInjection;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class ServerOwnedToolRegistrationTests
{
    [Fact]
    public void GIVEN_ServiceCollection_WHEN_RegisteringServerOwnedTools_THEN_ShouldRegisterCompleteSingletonCatalogue()
    {
        var services = new ServiceCollection();

        ServerOwnedToolRegistration.AddMcpTools(services);

        var registrations = services.Where(item => item.ServiceType == typeof(McpServerTool)).ToArray();
        registrations.Should().HaveCount(ServerOwnedToolRegistration.BaseToolCount + 2);
        registrations.Should().OnlyContain(item => item.Lifetime == ServiceLifetime.Singleton);
        registrations.Select(item => item.ImplementationType).Should().BeEquivalentTo(
        [
            typeof(GetErrorDetailsTool),
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
            typeof(PrepareErrorReportTool),
            typeof(SubmitErrorReportTool),
        ]);
    }

    [Fact]
    public void GIVEN_NeverConsent_WHEN_RegisteringServerOwnedTools_THEN_ShouldOmitReportingTools()
    {
        var services = new ServiceCollection();
        var options = new ErrorReportingOptions
        {
            ConsentMode = ErrorReportingConsentMode.Never,
        };

        ServerOwnedToolRegistration.AddMcpTools(services, options);

        var registrations = services.Where(item => item.ServiceType == typeof(McpServerTool));
        var implementationTypes = registrations.Select(item => item.ImplementationType);
        implementationTypes.Should().NotContain(typeof(PrepareErrorReportTool));
        implementationTypes.Should().NotContain(typeof(SubmitErrorReportTool));
        registrations.Should().HaveCount(ServerOwnedToolRegistration.BaseToolCount);
    }
}
