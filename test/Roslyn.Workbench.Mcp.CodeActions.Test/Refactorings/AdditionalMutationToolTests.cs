namespace Roslyn.Workbench.Mcp.CodeActions.Test.Refactorings;

public sealed class AddAwaitToolTests
{
    [Fact]
    public async Task GIVEN_AwaitKind_WHEN_CallingExecuteAsync_THEN_ShouldDelegateDefaultAwaitReplaySelection()
    {
        await AdditionalMutationToolTestHelpers.AssertReplaySelectionAsync(
            new AddAwaitTool(),
            new AddAwaitRequest
            {
                Kind = AddAwaitKind.Await,
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
            },
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
            title: "Add 'await'",
            actionPath: [0]);
    }

    [Fact]
    public async Task GIVEN_AwaitConfigureAwaitFalseKind_WHEN_CallingExecuteAsync_THEN_ShouldDelegateConfigureAwaitReplaySelection()
    {
        await AdditionalMutationToolTestHelpers.AssertReplaySelectionAsync(
            new AddAwaitTool(),
            new AddAwaitRequest
            {
                Kind = AddAwaitKind.AwaitConfigureAwaitFalse,
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
            },
            "Microsoft.CodeAnalysis.CSharp.CodeRefactorings.AddAwait.CSharpAddAwaitCodeRefactoringProvider",
            title: "Add 'await' and 'ConfigureAwait(false)'",
            actionPath: [1]);
    }
}

public sealed class AddMissingUsingsToolTests
{
    [Fact]
    public async Task GIVEN_PreferGlobalUsingsIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnUnsupportedOption()
    {
        var target = new AddMissingUsingsTool();
        var context = new MutationContextBuilder().Build();

        var result = await target.ExecuteAsync(new AddMissingUsingsRequest
        {
            PreferGlobalUsings = true,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("UnsupportedOption");
    }

    [Fact]
    public async Task GIVEN_PreferGlobalUsingsIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldStageScopedCodeFix()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var request = new AddMissingUsingsRequest
        {
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Project,
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var context = new MutationContextBuilder()
            .WithStageScopedCodeFixAsync((stageRequest, cancellationToken) =>
            {
                stageRequest.Scope.Should().Be(request.Scope);
                stageRequest.ExpectedSnapshot.Should().Be(request.ExpectedSnapshot);
                stageRequest.ProviderId.Should().Be("Microsoft.CodeAnalysis.CSharp.AddImport.CSharpAddImportCodeFixProvider");
                stageRequest.DiagnosticIds.Should().BeEquivalentTo(["CS0103", "CS0246"]);
                cancellationToken.Should().Be(CancellationToken.None);
                return ValueTask.FromResult(expected);
            })
            .Build();
        var target = new AddMissingUsingsTool();

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }
}

public sealed class ConvertAnonymousTypeToClassToolTests
{
    [Fact]
    public async Task GIVEN_ClassKind_WHEN_CallingExecuteAsync_THEN_ShouldDelegateClassReplaySelection()
    {
        await AdditionalMutationToolTestHelpers.AssertReplaySelectionAsync(
            new ConvertAnonymousTypeToClassTool(),
            new ConvertAnonymousTypeToClassRequest
            {
                Kind = ConvertAnonymousTypeToClassKind.Class,
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
            },
            "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider",
            title: "Convert to class");
    }

    [Fact]
    public async Task GIVEN_RecordKind_WHEN_CallingExecuteAsync_THEN_ShouldDelegateRecordReplaySelection()
    {
        await AdditionalMutationToolTestHelpers.AssertReplaySelectionAsync(
            new ConvertAnonymousTypeToClassTool(),
            new ConvertAnonymousTypeToClassRequest
            {
                Kind = ConvertAnonymousTypeToClassKind.Record,
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
            },
            "Microsoft.CodeAnalysis.CSharp.ConvertAnonymousType.CSharpConvertAnonymousTypeToClassCodeRefactoringProvider",
            title: "Convert to record");
    }
}

public sealed class ConvertExpressionBodyToolTests
{
    [Fact]
    public async Task GIVEN_PrimaryProviderSucceeds_WHEN_CallingExecuteAsync_THEN_ShouldReturnPrimaryResult()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertExpressionBodyTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Never);
    }

    [Fact]
    public async Task GIVEN_PrimaryProviderReturnsCodeActionUnavailable_WHEN_CallingExecuteAsync_THEN_ShouldFallbackToLambdaProvider()
    {
        var fallback = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var context = new Mock<ICodeActionMutationContext>();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertExpressionBodyTool();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(PluginExecutionResult<MutationProposal>.Rejected(new ToolError
            {
                Code = "CodeActionUnavailable",
                Message = "CodeActionUnavailable",
            }));
        context
            .Setup(item => item.StageReplaySelectionAsync(
                request.Selection,
                request.ExpectedSnapshot,
                CancellationToken.None,
                "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
                null,
                null,
                null,
                null,
                null))
            .ReturnsAsync(fallback);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(fallback);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
        context.Verify(item => item.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBodyForLambda.UseExpressionBodyForLambdaCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
    }
}

public sealed class ConvertForeachLinqToolTests
{
    [Fact]
    public async Task GIVEN_SelectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var target = new ConvertForeachLinqTool();
        var context = new MutationContextBuilder().Build();

        var result = await target.ExecuteAsync(new ConvertForeachLinqRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_ForeachToCallFormKind_WHEN_CallingExecuteAsync_THEN_ShouldStageCallFormReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new ConvertForeachLinqTool(),
            new ConvertForeachLinqRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                ConversionKind = ConvertForeachLinqKind.ForeachToCallForm,
            },
            stageRequest =>
            {
                stageRequest.ProviderId.Should().Be("Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider");
                stageRequest.Title.Should().Be("Convert to LINQ call form");
                stageRequest.EquivalenceKey.Should().Be("Convert_to_linq_call_form");
            });
    }

    [Fact]
    public async Task GIVEN_ForeachToQueryKind_WHEN_CallingExecuteAsync_THEN_ShouldStageQueryReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new ConvertForeachLinqTool(),
            new ConvertForeachLinqRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                ConversionKind = ConvertForeachLinqKind.ForeachToQuery,
            },
            stageRequest =>
            {
                stageRequest.ProviderId.Should().Be("Microsoft.CodeAnalysis.CSharp.ConvertLinq.ConvertForEachToLinqQuery.CSharpConvertForEachToLinqQueryProvider");
                stageRequest.Title.Should().Be("Convert to LINQ");
                stageRequest.EquivalenceKey.Should().Be("Convert_to_linq");
            });
    }

    [Fact]
    public async Task GIVEN_LinqToForeachKind_WHEN_CallingExecuteAsync_THEN_ShouldStageForeachReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new ConvertForeachLinqTool(),
            new ConvertForeachLinqRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                ConversionKind = ConvertForeachLinqKind.LinqToForeach,
            },
            stageRequest =>
            {
                stageRequest.ProviderId.Should().Be("Microsoft.CodeAnalysis.CSharp.ConvertLinq.CSharpConvertLinqQueryToForEachProvider");
                stageRequest.Title.Should().Be("Convert to foreach");
                stageRequest.EquivalenceKey.Should().Be("Convert_to_foreach");
            });
    }
}

public sealed class ConvertIfToSwitchToolTests
{
    [Fact]
    public async Task GIVEN_StatementKind_WHEN_CallingExecuteAsync_THEN_ShouldDelegateSwitchStatementReplaySelection()
    {
        await AdditionalMutationToolTestHelpers.AssertReplaySelectionAsync(
            new ConvertIfToSwitchTool(),
            new ConvertIfToSwitchRequest
            {
                Kind = ConvertIfToSwitchKind.Statement,
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
            },
            "Microsoft.CodeAnalysis.CSharp.ConvertIfToSwitch.CSharpConvertIfToSwitchCodeRefactoringProvider",
            title: "Convert to 'switch' statement");
    }

    [Fact]
    public async Task GIVEN_ExpressionKind_WHEN_CallingExecuteAsync_THEN_ShouldDelegateSwitchExpressionReplaySelection()
    {
        await AdditionalMutationToolTestHelpers.AssertReplaySelectionAsync(
            new ConvertIfToSwitchTool(),
            new ConvertIfToSwitchRequest
            {
                Kind = ConvertIfToSwitchKind.Expression,
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
            },
            "Microsoft.CodeAnalysis.CSharp.ConvertIfToSwitch.CSharpConvertIfToSwitchCodeRefactoringProvider",
            title: "Convert to 'switch' expression");
    }
}

public sealed class ExtractMethodToolTests
{
    [Fact]
    public async Task GIVEN_SelectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var target = new ExtractMethodTool();
        var context = new MutationContextBuilder().Build();

        var result = await target.ExecuteAsync(new ExtractMethodRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_LocalFunctionTargetKind_WHEN_CallingExecuteAsync_THEN_ShouldStageLocalFunctionReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new ExtractMethodTool(),
            new ExtractMethodRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                TargetKind = ExtractMethodTargetKind.LocalFunction,
            },
            stageRequest =>
            {
                stageRequest.ProviderId.Should().Be("Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider");
                stageRequest.Title.Should().Be("Extract local function");
                stageRequest.EquivalenceKey.Should().Be("Extract_local_function");
            });
    }

    [Fact]
    public async Task GIVEN_MethodTargetKind_WHEN_CallingExecuteAsync_THEN_ShouldStageMethodReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new ExtractMethodTool(),
            new ExtractMethodRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                TargetKind = ExtractMethodTargetKind.Method,
            },
            stageRequest =>
            {
                stageRequest.ProviderId.Should().Be("Microsoft.CodeAnalysis.CodeRefactorings.ExtractMethod.ExtractMethodCodeRefactoringProvider");
                stageRequest.Title.Should().Be("Extract method");
                stageRequest.EquivalenceKey.Should().Be("Extract_method");
            });
    }
}

public sealed class IntroduceParameterToolTests
{
    [Fact]
    public async Task GIVEN_SelectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var target = new IntroduceParameterTool();
        var context = new MutationContextBuilder().Build();

        var result = await target.ExecuteAsync(new IntroduceParameterRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_IntoNewOverloadWithAllOccurrences_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceParameterTool(),
            new IntroduceParameterRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                Strategy = IntroduceParameterStrategy.IntoNewOverload,
                AllOccurrences = true,
            },
            stageRequest =>
            {
                stageRequest.ProviderId.Should().Be("Microsoft.CodeAnalysis.CSharp.IntroduceParameter.CSharpIntroduceParameterCodeRefactoringProvider");
                stageRequest.Title.Should().Be("into new overload");
                stageRequest.EquivalenceKey.Should().Be("into new overload");
                stageRequest.ActionPath.Should().BeEquivalentTo([1, 2]);
            });
    }

    [Fact]
    public async Task GIVEN_UpdateCallSitesDirectlyWithSingleOccurrence_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceParameterTool(),
            new IntroduceParameterRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                Strategy = IntroduceParameterStrategy.UpdateCallSitesDirectly,
                AllOccurrences = false,
            },
            stageRequest =>
            {
                stageRequest.Title.Should().Be("and update call sites directly");
                stageRequest.EquivalenceKey.Should().Be("and update call sites directly");
                stageRequest.ActionPath.Should().BeEquivalentTo([0, 0]);
            });
    }

    [Fact]
    public async Task GIVEN_UpdateCallSitesDirectlyWithAllOccurrences_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceParameterTool(),
            new IntroduceParameterRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                Strategy = IntroduceParameterStrategy.UpdateCallSitesDirectly,
                AllOccurrences = true,
            },
            stageRequest =>
            {
                stageRequest.Title.Should().Be("and update call sites directly");
                stageRequest.EquivalenceKey.Should().Be("and update call sites directly");
                stageRequest.ActionPath.Should().BeEquivalentTo([1, 0]);
            });
    }

    [Fact]
    public async Task GIVEN_IntoExtractedMethodWithSingleOccurrence_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceParameterTool(),
            new IntroduceParameterRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                Strategy = IntroduceParameterStrategy.IntoExtractedMethod,
                AllOccurrences = false,
            },
            stageRequest =>
            {
                stageRequest.Title.Should().Be("into extracted method to invoke at call sites");
                stageRequest.EquivalenceKey.Should().Be("into extracted method to invoke at call sites");
                stageRequest.ActionPath.Should().BeEquivalentTo([0, 1]);
            });
    }

    [Fact]
    public async Task GIVEN_IntoExtractedMethodWithAllOccurrences_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceParameterTool(),
            new IntroduceParameterRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                Strategy = IntroduceParameterStrategy.IntoExtractedMethod,
                AllOccurrences = true,
            },
            stageRequest =>
            {
                stageRequest.Title.Should().Be("into extracted method to invoke at call sites");
                stageRequest.EquivalenceKey.Should().Be("into extracted method to invoke at call sites");
                stageRequest.ActionPath.Should().BeEquivalentTo([1, 1]);
            });
    }

    [Fact]
    public async Task GIVEN_IntoNewOverloadWithSingleOccurrence_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceParameterTool(),
            new IntroduceParameterRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                Strategy = IntroduceParameterStrategy.IntoNewOverload,
                AllOccurrences = false,
            },
            stageRequest =>
            {
                stageRequest.Title.Should().Be("into new overload");
                stageRequest.EquivalenceKey.Should().Be("into new overload");
                stageRequest.ActionPath.Should().BeEquivalentTo([0, 2]);
            });
    }
}

public sealed class IntroduceVariableToolTests
{
    [Fact]
    public async Task GIVEN_SelectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var target = new IntroduceVariableTool();
        var context = new MutationContextBuilder().Build();

        var result = await target.ExecuteAsync(new IntroduceVariableRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_FieldAllOccurrencesKind_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceVariableTool(),
            new IntroduceVariableRequest
            {
                Selection = new LocationSelector(),
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = 1,
                },
                Kind = IntroduceVariableKind.FieldAllOccurrences,
            },
            stageRequest =>
            {
                stageRequest.TitleStartsWith.Should().Be("Introduce field for all occurrences of ");
                stageRequest.TitleDoesNotContain.Should().BeNull();
            });
    }

    [Fact]
    public async Task GIVEN_LocalKind_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceVariableTool(),
            AdditionalMutationToolTestHelpers.CreateIntroduceVariableRequest(IntroduceVariableKind.Local),
            stageRequest =>
            {
                stageRequest.TitleStartsWith.Should().Be("Introduce local for ");
                stageRequest.TitleDoesNotContain.Should().Be("all occurrences");
            });
    }

    [Fact]
    public async Task GIVEN_LocalAllOccurrencesKind_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceVariableTool(),
            AdditionalMutationToolTestHelpers.CreateIntroduceVariableRequest(IntroduceVariableKind.LocalAllOccurrences),
            stageRequest =>
            {
                stageRequest.TitleStartsWith.Should().Be("Introduce local for all occurrences of ");
                stageRequest.TitleDoesNotContain.Should().BeNull();
            });
    }

    [Fact]
    public async Task GIVEN_LocalConstantKind_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceVariableTool(),
            AdditionalMutationToolTestHelpers.CreateIntroduceVariableRequest(IntroduceVariableKind.LocalConstant),
            stageRequest =>
            {
                stageRequest.TitleStartsWith.Should().Be("Introduce local constant for ");
                stageRequest.TitleDoesNotContain.Should().Be("all occurrences");
            });
    }

    [Fact]
    public async Task GIVEN_LocalConstantAllOccurrencesKind_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceVariableTool(),
            AdditionalMutationToolTestHelpers.CreateIntroduceVariableRequest(IntroduceVariableKind.LocalConstantAllOccurrences),
            stageRequest =>
            {
                stageRequest.TitleStartsWith.Should().Be("Introduce local constant for all occurrences of ");
                stageRequest.TitleDoesNotContain.Should().BeNull();
            });
    }

    [Fact]
    public async Task GIVEN_ConstantKind_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceVariableTool(),
            AdditionalMutationToolTestHelpers.CreateIntroduceVariableRequest(IntroduceVariableKind.Constant),
            stageRequest =>
            {
                stageRequest.TitleStartsWith.Should().Be("Introduce constant for ");
                stageRequest.TitleDoesNotContain.Should().Be("all occurrences");
            });
    }

    [Fact]
    public async Task GIVEN_ConstantAllOccurrencesKind_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceVariableTool(),
            AdditionalMutationToolTestHelpers.CreateIntroduceVariableRequest(IntroduceVariableKind.ConstantAllOccurrences),
            stageRequest =>
            {
                stageRequest.TitleStartsWith.Should().Be("Introduce constant for all occurrences of ");
                stageRequest.TitleDoesNotContain.Should().BeNull();
            });
    }

    [Fact]
    public async Task GIVEN_FieldKind_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceVariableTool(),
            AdditionalMutationToolTestHelpers.CreateIntroduceVariableRequest(IntroduceVariableKind.Field),
            stageRequest =>
            {
                stageRequest.TitleStartsWith.Should().Be("Introduce field for ");
                stageRequest.TitleDoesNotContain.Should().Be("all occurrences");
            });
    }

    [Fact]
    public async Task GIVEN_QueryVariableKind_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceVariableTool(),
            AdditionalMutationToolTestHelpers.CreateIntroduceVariableRequest(IntroduceVariableKind.QueryVariable),
            stageRequest =>
            {
                stageRequest.TitleStartsWith.Should().Be("Introduce query variable for ");
                stageRequest.TitleDoesNotContain.Should().Be("all occurrences");
            });
    }

    [Fact]
    public async Task GIVEN_QueryVariableAllOccurrencesKind_WHEN_CallingExecuteAsync_THEN_ShouldStageExpectedReplayAction()
    {
        await AdditionalMutationToolTestHelpers.AssertStageReplayRequestAsync(
            new IntroduceVariableTool(),
            AdditionalMutationToolTestHelpers.CreateIntroduceVariableRequest(IntroduceVariableKind.QueryVariableAllOccurrences),
            stageRequest =>
            {
                stageRequest.TitleStartsWith.Should().Be("Introduce query variable for all occurrences of ");
                stageRequest.TitleDoesNotContain.Should().BeNull();
            });
    }
}

internal static class AdditionalMutationToolTestHelpers
{
    public static IMutationContext CreateWorkspaceMutationContext(MiniWorkspace workspace)
    {
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        return new MutationContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
    }

    public static IntroduceVariableRequest CreateIntroduceVariableRequest(IntroduceVariableKind kind)
    {
        return new IntroduceVariableRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
            Kind = kind,
        };
    }

    public static async Task AssertReplaySelectionAsync<TRequest>(
        IMutationToolHandler<TRequest> target,
        TRequest request,
        string providerId,
        string? title = null,
        IReadOnlyList<int>? actionPath = null)
        where TRequest : WorkspaceBoundRequest
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var context = new Mock<ICodeActionMutationContext>();

        context
            .Setup(item => item.StageReplaySelectionAsync(
                It.IsAny<LocationSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<CancellationToken>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<IReadOnlyList<int>?>()))
            .ReturnsAsync(expected);

        var result = await target.ExecuteAsync(request, context.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        context.Verify(item => item.StageReplaySelectionAsync(
            It.IsAny<LocationSelector?>(),
            It.IsAny<SnapshotPrecondition?>(),
            CancellationToken.None,
            providerId,
            title,
            null,
            null,
            null,
            actionPath), Times.Once);
    }

    public static async Task AssertStageReplayRequestAsync<TRequest>(
        IMutationToolHandler<TRequest> target,
        TRequest request,
        Action<ReplayCodeActionRequest> assertRequest)
        where TRequest : WorkspaceBoundRequest
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var context = new MutationContextBuilder()
            .WithStageReplayCodeActionAsync((stageRequest, cancellationToken) =>
            {
                assertRequest(stageRequest);
                cancellationToken.Should().Be(CancellationToken.None);
                return ValueTask.FromResult(expected);
            })
            .Build();

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }
}
