using Microsoft.Extensions.DependencyInjection;

namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class ReceiptServiceCompositionTests
{
    [Theory]
    [InlineData((int)OperationalMode.InspectionOnly, false)]
    [InlineData((int)OperationalMode.Transactional, false)]
    [InlineData((int)OperationalMode.ApprovalRequired, true)]
    [InlineData((int)OperationalMode.AutonomousTrusted, false)]
    public void GIVEN_OperationalMode_WHEN_RegisteringHostServices_THEN_ShouldComposeReceiptStoreOnlyForReceiptApproval(
        int modeValue,
        bool expectedReceiptStore)
    {
        var services = new ServiceCollection();
        var policy = OperationalPolicyResolver.Resolve((OperationalMode)modeValue);

        services.AddHostServices(policy);

        var registration = services.SingleOrDefault(item => item.ServiceType == typeof(ITransactionReceiptStore));
        (registration is not null).Should().Be(expectedReceiptStore);
        if (expectedReceiptStore)
        {
            var requiredRegistration = registration
                ?? throw new InvalidOperationException("Receipt approval must register its receipt store.");

            requiredRegistration.ImplementationType.Should().Be<TransactionReceiptStore>();
            requiredRegistration.Lifetime.Should().Be(ServiceLifetime.Singleton);
        }
    }

    [Theory]
    [InlineData((int)OperationalMode.InspectionOnly, false)]
    [InlineData((int)OperationalMode.Transactional, false)]
    [InlineData((int)OperationalMode.ApprovalRequired, true)]
    [InlineData((int)OperationalMode.AutonomousTrusted, false)]
    public void GIVEN_OperationalMode_WHEN_RegisteringWorkspaceServices_THEN_ShouldComposeReviewServicesOnlyForReceiptApproval(
        int modeValue,
        bool expectedReviewServices)
    {
        var services = new ServiceCollection();
        var policy = OperationalPolicyResolver.Resolve((OperationalMode)modeValue);

        services.AddWorkspaceServices(policy);

        var reviewServiceTypes = new[]
        {
            typeof(ITransactionReviewBuilder),
            typeof(ITransactionReviewDocumentFactory),
            typeof(ITransactionReviewIdentityService),
        };

        foreach (var reviewServiceType in reviewServiceTypes)
        {
            var registration = services.SingleOrDefault(item => item.ServiceType == reviewServiceType);
            (registration is not null).Should().Be(expectedReviewServices);
        }
    }

    [Theory]
    [InlineData((int)CommitValidationPolicy.None, false)]
    [InlineData((int)CommitValidationPolicy.NoNewCompilerErrors, true)]
    public void GIVEN_CommitValidationPolicy_WHEN_RegisteringWorkspaceServices_THEN_ShouldConditionallyComposeCompilerValidation(
        int validationValue,
        bool expectedService)
    {
        var services = new ServiceCollection();
        var policy = OperationalPolicyResolver.Resolve(
            OperationalMode.AutonomousTrusted,
            (CommitValidationPolicy)validationValue);

        services.AddWorkspaceServices(policy);

        var registration = services.SingleOrDefault(
            item => item.ServiceType == typeof(ITransactionCompilerValidationService));

        (registration is not null).Should().Be(expectedService);
        if (expectedService)
        {
            registration!.ImplementationType.Should().Be<TransactionCompilerValidationService>();
            registration.Lifetime.Should().Be(ServiceLifetime.Singleton);
        }
    }
}
