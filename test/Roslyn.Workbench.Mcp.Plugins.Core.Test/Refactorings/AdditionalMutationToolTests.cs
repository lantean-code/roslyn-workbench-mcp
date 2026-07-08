namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

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
        var replayExecutor = new Mock<IReplayCodeActionExecutor>();
        var services = new ToolExecutionServicesBuilder()
            .WithReplayCodeActionExecutor(replayExecutor.Object)
            .Build();
        var context = new MutationContextBuilder()
            .WithToolExecutionServices(services)
            .Build();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertExpressionBodyTool();

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

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        replayExecutor.Verify(executor => executor.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
            CancellationToken.None,
            "Microsoft.CodeAnalysis.CSharp.UseExpressionBody.UseExpressionBodyCodeRefactoringProvider",
            null,
            null,
            null,
            null,
            null), Times.Once);
        replayExecutor.Verify(executor => executor.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
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
        var replayExecutor = new Mock<IReplayCodeActionExecutor>();
        var services = new ToolExecutionServicesBuilder()
            .WithReplayCodeActionExecutor(replayExecutor.Object)
            .Build();
        var context = new MutationContextBuilder()
            .WithToolExecutionServices(services)
            .Build();
        var request = new LocationRefactoringRequest
        {
            Selection = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        };
        var target = new ConvertExpressionBodyTool();

        replayExecutor
            .SetupSequence(executor => executor.StageReplaySelectionAsync(
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
            .ReturnsAsync(PluginExecutionResult<MutationProposal>.Rejected(new ToolError
            {
                Code = "CodeActionUnavailable",
                Message = "CodeActionUnavailable",
            }))
            .ReturnsAsync(fallback);

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(fallback);
        replayExecutor.Verify(executor => executor.StageReplaySelectionAsync(
            request.Selection,
            request.ExpectedSnapshot,
            context,
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

public sealed class FormatDocumentToolTests
{
    [Fact]
    public async Task GIVEN_RequestResolverRejectsDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnResolverRejection()
    {
        var rejection = PluginExecutionResult<MutationProposal>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        requestResolver
            .Setup(item => item.ResolveDocument<MutationProposal>(It.IsAny<DocumentSelector?>(), It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<Document, MutationProposal>
            {
                Rejection = rejection,
            });
        var target = new FormatDocumentTool();
        var context = new MutationContextBuilder()
            .WithToolExecutionServices(new ToolExecutionServicesBuilder().WithRequestResolver(requestResolver.Object).Build())
            .Build();

        var result = await target.ExecuteAsync(new FormatDocumentRequest
        {
            Document = new DocumentSelector
            {
                Path = "Sample.cs",
            },
        }, context, CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_RequestResolverRejectsSnapshot_WHEN_CallingExecuteAsync_THEN_ShouldReturnSnapshotConflict()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var document = workspace.Solution.Projects.Single().Documents.Single();
        var rejection = PluginExecutionResult<MutationProposal>.Conflict(new ToolError
        {
            Code = "SnapshotMismatch",
            Message = "SnapshotMismatch",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        requestResolver
            .Setup(item => item.ResolveDocument<MutationProposal>(It.IsAny<DocumentSelector?>(), It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<Document, MutationProposal>
            {
                Value = document,
            });
        requestResolver
            .Setup(item => item.ValidateSnapshot<MutationProposal>(It.IsAny<IToolExecutionContext>(), It.IsAny<SnapshotPrecondition?>()))
            .Returns(rejection);
        var target = new FormatDocumentTool();
        var context = new MutationContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithToolExecutionServices(new ToolExecutionServicesBuilder().WithRequestResolver(requestResolver.Object).Build())
            .Build();

        var result = await target.ExecuteAsync(new FormatDocumentRequest
        {
            Document = new DocumentSelector
            {
                Path = "Sample.cs",
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, context, CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_CallingExecuteAsync_THEN_ShouldThrowOperationCanceledException()
    {
        var target = new FormatDocumentTool();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var act = () => target.ExecuteAsync(new FormatDocumentRequest(), new MutationContextBuilder().Build(), cancellationTokenSource.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_FormatProducesNoChanges_WHEN_CallingExecuteAsync_THEN_ShouldReturnNoChange()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public int Value { get; }
            }
            """);
        var target = new FormatDocumentTool();
        var context = AdditionalMutationToolTestHelpers.CreateWorkspaceMutationContext(workspace);

        var result = await target.ExecuteAsync(new FormatDocumentRequest
        {
            Document = new DocumentSelector
            {
                Path = "Sample.cs",
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.NoChange);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_RangeIsSpecifiedAndFormattingChangesDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationProposal()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Execute()
                {
                    var value=1;
                }
            }
            """);
        var target = new FormatDocumentTool();
        var context = AdditionalMutationToolTestHelpers.CreateWorkspaceMutationContext(workspace);

        var result = await target.ExecuteAsync(new FormatDocumentRequest
        {
            Document = new DocumentSelector
            {
                Path = "Sample.cs",
            },
            Range = new TextSpanSelector
            {
                Document = new DocumentSelector
                {
                    Path = "Sample.cs",
                },
                Start = 0,
                Length = 200,
            },
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        var proposal = result.Data;
        proposal.Should().NotBeNull();
        proposal!.Summary.Should().Be("Format 'Sample.cs'.");
        var candidateSolution = proposal.CandidateSolution;
        candidateSolution.Should().NotBeNull();
        var formattedText = await candidateSolution!.Projects.Single().Documents.Single().GetTextAsync(TestContext.Current.CancellationToken);
        formattedText.ToString().Should().Contain("var value = 1;");
    }
}

public sealed class RenameSymbolToolTests
{
    [Fact]
    public async Task GIVEN_SymbolResolutionHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnResolverRejection()
    {
        var rejection = PluginExecutionResult<MutationProposal>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ToolResolutionResult<ISymbol, MutationProposal>>(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Rejection = rejection,
            }));
        var target = new RenameSymbolTool();
        var context = new MutationContextBuilder()
            .WithToolExecutionServices(new ToolExecutionServicesBuilder().WithRequestResolver(requestResolver.Object).Build())
            .Build();

        var result = await target.ExecuteAsync(new RenameSymbolRequest
        {
            Symbol = new SymbolSelector(),
            NewName = "NewName",
        }, context, CancellationToken.None);

        result.Should().BeEquivalentTo(rejection);
    }

    [Fact]
    public async Task GIVEN_NewNameIsWhitespace_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var requestResolver = new Mock<IToolRequestResolver>();
        requestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IToolExecutionContext>(),
                It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<ToolResolutionResult<ISymbol, MutationProposal>>(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Value = Mock.Of<ISymbol>(),
            }));
        var target = new RenameSymbolTool();
        var context = new MutationContextBuilder()
            .WithToolExecutionServices(new ToolExecutionServicesBuilder().WithRequestResolver(requestResolver.Object).Build())
            .Build();

        var result = await target.ExecuteAsync(new RenameSymbolRequest
        {
            Symbol = new SymbolSelector(),
            NewName = " ",
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_CancellationIsRequested_WHEN_CallingExecuteAsync_THEN_ShouldThrowOperationCanceledException()
    {
        var target = new RenameSymbolTool();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();

        var act = () => target.ExecuteAsync(new RenameSymbolRequest(), new MutationContextBuilder().Build(), cancellationTokenSource.Token).AsTask();

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_NewNameMatchesExistingName_WHEN_CallingExecuteAsync_THEN_ShouldReturnSucceededResult()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class ExistingName
            {
            }
            """);
        var target = new RenameSymbolTool();
        var context = AdditionalMutationToolTestHelpers.CreateWorkspaceMutationContext(workspace);

        var result = await target.ExecuteAsync(new RenameSymbolRequest
        {
            Symbol = new SymbolSelector
            {
                Location = workspace.GetLocationSelector("ExistingName"),
            },
            NewName = "ExistingName",
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        var proposal = result.Data;
        proposal.Should().NotBeNull();
        proposal!.Summary.Should().Be("Rename 'ExistingName' to 'ExistingName'.");
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GIVEN_RenameChangesSolution_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationProposal()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class ExistingName
            {
            }
            """);
        var target = new RenameSymbolTool();
        var context = AdditionalMutationToolTestHelpers.CreateWorkspaceMutationContext(workspace);

        var result = await target.ExecuteAsync(new RenameSymbolRequest
        {
            Symbol = new SymbolSelector
            {
                Location = workspace.GetLocationSelector("ExistingName"),
            },
            NewName = "UpdatedName",
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        var proposal = result.Data;
        proposal.Should().NotBeNull();
        proposal!.Summary.Should().Be("Rename 'ExistingName' to 'UpdatedName'.");
        var candidateSolution = proposal.CandidateSolution;
        candidateSolution.Should().NotBeNull();
        var updatedText = await candidateSolution!.Projects.Single().Documents.Single().GetTextAsync(TestContext.Current.CancellationToken);
        updatedText.ToString().Should().Contain("UpdatedName");
    }
}

public sealed class RemoveUnusedUsingsToolTests
{
    [Fact]
    public async Task GIVEN_RequestScope_WHEN_CallingExecuteAsync_THEN_ShouldStageScopedCodeFix()
    {
        var expected = PluginExecutionResult<MutationProposal>.Success(new MutationProposal());
        var request = new RemoveUnusedUsingsRequest
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
                stageRequest.DiagnosticIds.Should().BeEquivalentTo(["RemoveUnnecessaryImportsFixable"]);
                stageRequest.Title.Should().Be("Remove unnecessary usings");
                stageRequest.SyntheticDiagnosticId.Should().Be("RemoveUnnecessaryImportsFixable");
                cancellationToken.Should().Be(CancellationToken.None);
                return ValueTask.FromResult(expected);
            })
            .Build();
        var target = new RemoveUnusedUsingsTool();

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
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
        var replayExecutor = new Mock<IReplayCodeActionExecutor>();
        var services = new ToolExecutionServicesBuilder()
            .WithReplayCodeActionExecutor(replayExecutor.Object)
            .Build();
        var context = new MutationContextBuilder()
            .WithToolExecutionServices(services)
            .Build();

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

        var result = await target.ExecuteAsync(request, context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        replayExecutor.Verify(executor => executor.StageReplaySelectionAsync(
            It.IsAny<LocationSelector?>(),
            It.IsAny<SnapshotPrecondition?>(),
            context,
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
