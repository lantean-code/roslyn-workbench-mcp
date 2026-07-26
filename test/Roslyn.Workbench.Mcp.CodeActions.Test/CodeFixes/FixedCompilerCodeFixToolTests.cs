namespace Roslyn.Workbench.Mcp.CodeActions.Test.CodeFixes;

public sealed class FixedCompilerCodeFixToolTests
{
    [Fact]
    public async Task GIVEN_FixedCompilerCodeFixTool_WHEN_CallingExecuteAsync_THEN_ShouldStageItsProviderAndDiagnostic()
    {
        var expected = CodeActionExecutionResult.Success(MutationCandidateTestData.CreateWorkspaceCandidate());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new FixedCompilerCodeFixRequest
        {
            Location = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };

        var locationFixStager = new Mock<ILocationCodeFixStager>();
        locationFixStager
            .Setup(item => item.StageLocationCodeFixAsync(
                It.IsAny<LocationCodeFixRequest>(),
                context.Object,
                CancellationToken.None))
            .ReturnsAsync(expected);

        var cases = new (FixedCompilerCodeFixTool Target, string ProviderId, string DiagnosticId)[]
        {
            (
                new AddAnonymousTypeMemberNameTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.AddAnonymousTypeMemberName.CSharpAddAnonymousTypeMemberNameCodeFixProvider",
                "CS0746"),
            (
                new AddConditionalInterpolationParenthesesTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.ConditionalExpressionInStringInterpolation.CSharpAddParenthesesAroundConditionalExpressionInInterpolatedStringCodeFixProvider",
                "CS8361"),
            (
                new AddExplicitCastTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddExplicitCast.CSharpAddExplicitCastCodeFixProvider",
                "CS0266"),
            (
                new AddInheritdocTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddInheritdoc.AddInheritdocCodeFixProvider",
                "CS1591"),
            (
                new RemoveInKeywordTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.RemoveInKeyword.RemoveInKeywordCodeFixProvider",
                "CS1615"),
            (
                new RemoveNewModifierTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.RemoveNewModifier.RemoveNewModifierCodeFixProvider",
                "CS0109"),
            (
                new ReplaceDefaultLiteralTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.ReplaceDefaultLiteral.CSharpReplaceDefaultLiteralCodeFixProvider",
                "CS8505"),
            (
                new UseExplicitTypeForConstTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.UseExplicitTypeForConst.UseExplicitTypeForConstCodeFixProvider",
                "CS0822"),
        };

        foreach (var (target, providerId, diagnosticId) in cases)
        {
            var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

            result.Should().BeEquivalentTo(expected);
            locationFixStager.Verify(item => item.StageLocationCodeFixAsync(
                It.Is<LocationCodeFixRequest>(stageRequest =>
                    stageRequest.Location == request.Location
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == providerId
                    && stageRequest.DiagnosticIds.Count == 1
                    && stageRequest.DiagnosticIds[0] == diagnosticId),
                context.Object,
                CancellationToken.None), Times.Once);
        }
    }
}
