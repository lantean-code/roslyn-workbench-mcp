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

        var cases = new (FixedCompilerCodeFixTool Target, string ProviderId, IReadOnlyList<string> DiagnosticIds)[]
        {
            (
                new AddAnonymousTypeMemberNameTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.AddAnonymousTypeMemberName.CSharpAddAnonymousTypeMemberNameCodeFixProvider",
                ["CS0746"]),
            (
                new AddConditionalInterpolationParenthesesTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.ConditionalExpressionInStringInterpolation.CSharpAddParenthesesAroundConditionalExpressionInInterpolatedStringCodeFixProvider",
                ["CS8361"]),
            (
                new AddDocumentationCommentNodesTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.DocumentationComments.CSharpAddDocCommentNodesCodeFixProvider",
                ["CS1573"]),
            (
                new AddExplicitCastTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddExplicitCast.CSharpAddExplicitCastCodeFixProvider",
                ["CS0266"]),
            (
                new AddInheritdocTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.AddInheritdoc.AddInheritdocCodeFixProvider",
                ["CS1591"]),
            (
                new AddObsoleteAttributeTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.AddObsoleteAttribute.CSharpAddObsoleteAttributeCodeFixProvider",
                ["CS0612", "CS0618", "CS0672", "CS1062", "CS1064"]),
            (
                new AddYieldTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpAddYieldCodeFixProvider",
                ["CS0029", "CS0266"]),
            (
                new ChangeIteratorReturnTypeTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.Iterator.CSharpChangeToIEnumerableCodeFixProvider",
                ["CS1624"]),
            (
                new DeclareAsNullableTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.DeclareAsNullable.CSharpDeclareAsNullableCodeFixProvider",
                ["CS8603", "CS8600", "CS8625", "CS8618"]),
            (
                new DisambiguateSameVariableTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.DisambiguateSameVariable.CSharpDisambiguateSameVariableCodeFixProvider",
                ["CS1717", "CS1718"]),
            (
                new FixIncorrectConstraintTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.FixIncorrectConstraint.CSharpFixIncorrectConstraintCodeFixProvider",
                ["CS9010", "CS9011"]),
            (
                new FixReturnTypeTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.FixReturnType.CSharpFixReturnTypeCodeFixProvider",
                ["CS0127", "CS1997", "CS0201"]),
            (
                new HideBaseMemberTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.HideBase.HideBaseCodeFixProvider",
                ["CS0108"]),
            (
                new MakeMemberRequiredTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.MakeMemberRequired.CSharpMakeMemberRequiredCodeFixProvider",
                ["CS8618"]),
            (
                new MakeMemberStaticTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.MakeMemberStatic.CSharpMakeMemberStaticCodeFixProvider",
                ["CS0708"]),
            (
                new MakeRefStructTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.MakeRefStruct.MakeRefStructCodeFixProvider",
                ["CS8345"]),
            (
                new MakeStatementAsynchronousTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.MakeStatementAsynchronous.CSharpMakeStatementAsynchronousCodeFixProvider",
                ["CS8414", "CS8418"]),
            (
                new MakeTypeAbstractTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.MakeTypeAbstract.CSharpMakeTypeAbstractCodeFixProvider",
                ["CS0513"]),
            (
                new MakeTypePartialTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.MakeTypePartial.CSharpMakeTypePartialCodeFixProvider",
                ["CS0260"]),
            (
                new OrderModifiersTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.OrderModifiers.CSharpOrderModifiersCodeFixProvider",
                ["CS0267"]),
            (
                new PassCapturedVariablesAsArgumentsTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.MakeLocalFunctionStatic.PassInCapturedVariablesAsArgumentsCodeFixProvider",
                ["CS8421"]),
            (
                new RemoveDocumentationCommentNodeTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.DocumentationComments.CSharpRemoveDocCommentNodeCodeFixProvider",
                ["CS1571", "CS1572", "CS1710"]),
            (
                new RemoveInKeywordTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.RemoveInKeyword.RemoveInKeywordCodeFixProvider",
                ["CS1615"]),
            (
                new RemoveNewModifierTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.RemoveNewModifier.RemoveNewModifierCodeFixProvider",
                ["CS0109"]),
            (
                new RemoveUnusedLocalFunctionTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.RemoveUnusedLocalFunction.CSharpRemoveUnusedLocalFunctionCodeFixProvider",
                ["CS8321"]),
            (
                new ReplaceDefaultLiteralTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.ReplaceDefaultLiteral.CSharpReplaceDefaultLiteralCodeFixProvider",
                ["CS8505"]),
            (
                new TransposeRecordKeywordTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.CodeFixes.TransposeRecordKeyword.CSharpTransposeRecordKeywordCodeFixProvider",
                ["CS9012"]),
            (
                new UnsealClassTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.UnsealClass.CSharpUnsealClassCodeFixProvider",
                ["CS0509"]),
            (
                new UseExplicitArrayInExpressionTreeTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.UseExplicitArrayInExpressionTree.CSharpUseExplicitArrayInExpressionTreeCodeFixProvider",
                ["CS9226"]),
            (
                new UseExplicitTypeForConstTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.UseExplicitTypeForConst.UseExplicitTypeForConstCodeFixProvider",
                ["CS0822"]),
            (
                new UseInterpolatedVerbatimStringTool(locationFixStager.Object),
                "Microsoft.CodeAnalysis.CSharp.UseInterpolatedVerbatimString.CSharpUseInterpolatedVerbatimStringCodeFixProvider",
                ["CS8401"]),
        };

        foreach (var (target, providerId, diagnosticIds) in cases)
        {
            var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

            result.Should().BeEquivalentTo(expected);
            locationFixStager.Verify(item => item.StageLocationCodeFixAsync(
                It.Is<LocationCodeFixRequest>(stageRequest =>
                    stageRequest.Location == request.Location
                    && stageRequest.ExpectedSnapshot == request.ExpectedSnapshot
                    && stageRequest.ProviderId == providerId
                    && stageRequest.DiagnosticIds.SequenceEqual(diagnosticIds)),
                context.Object,
                CancellationToken.None), Times.Once);
        }
    }
}
