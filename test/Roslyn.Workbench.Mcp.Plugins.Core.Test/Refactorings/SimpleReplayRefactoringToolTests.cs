namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

public sealed class SimpleReplayRefactoringToolTests
{
    [Fact]
    public async Task GIVEN_AddDebuggerDisplayRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new AddDebuggerDisplayTool(), "Microsoft.CodeAnalysis.CSharp.AddDebuggerDisplay.CSharpAddDebuggerDisplayCodeRefactoringProvider", title: "Add 'DebuggerDisplay' attribute");
    }

    [Fact]
    public async Task GIVEN_AddNullChecksRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new AddNullChecksTool(), "Microsoft.CodeAnalysis.CSharp.InitializeParameter.CSharpAddParameterCheckCodeRefactoringProvider", title: "Add null check");
    }

    [Fact]
    public async Task GIVEN_ConvertAnonymousTypeToTupleRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ConvertAnonymousTypeToTupleTool(), "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToTupleCodeRefactoringProvider", title: "Convert to tuple");
    }

    [Fact]
    public async Task GIVEN_ConvertBetweenRegularAndVerbatimInterpolatedStringRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ConvertBetweenRegularAndVerbatimInterpolatedStringTool(), "Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimInterpolatedStringCodeRefactoringProvider");
    }

    [Fact]
    public async Task GIVEN_ConvertBetweenRegularAndVerbatimStringRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ConvertBetweenRegularAndVerbatimStringTool(), "Microsoft.CodeAnalysis.CSharp.ConvertBetweenRegularAndVerbatimString.ConvertBetweenRegularAndVerbatimStringCodeRefactoringProvider");
    }

    [Fact]
    public async Task GIVEN_ConvertDirectCastToTryCastRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ConvertDirectCastToTryCastTool(), "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertDirectCastToTryCastCodeRefactoringProvider", title: "Change to 'as' expression");
    }

    [Fact]
    public async Task GIVEN_ConvertForEachToForRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ConvertForEachToForTool(), "Microsoft.CodeAnalysis.CSharp.ConvertForEachToFor.CSharpConvertForEachToForCodeRefactoringProvider", title: "Convert to 'for'");
    }

    [Fact]
    public async Task GIVEN_ConvertForToForeachRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ConvertForToForeachTool(), "Microsoft.CodeAnalysis.CSharp.ConvertForToForEach.CSharpConvertForToForEachCodeRefactoringProvider", title: "Convert to 'foreach'");
    }

    [Fact]
    public async Task GIVEN_ConvertLocalFunctionToMethodRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ConvertLocalFunctionToMethodTool(), "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.ConvertLocalFunctionToMethod.CSharpConvertLocalFunctionToMethodCodeRefactoringProvider", title: "Convert to method");
    }

    [Fact]
    public async Task GIVEN_ConvertPrimaryToRegularConstructorRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ConvertPrimaryToRegularConstructorTool(), "Microsoft.CodeAnalysis.CSharp.ConvertPrimaryToRegularConstructor.ConvertPrimaryToRegularConstructorCodeRefactoringProvider", title: "Convert to regular constructor");
    }

    [Fact]
    public async Task GIVEN_ConvertToRecordRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ConvertToRecordTool(), "Microsoft.CodeAnalysis.CSharp.ConvertToRecord.CSharpConvertToRecordRefactoringProvider", title: "Convert to positional record");
    }

    [Fact]
    public async Task GIVEN_ConvertTryCastToDirectCastRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ConvertTryCastToDirectCastTool(), "Microsoft.CodeAnalysis.CSharp.ConvertCast.CSharpConvertTryCastToDirectCastCodeRefactoringProvider", title: "Change to cast");
    }

    [Fact]
    public async Task GIVEN_IntroduceUsingStatementRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new IntroduceUsingStatementTool(), "Microsoft.CodeAnalysis.CSharp.IntroduceUsingStatement.CSharpIntroduceUsingStatementCodeRefactoringProvider", title: "Introduce 'using' statement");
    }

    [Fact]
    public async Task GIVEN_InvertConditionalRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new InvertConditionalTool(), "Microsoft.CodeAnalysis.CSharp.InvertConditional.CSharpInvertConditionalCodeRefactoringProvider", title: "Invert conditional");
    }

    [Fact]
    public async Task GIVEN_InvertIfRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new InvertIfTool(), "Microsoft.CodeAnalysis.CSharp.InvertIf.CSharpInvertIfCodeRefactoringProvider", title: "Invert if");
    }

    [Fact]
    public async Task GIVEN_InvertLogicalRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new InvertLogicalTool(), "Microsoft.CodeAnalysis.CSharp.InvertLogical.CSharpInvertLogicalCodeRefactoringProvider", titleStartsWith: "Replace '");
    }

    [Fact]
    public async Task GIVEN_MakeLocalFunctionStaticRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new MakeLocalFunctionStaticTool(), "Microsoft.CodeAnalysis.CSharp.MakeLocalFunctionStatic.MakeLocalFunctionStaticCodeRefactoringProvider", title: "Make local function 'static'");
    }

    [Fact]
    public async Task GIVEN_MoveDeclarationNearReferenceRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new MoveDeclarationNearReferenceTool(), "Microsoft.CodeAnalysis.CSharp.MoveDeclarationNearReference.CSharpMoveDeclarationNearReferenceCodeRefactoringProvider", titleStartsWith: "Move declaration near reference");
    }

    [Fact]
    public async Task GIVEN_NameTupleElementRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new NameTupleElementTool(), "Microsoft.CodeAnalysis.CSharp.NameTupleElement.CSharpNameTupleElementCodeRefactoringProvider", titleStartsWith: "Add tuple element name '");
    }

    [Fact]
    public async Task GIVEN_ReplaceConditionalWithStatementsRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ReplaceConditionalWithStatementsTool(), "Microsoft.CodeAnalysis.CSharp.ReplaceConditionalWithStatements.CSharpReplaceConditionalWithStatementsCodeRefactoringProvider", title: "Replace conditional expression with statements");
    }

    [Fact]
    public async Task GIVEN_ReplaceDocCommentTextWithTagRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ReplaceDocCommentTextWithTagTool(), "Microsoft.CodeAnalysis.CSharp.ReplaceDocCommentTextWithTag.CSharpReplaceDocCommentTextWithTagCodeRefactoringProvider", titleStartsWith: "Use <");
    }

    [Fact]
    public async Task GIVEN_ReverseForStatementRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new ReverseForStatementTool(), "Microsoft.CodeAnalysis.CSharp.ReverseForStatement.CSharpReverseForStatementCodeRefactoringProvider", title: "Reverse 'for' statement");
    }

    [Fact]
    public async Task GIVEN_UseExplicitTypeRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new UseExplicitTypeTool(), "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseExplicitType.UseExplicitTypeCodeRefactoringProvider", title: "Use explicit type");
    }

    [Fact]
    public async Task GIVEN_UseImplicitTypeRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new UseImplicitTypeTool(), "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseImplicitType.UseImplicitTypeCodeRefactoringProvider", title: "Use implicit type");
    }

    [Fact]
    public async Task GIVEN_UseRecursivePatternsRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        await AssertLocationReplaySelectionAsync(new UseRecursivePatternsTool(), "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.UseRecursivePatterns.UseRecursivePatternsCodeRefactoringProvider", title: "Use recursive patterns");
    }

    [Fact]
    public async Task GIVEN_AddImportRequestWithoutSimplifyAllOccurrences_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        var target = new AddImportTool();
        var request = new AddImportRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            SimplifyAllOccurrences = false,
        };

        await AssertDelegationAsync(
            contextBuilder => contextBuilder.Build(),
            (context, cancellationToken) => target.ExecuteAsync(request, context, cancellationToken),
            "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
            request.Selection,
            request.ExpectedSnapshot,
            titleStartsWith: "Add 'using ",
            titleDoesNotContain: "simplify all occurrences");
    }

    [Fact]
    public async Task GIVEN_AddImportRequestWithSimplifyAllOccurrences_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        var target = new AddImportTool();
        var request = new AddImportRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            SimplifyAllOccurrences = true,
        };

        await AssertDelegationAsync(
            contextBuilder => contextBuilder.Build(),
            (context, cancellationToken) => target.ExecuteAsync(request, context, cancellationToken),
            "Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeRefactoringProvider",
            request.Selection,
            request.ExpectedSnapshot,
            titleStartsWith: "Add 'using ");
    }

    [Fact]
    public async Task GIVEN_ConvertAutoPropertyToFullPropertyRequest_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        var target = new ConvertAutoPropertyToFullPropertyTool();
        var request = new ConvertAutoPropertyToFullPropertyRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };

        await AssertDelegationAsync(
            contextBuilder => contextBuilder.Build(),
            (context, cancellationToken) => target.ExecuteAsync(request, context, cancellationToken),
            "Microsoft.CodeAnalysis.CSharp.ConvertAutoPropertyToFullProperty.CSharpConvertAutoPropertyToFullPropertyCodeRefactoringProvider",
            request.Selection,
            request.ExpectedSnapshot,
            title: "Convert to full property");
    }

    [Fact]
    public async Task GIVEN_UseNamedArgumentsRequestWithoutTrailingArguments_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        var target = new UseNamedArgumentsTool();
        var request = new UseNamedArgumentsRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            IncludeTrailingArguments = false,
        };

        await AssertDelegationAsync(
            contextBuilder => contextBuilder.Build(),
            (context, cancellationToken) => target.ExecuteAsync(request, context, cancellationToken),
            "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
            request.Selection,
            request.ExpectedSnapshot,
            titleStartsWith: "Add argument name '",
            titleDoesNotContain: "including trailing arguments");
    }

    [Fact]
    public async Task GIVEN_UseNamedArgumentsRequestWithTrailingArguments_WHEN_CallingExecuteAsync_THEN_ShouldDelegateReplaySelection()
    {
        var target = new UseNamedArgumentsTool();
        var request = new UseNamedArgumentsRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            IncludeTrailingArguments = true,
        };

        await AssertDelegationAsync(
            contextBuilder => contextBuilder.Build(),
            (context, cancellationToken) => target.ExecuteAsync(request, context, cancellationToken),
            "Microsoft.CodeAnalysis.CSharp.UseNamedArguments.CSharpUseNamedArgumentsCodeRefactoringProvider",
            request.Selection,
            request.ExpectedSnapshot,
            titleStartsWith: "Add argument name '");
    }

    private static async Task AssertLocationReplaySelectionAsync(
        MutationToolHandler<LocationRefactoringRequest> target,
        string providerId,
        string? title = null,
        string? titleStartsWith = null,
        string? titleDoesNotContain = null)
    {
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };

        await AssertDelegationAsync(
            contextBuilder => contextBuilder.Build(),
            (context, cancellationToken) => target.ExecuteAsync(request, context, cancellationToken),
            providerId,
            request.Selection,
            request.ExpectedSnapshot,
            title,
            titleStartsWith,
            titleDoesNotContain);
    }

    private static async Task AssertDelegationAsync(
        Func<MutationContextBuilder, IMutationContext> buildContext,
        Func<IMutationContext, CancellationToken, ValueTask<PluginExecutionResult<MutationProposal>>> invokeAsync,
        string providerId,
        LocationSelector? selection,
        SnapshotPrecondition? expectedSnapshot,
        string? title = null,
        string? titleStartsWith = null,
        string? titleDoesNotContain = null)
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var replayExecutor = new Mock<IReplayCodeActionExecutor>();
        var services = new ToolExecutionServicesBuilder()
            .WithReplayCodeActionExecutor(replayExecutor.Object)
            .Build();
        var context = buildContext(new MutationContextBuilder().WithToolExecutionServices(services));

        replayExecutor
            .Setup(executor => executor.StageReplaySelectionAsync(
                It.IsAny<LocationSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IMutationContext>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<int>?>()))
            .ReturnsAsync(expected);

        var result = await invokeAsync(context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        replayExecutor.Verify(executor => executor.StageReplaySelectionAsync(
            selection,
            expectedSnapshot,
            context,
            CancellationToken.None,
            providerId,
            title,
            titleStartsWith,
            titleDoesNotContain,
            null,
            null), Times.Once);
    }
}
