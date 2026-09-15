using Microsoft.Extensions.DependencyInjection;

namespace Roslyn.Workbench.Mcp.Test;

public sealed class ServerOwnedToolRegistrationTests
{
    [Fact]
    public void GIVEN_DefaultInspectionPolicy_WHEN_RegisteringServerOwnedTools_THEN_ShouldOmitMutationTools()
    {
        var services = new ServiceCollection();

        ServerOwnedToolRegistration.AddMcpTools(services);

        var registrations = services.Where(item => item.ServiceType == typeof(McpServerTool)).ToArray();
        registrations.Should().HaveCount(ServerOwnedToolRegistration.InspectionToolCount + 2);
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
            typeof(PrepareErrorReportTool),
            typeof(SubmitErrorReportTool),
        ]);
    }

    [Fact]
    public void GIVEN_MutationEnabledPolicy_WHEN_RegisteringServerOwnedTools_THEN_ShouldRegisterTransactionTools()
    {
        var services = new ServiceCollection();
        var policy = OperationalPolicyResolver.Resolve(OperationalMode.Transactional);

        ServerOwnedToolRegistration.AddMcpTools(services, new ErrorReportingOptions(), policy);

        var registrations = services.Where(item => item.ServiceType == typeof(McpServerTool)).ToArray();
        registrations.Should().HaveCount(ServerOwnedToolRegistration.BaseToolCount + 2);
        registrations.Select(item => item.ImplementationType).Should().Contain(
        [
            typeof(TransactionStartTool),
            typeof(TransactionPreviewTool),
            typeof(TransactionHistoryTool),
            typeof(TransactionCommitTool),
            typeof(TransactionRollbackTool),
        ]);
    }

    [Fact]
    public void GIVEN_ReceiptApprovalPolicy_WHEN_RegisteringServerOwnedTools_THEN_ShouldPublishOnlyReceiptReviewWorkflow()
    {
        var services = new ServiceCollection();
        var policy = OperationalPolicyResolver.Resolve(OperationalMode.ApprovalRequired);

        ServerOwnedToolRegistration.AddMcpTools(services, new ErrorReportingOptions(), policy);

        var implementations = services
            .Where(item => item.ServiceType == typeof(McpServerTool))
            .Select(item => item.ImplementationType)
            .ToArray();

        implementations.Should().Contain(typeof(TransactionReviewTool));
        implementations.Should().Contain(typeof(TransactionReceiptCommitTool));
        implementations.Should().NotContain(typeof(TransactionPreviewTool));
        implementations.Should().NotContain(typeof(TransactionCommitTool));
    }

    [Fact]
    public void GIVEN_NeverConsent_WHEN_RegisteringServerOwnedTools_THEN_ShouldOmitReportingTools()
    {
        var services = new ServiceCollection();
        var options = new ErrorReportingOptions
        {
            ConsentMode = ErrorReportingConsentMode.Never,
        };

        var policy = OperationalPolicyResolver.Resolve(OperationalMode.InspectionOnly);

        ServerOwnedToolRegistration.AddMcpTools(services, options, policy);

        var registrations = services.Where(item => item.ServiceType == typeof(McpServerTool));
        var implementationTypes = registrations.Select(item => item.ImplementationType);
        implementationTypes.Should().NotContain(typeof(PrepareErrorReportTool));
        implementationTypes.Should().NotContain(typeof(SubmitErrorReportTool));
        registrations.Should().HaveCount(ServerOwnedToolRegistration.InspectionToolCount);
    }
}
