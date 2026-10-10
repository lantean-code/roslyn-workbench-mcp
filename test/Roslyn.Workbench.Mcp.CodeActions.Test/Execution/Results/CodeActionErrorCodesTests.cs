namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution.Results;

public sealed class CodeActionErrorCodesTests
{
    [Fact]
    [Trait("Category", "Contract")]
    public void GIVEN_CodeActionErrorCodes_WHEN_ComparingCompatibilityContract_THEN_ShouldRetainExactValues()
    {
        var actual = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [nameof(CodeActionErrorCodes.ActionAmbiguous)] = CodeActionErrorCodes.ActionAmbiguous,
            [nameof(CodeActionErrorCodes.ActionExpired)] = CodeActionErrorCodes.ActionExpired,
            [nameof(CodeActionErrorCodes.ActionReferenceCapacityExceeded)] = CodeActionErrorCodes.ActionReferenceCapacityExceeded,
            [nameof(CodeActionErrorCodes.ActionUnavailable)] = CodeActionErrorCodes.ActionUnavailable,
            [nameof(CodeActionErrorCodes.CodeActionDocumentPathUnavailable)] = CodeActionErrorCodes.CodeActionDocumentPathUnavailable,
            [nameof(CodeActionErrorCodes.CodeActionLocationUnavailable)] = CodeActionErrorCodes.CodeActionLocationUnavailable,
            [nameof(CodeActionErrorCodes.CodeActionProjectionFailed)] = CodeActionErrorCodes.CodeActionProjectionFailed,
            [nameof(CodeActionErrorCodes.CodeActionsUnavailable)] = CodeActionErrorCodes.CodeActionsUnavailable,
            [nameof(CodeActionErrorCodes.FixAllLimitExceeded)] = CodeActionErrorCodes.FixAllLimitExceeded,
            [nameof(CodeActionErrorCodes.FixAllUnavailable)] = CodeActionErrorCodes.FixAllUnavailable,
            [nameof(CodeActionErrorCodes.InvalidRange)] = CodeActionErrorCodes.InvalidRange,
        };

        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ActionAmbiguous"] = "ActionAmbiguous",
            ["ActionExpired"] = "ActionExpired",
            ["ActionReferenceCapacityExceeded"] = "ActionReferenceCapacityExceeded",
            ["ActionUnavailable"] = "ActionUnavailable",
            ["CodeActionDocumentPathUnavailable"] = "CodeActionDocumentPathUnavailable",
            ["CodeActionLocationUnavailable"] = "CodeActionLocationUnavailable",
            ["CodeActionProjectionFailed"] = "CodeActionProjectionFailed",
            ["CodeActionsUnavailable"] = "CodeActionsUnavailable",
            ["FixAllLimitExceeded"] = "FixAllLimitExceeded",
            ["FixAllUnavailable"] = "FixAllUnavailable",
            ["InvalidRange"] = "InvalidRange",
        };

        actual.Should().BeEquivalentTo(expected);
    }
}
