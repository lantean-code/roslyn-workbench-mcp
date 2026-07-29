namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionInventoryTests
{
    [Fact]
    public void GIVEN_PinnedBuiltInComposition_WHEN_ComparingProvidersWithLedger_THEN_ShouldHaveExplicitDispositionForEveryProvider()
    {
        var target = CodeActionCompositionFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        target.Status.IsAvailable.Should().BeTrue(target.Status.Message);

        var composedRefactoringProviderIds = target.RefactoringProviders
            .Select(CodeActionProviderIdentity.GetId)
            .OrderBy(static providerId => providerId, StringComparer.Ordinal)
            .ToArray();

        var supportedRefactoringProviderIds = BuiltInCodeActionLedger.Families
            .Where(static family => family.Kind == BuiltInCodeActionFamilyKind.Refactoring)
            .Select(static family => family.ProviderId)
            .ToArray();

        var assessedRefactoringProviderIds = BuiltInCodeActionProviderAssessment.Entries
            .Where(static entry => entry.Kind == BuiltInCodeActionFamilyKind.Refactoring)
            .Select(static entry => entry.ProviderId);

        var trackedRefactoringProviderIds = supportedRefactoringProviderIds
            .Concat(assessedRefactoringProviderIds)
            .OrderBy(static providerId => providerId, StringComparer.Ordinal)
            .ToArray();

        AssertMatchingInventory(composedRefactoringProviderIds, trackedRefactoringProviderIds);

        var composedCodeFixProviderIds = target.CodeFixProviders
            .Select(CodeActionProviderIdentity.GetId)
            .OrderBy(static providerId => providerId, StringComparer.Ordinal)
            .ToArray();

        var supportedCodeFixProviderIds = BuiltInCodeActionLedger.Families
            .Where(static family => family.Kind == BuiltInCodeActionFamilyKind.CodeFix)
            .Select(static family => family.ProviderId)
            .ToArray();

        var additionalAssessedCodeFixProviderIds = BuiltInCodeActionProviderAssessment.Entries
            .Where(static entry => entry.Kind == BuiltInCodeActionFamilyKind.CodeFix)
            .Select(static entry => entry.ProviderId);

        var trackedCodeFixProviderIds = supportedCodeFixProviderIds
            .Concat(BuiltInCodeFixProviderAssessment.ProviderIds)
            .Concat(additionalAssessedCodeFixProviderIds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static providerId => providerId, StringComparer.Ordinal)
            .ToArray();

        AssertMatchingInventory(composedCodeFixProviderIds, trackedCodeFixProviderIds);

        BuiltInCodeActionLedger.Families
            .Select(static family => family.ProviderId)
            .Should()
            .OnlyHaveUniqueItems();

        BuiltInCodeActionLedger.Families
            .Should()
            .OnlyContain(static family =>
                family.ExecutionMode == CodeActionExecutionMode.Replay
                || family.ExecutionMode == CodeActionExecutionMode.Parameterised);

        var supportedProviderIds = BuiltInCodeActionLedger.Families
            .Select(static family => family.ProviderId)
            .ToHashSet(StringComparer.Ordinal);

        var additionalAssessedProviderIds = BuiltInCodeActionProviderAssessment.Entries
            .Select(static entry => entry.ProviderId)
            .ToArray();

        additionalAssessedProviderIds.Should().OnlyHaveUniqueItems();
        additionalAssessedProviderIds
            .Intersect(supportedProviderIds, StringComparer.Ordinal)
            .Should()
            .BeEmpty();

        AssertAssessedCodeFixClassifications(target.CodeFixProviders);
    }

    private static void AssertAssessedCodeFixClassifications(IReadOnlyList<CodeFixProvider> composedProviders)
    {
        var assessments = BuiltInCodeFixProviderAssessment.ProviderIds
            .Select(static providerId => new
            {
                ProviderId = providerId,
                Status = BuiltInCodeFixProviderAssessment.GetAuditStatus(providerId),
            })
            .ToArray();

        var pendingReplayValidationCount = assessments.Count(
            static assessment => assessment.Status == BuiltInCodeActionAuditStatus.PendingReplayValidation);

        var validatedSupportedCount = assessments.Count(
            static assessment => assessment.Status == BuiltInCodeActionAuditStatus.ValidatedSupported);

        var requiresBuiltInDiagnosticSupportCount = assessments.Count(
            static assessment => assessment.Status == BuiltInCodeActionAuditStatus.RequiresBuiltInDiagnosticSupport);

        var coveredByDedicatedToolCount = assessments.Count(
            static assessment => assessment.Status == BuiltInCodeActionAuditStatus.CoveredByDedicatedTool);

        var excludedCount = assessments.Count(
            static assessment => assessment.Status == BuiltInCodeActionAuditStatus.Excluded);

        assessments.Should().HaveCount(151);
        pendingReplayValidationCount.Should().Be(13);
        validatedSupportedCount.Should().Be(34);
        requiresBuiltInDiagnosticSupportCount.Should().Be(94);
        coveredByDedicatedToolCount.Should().Be(8);
        excludedCount.Should().Be(2);

        var providersById = composedProviders.ToDictionary(
            CodeActionProviderIdentity.GetId,
            StringComparer.Ordinal);

        foreach (var assessment in assessments)
        {
            var provider = providersById[assessment.ProviderId];
            if (assessment.Status is BuiltInCodeActionAuditStatus.PendingReplayValidation
                or BuiltInCodeActionAuditStatus.ValidatedSupported)
            {
                provider.FixableDiagnosticIds
                    .Should()
                    .Contain(static diagnosticId => diagnosticId.StartsWith("CS", StringComparison.Ordinal));
            }

            if (assessment.Status == BuiltInCodeActionAuditStatus.ValidatedSupported)
            {
                var family = BuiltInCodeActionLedger.Families.Single(
                    candidate => candidate.ProviderId == assessment.ProviderId);

                family.ExecutionMode.Should().BeOneOf(
                    CodeActionExecutionMode.Replay,
                    CodeActionExecutionMode.Parameterised);

                family.ToolName.Should().NotBeNullOrWhiteSpace();
            }

            if (assessment.Status == BuiltInCodeActionAuditStatus.RequiresBuiltInDiagnosticSupport)
            {
                provider.FixableDiagnosticIds
                    .Should()
                    .OnlyContain(static diagnosticId => diagnosticId.StartsWith("IDE", StringComparison.Ordinal));
            }
        }
    }

    private static void AssertMatchingInventory(IReadOnlyList<string> composedProviderIds, IReadOnlyList<string> ledgerProviderIds)
    {
        var missingFromLedger = composedProviderIds
            .Except(ledgerProviderIds, StringComparer.Ordinal)
            .ToArray();

        var missingFromComposition = ledgerProviderIds
            .Except(composedProviderIds, StringComparer.Ordinal)
            .ToArray();

        var missingFromLedgerLines = string.Join(Environment.NewLine, missingFromLedger);
        var missingFromLedgerMessage = $"these composed providers require a ledger disposition:{Environment.NewLine}{missingFromLedgerLines}";

        missingFromLedger.Should().BeEmpty(missingFromLedgerMessage);

        var missingFromCompositionLines = string.Join(Environment.NewLine, missingFromComposition);
        var missingFromCompositionMessage = $"these ledger providers were not composed:{Environment.NewLine}{missingFromCompositionLines}";

        missingFromComposition.Should().BeEmpty(missingFromCompositionMessage);
    }
}
