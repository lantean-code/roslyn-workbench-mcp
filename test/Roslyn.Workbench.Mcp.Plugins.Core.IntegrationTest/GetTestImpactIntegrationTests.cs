namespace Roslyn.Workbench.Mcp.Plugins.Core.Test;

public sealed class GetTestImpactIntegrationTests
{
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_ReferencingTestProject_WHEN_GettingTestImpact_THEN_ShouldReturnDirectlyReferencingTest()
    {
        using var fixture = CrossProjectTestImpactFixture.Create();
        await using var coordinator = BundledComponentWorkspaceFactory.CreateInspectionWorkspace();
        var openResult = await coordinator.OpenAsync(fixture.SolutionPath, TestContext.Current.CancellationToken);
        var session = new PluginComponentTestSession(coordinator, BundledPluginCatalogueFactory.CreateCatalogue());

        var result = await session.ExecuteQueryAsync<GetTestImpactRequest, TestImpactData>(
            "get-test-impact",
            new GetTestImpactRequest
            {
                Symbol = new SymbolSelector
                {
                    DocumentationCommentId = "T:CrossProjectTestImpact.Target",
                    Project = new ProjectSelector
                    {
                        Path = "Production/Production.csproj",
                    },
                },
                TestScope = new ScopeSelector
                {
                    Kind = ScopeKind.Project,
                    Project = new ProjectSelector
                    {
                        Path = "Production.Tests/Production.Tests.csproj",
                    },
                },
                IncludeReasons = true,
            },
            TestContext.Current.CancellationToken);

        openResult.Status.Should().Be(WorkspaceOperationStatus.Succeeded);
        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Tests.Items.Should().ContainSingle();
        result.Data.Tests.Items[0].Test!.DisplayName.Should().Contain("AccessingTest");
        result.Data.Tests.Items[0].Location!.Document!.Path.Should().EndWith("TargetTests.cs");
        result.Data.Tests.Items[0].Reasons.Should().Equal("Direct reference to the target symbol or its owning type.");
        result.Data.Tests.HasMore.Should().BeFalse();
        result.Data.Tests.Items.Should().NotContain(static test => test.Test!.DisplayName.Contains("UnrelatedTest", StringComparison.Ordinal));
    }
}
