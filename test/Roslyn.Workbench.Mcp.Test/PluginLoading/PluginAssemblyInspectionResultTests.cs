namespace Roslyn.Workbench.Mcp.Test.PluginLoading;

public sealed class PluginAssemblyInspectionResultTests
{
    [Fact]
    public void GIVEN_PluginEntryPoint_WHEN_CreatingSuccess_THEN_ShouldExposeOnlySuccessfulOutcome()
    {
        var entryPoint = CreateEntryPoint();

        var result = PluginAssemblyInspectionResult.Success([entryPoint]);

        result.Outcome.Should().Be(PluginAssemblyInspectionOutcome.Success);
        result.Succeeded.Should().BeTrue();
        result.WasSkipped.Should().BeFalse();
        result.Failed.Should().BeFalse();
        result.EntryPoints.Should().ContainSingle().Which.Should().BeSameAs(entryPoint);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void GIVEN_NoPluginEntryPoint_WHEN_CreatingSkipped_THEN_ShouldExposeOnlySkippedOutcome()
    {
        var result = PluginAssemblyInspectionResult.Skipped();

        result.Outcome.Should().Be(PluginAssemblyInspectionOutcome.Skipped);
        result.Succeeded.Should().BeFalse();
        result.WasSkipped.Should().BeTrue();
        result.Failed.Should().BeFalse();
        result.EntryPoints.Should().BeNull();
        result.Error.Should().BeNull();
    }

    [Fact]
    public void GIVEN_InspectionError_WHEN_CreatingFailure_THEN_ShouldExposeOnlyFailedOutcome()
    {
        var result = PluginAssemblyInspectionResult.Failure("Error");

        result.Outcome.Should().Be(PluginAssemblyInspectionOutcome.Failure);
        result.Succeeded.Should().BeFalse();
        result.WasSkipped.Should().BeFalse();
        result.Failed.Should().BeTrue();
        result.EntryPoints.Should().BeNull();
        result.Error.Should().Be("Error");
    }

    [Fact]
    public void GIVEN_NoPluginEntryPoints_WHEN_CreatingSuccess_THEN_ShouldRejectInvalidOutcome()
    {
        Action action = static () => PluginAssemblyInspectionResult.Success([]);

        action.Should().Throw<ArgumentException>().WithParameterName("entryPoints");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void GIVEN_BlankError_WHEN_CreatingFailure_THEN_ShouldRejectInvalidOutcome(string error)
    {
        Action action = () => PluginAssemblyInspectionResult.Failure(error);

        action.Should().Throw<ArgumentException>().WithParameterName(nameof(error));
    }

    private static PluginEntryPointMetadata CreateEntryPoint()
    {
        return new PluginEntryPointMetadata
        {
            PluginId = "PluginId",
            DisplayName = "DisplayName",
            SupportedApiVersion = PluginApiVersions.V1,
            Version = "Version",
            EntryTypeName = "EntryTypeName",
        };
    }
}
