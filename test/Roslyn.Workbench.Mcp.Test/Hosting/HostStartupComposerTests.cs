namespace Roslyn.Workbench.Mcp.Test.Hosting;

public sealed class HostStartupComposerTests
{
    [Fact]
    public void GIVEN_ValidStartupConfiguration_WHEN_Composing_THEN_ShouldReturnConfigurationAndCodeActionCatalogue()
    {
        var result = HostStartupComposer.Compose(
        [
            "--operational-mode=autonomous-trusted",
            "--enable-plugins",
            "--plugin-directory=/missing/plugins",
            "--default-max-results=25",
            "--code-action-reference-lifetime=00:10:00",
            "--max-transaction-revisions=30",
            "--max-concurrent-queries=4",
            "--tool-output-schema-mode=full",
            "--state-directory=/state",
        ]);

        result.Options.Should().BeSameAs(result.Configuration.Options);
        result.Configuration.Warnings.Should().BeEmpty();
        result.Policy.Mode.Should().Be(OperationalMode.AutonomousTrusted);
        result.CodeActions.Tools
            .Select(static tool => tool.Metadata.Name)
            .Should()
            .Equal(
                "list-code-actions",
                "prepare-fix-all",
                "stage-code-action");
    }

    [Fact]
    public void GIVEN_DefaultInspectionMode_WHEN_Composing_THEN_ShouldPublishOnlyCodeActionQueries()
    {
        var result = HostStartupComposer.Compose([]);

        result.Policy.Mode.Should().Be(OperationalMode.InspectionOnly);
        result.CodeActions.Tools
            .Select(static tool => tool.Metadata.Name)
            .Should()
            .Equal(
                "list-code-actions",
                "prepare-fix-all");

        result.CodeActions.ReservedToolNames.Should().Equal(
            "list-code-actions",
            "prepare-fix-all",
            "stage-code-action");
    }
}
