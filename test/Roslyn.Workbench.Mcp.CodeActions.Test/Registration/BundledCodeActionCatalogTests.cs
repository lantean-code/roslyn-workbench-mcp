namespace Roslyn.Workbench.Mcp.CodeActions.Test.Registration;

public sealed class BundledCodeActionCatalogTests
{
    [Fact]
    public void GIVEN_BundledCodeActionCatalog_WHEN_CreatingCatalog_THEN_ShouldPublishOnlyOrchestrationTools()
    {
        var tools = BundledCodeActionCatalog.Create();

        tools
            .Select(static tool => tool.Metadata.Name)
            .Should()
            .Equal(
                "list-code-actions",
                "prepare-fix-all",
                "stage-code-action");

        tools.Select(static tool => tool.Metadata.Name).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void GIVEN_BundledReferenceProducingQueries_WHEN_CreatingCatalog_THEN_ShouldMarkThemNonIdempotent()
    {
        var tools = BundledCodeActionCatalog.Create();

        var referenceProducingQueries = tools
            .Where(static tool => tool.Kind == CodeActionToolKind.Query)
            .ToArray();

        referenceProducingQueries.Should().HaveCount(2);
        referenceProducingQueries.Should().OnlyContain(static tool => !tool.Metadata.Behavior.Idempotent);
    }
}
