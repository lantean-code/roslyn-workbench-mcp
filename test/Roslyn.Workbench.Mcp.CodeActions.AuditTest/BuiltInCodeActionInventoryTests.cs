namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionInventoryTests
{
    [Fact]
    public void GIVEN_PinnedBuiltInComposition_WHEN_ComparingProvidersWithLedger_THEN_ShouldHaveExplicitDispositionForEveryProvider()
    {
        var target = CodeActionProviderCatalogFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        target.Status.IsAvailable.Should().BeTrue(target.Status.Message);

        var composedRefactoringProviderIds = target.RefactoringProviders
            .Select(static provider => provider.GetType().ToString())
            .OrderBy(static providerId => providerId, StringComparer.Ordinal)
            .ToArray();

        var ledgerRefactoringProviderIds = BuiltInCodeActionLedger.Families
            .Where(static family => family.Kind == BuiltInCodeActionFamilyKind.Refactoring)
            .Select(static family => family.ProviderId)
            .OrderBy(static providerId => providerId, StringComparer.Ordinal)
            .ToArray();

        AssertMatchingInventory(composedRefactoringProviderIds, ledgerRefactoringProviderIds);

        var composedCodeFixProviderIds = target.CodeFixProviders
            .Select(static provider => provider.GetType().ToString())
            .OrderBy(static providerId => providerId, StringComparer.Ordinal)
            .ToArray();

        var ledgerCodeFixProviderIds = BuiltInCodeActionLedger.Families
            .Where(static family => family.Kind == BuiltInCodeActionFamilyKind.CodeFix)
            .Select(static family => family.ProviderId)
            .OrderBy(static providerId => providerId, StringComparer.Ordinal)
            .ToArray();

        AssertMatchingInventory(composedCodeFixProviderIds, ledgerCodeFixProviderIds);

        BuiltInCodeActionLedger.Families
            .Select(static family => family.ProviderId)
            .Should()
            .OnlyHaveUniqueItems();

        BuiltInCodeActionLedger.Families
            .Should()
            .NotContain(static family => family.AuditStatus == BuiltInCodeActionAuditStatus.Unclassified);

        AssertAssessedCodeFixClassifications(target.CodeFixProviders);
    }

    private static void AssertAssessedCodeFixClassifications(IReadOnlyList<CodeFixProvider> composedProviders)
    {
        var assessedProviderIds = BuiltInCodeFixProviderAssessment.ProviderIds.ToHashSet(StringComparer.Ordinal);
        var assessedFamilies = BuiltInCodeActionLedger.Families
            .Where(family => assessedProviderIds.Contains(family.ProviderId))
            .ToArray();

        var pendingReplayValidationCount = assessedFamilies.Count(
            static family => family.AuditStatus == BuiltInCodeActionAuditStatus.PendingReplayValidation);

        var requiresBuiltInDiagnosticSupportCount = assessedFamilies.Count(
            static family => family.AuditStatus == BuiltInCodeActionAuditStatus.RequiresBuiltInDiagnosticSupport);

        var coveredByDedicatedToolCount = assessedFamilies.Count(
            static family => family.AuditStatus == BuiltInCodeActionAuditStatus.CoveredByDedicatedTool);

        var excludedCount = assessedFamilies.Count(
            static family => family.AuditStatus == BuiltInCodeActionAuditStatus.Excluded);

        assessedFamilies.Should().HaveCount(151);
        pendingReplayValidationCount.Should().Be(47);
        requiresBuiltInDiagnosticSupportCount.Should().Be(94);
        coveredByDedicatedToolCount.Should().Be(8);
        excludedCount.Should().Be(2);

        var providersById = composedProviders.ToDictionary(
            static provider => provider.GetType().ToString(),
            StringComparer.Ordinal);

        foreach (var family in assessedFamilies)
        {
            var provider = providersById[family.ProviderId];
            if (family.AuditStatus == BuiltInCodeActionAuditStatus.PendingReplayValidation)
            {
                provider.FixableDiagnosticIds
                    .Should()
                    .Contain(static diagnosticId => diagnosticId.StartsWith("CS", StringComparison.Ordinal));
            }

            if (family.AuditStatus == BuiltInCodeActionAuditStatus.RequiresBuiltInDiagnosticSupport)
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
