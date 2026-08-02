using Roslyn.Workbench.Mcp.Plugins.Resolution;

namespace Roslyn.Workbench.Mcp.Plugins.Test.Resolution;

public sealed class SelectorRejectionFactoryTests
{
    [Theory]
    [InlineData(SelectorResolveStatus.NotFound, "DocumentNotFound", "The document selector did not match any result.")]
    [InlineData(SelectorResolveStatus.Ambiguous, "DocumentAmbiguous", "The document selector matched multiple results.")]
    [InlineData(SelectorResolveStatus.Invalid, "DocumentSelectorInvalid", "The document selector contains an invalid path.")]
    public void GIVEN_UnresolvedSelectorStatus_WHEN_CreatingRejection_THEN_ShouldReturnStructuredFailure(
        SelectorResolveStatus status,
        string expectedCode,
        string expectedMessage)
    {
        var result = SelectorRejectionFactory.Create<object>(status, "Document", "document");

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be(expectedCode);
        result.Error.Message.Should().Be(expectedMessage);
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }
}
