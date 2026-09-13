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

    [Fact]
    public void GIVEN_MutationToolsExcluded_WHEN_CreatingCatalog_THEN_ShouldCreateOnlyQueryTools()
    {
        var tools = BundledCodeActionCatalog.Create(includeMutationTools: false);

        tools
            .Select(static tool => tool.Metadata.Name)
            .Should()
            .Equal("list-code-actions", "prepare-fix-all");

        tools.Should().OnlyContain(static tool => tool.Kind == CodeActionToolKind.Query);
    }

    [Fact]
    public void GIVEN_MutationToolsExcluded_WHEN_ReadingToolNames_THEN_ShouldRetainCompleteHostOwnedSet()
    {
        _ = BundledCodeActionCatalog.Create(includeMutationTools: false);

        BundledCodeActionCatalog.ToolNames.Should().Equal(
            "list-code-actions",
            "prepare-fix-all",
            "stage-code-action");
    }

    [Fact]
    public void GIVEN_MutationToolsIncluded_WHEN_CreatingCatalog_THEN_ShouldCreateAllTools()
    {
        var tools = BundledCodeActionCatalog.Create(includeMutationTools: true);

        tools
            .Select(static tool => tool.Metadata.Name)
            .Should()
            .Equal(
                "list-code-actions",
                "prepare-fix-all",
                "stage-code-action");
    }
}
