namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionCandidateCompatibilityTests
{
    [Fact]
    public void GIVEN_PendingCompilerBackedProviders_WHEN_ReadingCandidateCases_THEN_ShouldTrackEveryProvider()
    {
        var expectedProviderIds = BuiltInCodeFixProviderAssessment.ProviderIds
            .Where(static providerId =>
                BuiltInCodeFixProviderAssessment.GetAuditStatus(providerId)
                    == BuiltInCodeActionAuditStatus.PendingReplayValidation)
            .ToArray();

        var candidateProviderIds = BuiltInCodeActionAuditCases.CandidateCompatibilityCases
            .Select(static auditCase => auditCase.ProviderId)
            .ToArray();

        candidateProviderIds.Should().Equal(expectedProviderIds);
    }
}
