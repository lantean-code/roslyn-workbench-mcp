using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Test.Staging;

public sealed class CodeActionCandidateIdentityTests
{
    [Fact]
    public void GIVEN_EquivalentValuesAndReorderedDiagnosticIds_WHEN_ComparingIdentities_THEN_ShouldBeEqualWithSameHashCode()
    {
        var first = new CodeActionCandidateIdentity(
            "ProviderId",
            "Title",
            "EquivalenceKey",
            [1, 2],
            ["SecondDiagnosticId", "FirstDiagnosticId"]);
        var second = new CodeActionCandidateIdentity(
            "ProviderId",
            "Title",
            "EquivalenceKey",
            [1, 2],
            ["FirstDiagnosticId", "SecondDiagnosticId"]);

        var equals = first.Equals(second);
        var objectEquals = first.Equals((object)second);

        equals.Should().BeTrue();
        objectEquals.Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Theory]
    [InlineData(CandidateDifference.ProviderId)]
    [InlineData(CandidateDifference.Title)]
    [InlineData(CandidateDifference.EquivalenceKey)]
    [InlineData(CandidateDifference.ActionPath)]
    [InlineData(CandidateDifference.DiagnosticIds)]
    public void GIVEN_IdentityValueDiffers_WHEN_ComparingIdentities_THEN_ShouldNotBeEqual(CandidateDifference difference)
    {
        var first = new CodeActionCandidateIdentity(
            "ProviderId",
            "Title",
            "EquivalenceKey",
            [1],
            ["DiagnosticId"]);
        var second = difference switch
        {
            CandidateDifference.ProviderId => new CodeActionCandidateIdentity("OtherProviderId", "Title", "EquivalenceKey", [1], ["DiagnosticId"]),
            CandidateDifference.Title => new CodeActionCandidateIdentity("ProviderId", "OtherTitle", "EquivalenceKey", [1], ["DiagnosticId"]),
            CandidateDifference.EquivalenceKey => new CodeActionCandidateIdentity("ProviderId", "Title", "OtherEquivalenceKey", [1], ["DiagnosticId"]),
            CandidateDifference.ActionPath => new CodeActionCandidateIdentity("ProviderId", "Title", "EquivalenceKey", [2], ["DiagnosticId"]),
            _ => new CodeActionCandidateIdentity("ProviderId", "Title", "EquivalenceKey", [1], ["OtherDiagnosticId"]),
        };

        var equals = first.Equals(second);

        equals.Should().BeFalse();
    }

    [SuppressMessage(
        "Maintainability",
        "CA1508:Avoid dead conditional code",
        Justification = "The assertion deliberately verifies the typed equality contract for a null operand even though static analysis can determine the result.")]
    [Fact]
    public void GIVEN_OtherIdentityIsMissing_WHEN_ComparingIdentities_THEN_ShouldNotBeEqual()
    {
        var target = new CodeActionCandidateIdentity("ProviderId", "Title", null);

        var typedEquals = target.Equals((CodeActionCandidateIdentity?)null);
        var objectEquals = object.Equals(target, new object());

        typedEquals.Should().BeFalse();
        objectEquals.Should().BeFalse();
    }

    [Fact]
    public void GIVEN_OptionalCollectionsAreMissingOrEmpty_WHEN_ComparingIdentities_THEN_ShouldBeEqual()
    {
        var first = new CodeActionCandidateIdentity("ProviderId", "Title", null);
        var second = new CodeActionCandidateIdentity("ProviderId", "Title", null, [], []);

        var equals = first.Equals(second);

        equals.Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void GIVEN_SourceCollectionsAreModified_WHEN_ComparingIdentity_THEN_ShouldRetainConstructedValues()
    {
        var actionPath = new[] { 1 };
        var diagnosticIds = new[] { "DiagnosticId" };
        var target = new CodeActionCandidateIdentity("ProviderId", "Title", "EquivalenceKey", actionPath, diagnosticIds);
        actionPath[0] = 2;
        diagnosticIds[0] = "OtherDiagnosticId";
        var expected = new CodeActionCandidateIdentity("ProviderId", "Title", "EquivalenceKey", [1], ["DiagnosticId"]);

        var equals = target.Equals(expected);

        equals.Should().BeTrue();
    }

#pragma warning disable CA1515 // The enum is part of a public xUnit theory method signature.
    public enum CandidateDifference
    {
        ProviderId,
        Title,
        EquivalenceKey,
        ActionPath,
        DiagnosticIds,
    }
#pragma warning restore CA1515
}
