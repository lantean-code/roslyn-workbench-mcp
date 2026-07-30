namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class BuiltInCodeActionInventoryTests
{
    [Fact]
    public void GIVEN_PinnedBuiltInComposition_WHEN_InspectingProviders_THEN_ShouldRetainExactProviderCountAndUniqueIdentities()
    {
        var target = CodeActionCompositionFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        target.Status.IsAvailable.Should().BeTrue(target.Status.Message);

        var providerIds = target.RefactoringProviders
            .Select(CodeActionProviderIdentity.GetId)
            .Concat(target.CodeFixProviders.Select(CodeActionProviderIdentity.GetId))
            .ToArray();

        providerIds.Should().HaveCount(250);
        providerIds.Should().OnlyHaveUniqueItems();
        target.RefactoringProviders.Should().NotBeEmpty();
        target.CodeFixProviders.Should().NotBeEmpty();
    }

    [Fact]
    public void GIVEN_ProviderAssessments_WHEN_InspectingDispositions_THEN_ShouldNotRequireDedicatedMcpTools()
    {
        BuiltInCodeActionProviderAssessment.Entries
            .Select(static entry => entry.ProviderId)
            .Should()
            .OnlyHaveUniqueItems();

        Enum.GetNames<BuiltInCodeActionAuditStatus>()
            .Should()
            .NotContain(static name => name.Contains("Dedicated", StringComparison.Ordinal));
    }
}
