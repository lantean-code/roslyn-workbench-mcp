namespace Roslyn.Workbench.Mcp.CodeActions.Test;

[Trait("Category", "Audit")]
public sealed class DedicatedCodeActionToolsTests
{
    public static TheoryData<string> ReplayMutationCaseNames
    {
        get
        {
            var cases = new TheoryData<string>();

            foreach (var toolName in BuiltInCodeActionAuditCases.SupportedCompatibilityCases
                .Select(static auditCase => auditCase.ToolName)
                .Where(static toolName => !string.IsNullOrWhiteSpace(toolName))
                .Distinct(StringComparer.Ordinal))
            {
                if (ReplayMutationCases.ContainsKey(toolName!))
                {
                    cases.Add(toolName!);
                }
            }

            return cases;
        }
    }

    private static Dictionary<string, ReplayMutationCaseDefinition> ReplayMutationCases
    {
        get
        {
            return new Dictionary<string, ReplayMutationCaseDefinition>(StringComparer.Ordinal)
            {
                {
                    "add-constructor-parameters",
                    new(
                        static (fixture, open) => CreateAddConstructorParametersRequest(
                            fixture.GetSelectionInDocument(
                                "CandidateRefactorings.cs",
                                "private readonly int _count;\r\n    private readonly string _name;"),
                            open,
                            AddConstructorParametersKind.Required),
                        "CandidateRefactorings.cs")
                },
                { "add-anonymous-type-member-name", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "value + 1"), open), "CandidateCodeFixes.cs") },
                { "add-conditional-interpolation-parentheses", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "enabled ? \"enabled\" : \"disabled\""), open), "CandidateCodeFixes.cs") },
                { "add-explicit-cast", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "value;", 0), open), "CandidateCodeFixes.cs") },
                { "add-inheritdoc", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "DocumentedMember", 1), open), "CandidateCodeFixes.cs") },
                { "add-obsolete-attribute", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "CandidateObsoleteBase", 1), open), "CandidateCodeFixes.cs") },
                { "assign-out-parameters", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "return 'a';", 1), open), "CandidateCodeFixes.cs") },
                { "declare-as-nullable", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "null"), open), "CandidateCodeFixes.cs") },
                { "fix-incorrect-constraint", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "enum"), open), "CandidateCodeFixes.cs") },
                { "fix-return-type", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "return 1;"), open), "CandidateCodeFixes.cs") },
                { "remove-in-keyword", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "in value"), open), "CandidateCodeFixes.cs") },
                { "remove-new-modifier", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "internal new void RemoveNewModifier"), open), "CandidateCodeFixes.cs") },
                { "replace-default-literal", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "default"), open), "CandidateCodeFixes.cs") },
                { "use-explicit-type-for-const", new(static (fixture, open) => CreateCodeFixRequest(fixture.GetLocationInDocument("CandidateCodeFixes.cs", "const var"), open), "CandidateCodeFixes.cs") },
                { "convert-between-regular-and-verbatim-interpolated-string", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("$\"C:\\\\temp\\\\{value}\""), open), "Formatting.cs") },
                { "convert-between-regular-and-verbatim-string", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("\"C:\\\\temp\\\\logs\""), open), "Formatting.cs") },
                { "convert-foreach-to-for", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("foreach (var value in values)"), open), "Formatting.cs") },
                { "convert-for-to-foreach", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("for (var i = 0; i < values.Length; i++)", 0), open), "Formatting.cs") },
                { "convert-anonymous-type-to-tuple", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("new { Name = \"Alpha\", Count = 1 }"), open), "Formatting.cs") },
                { "convert-anonymous-type-to-class", new(static (fixture, open) => CreateAnonymousTypeToClassRequest(fixture.GetLocation("new { Name = \"Alpha\", Count = 1 }"), open, ConvertAnonymousTypeToClassKind.Class), "Formatting.cs") },
                { "convert-auto-property-to-full-property", new(static (fixture, open) => CreateAutoPropertyRequest(fixture.GetLocation("Goo"), open), "Formatting.cs") },
                { "convert-to-record", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("ConvertibleToRecord"), open), "Formatting.cs") },
                { "convert-direct-cast-to-try-cast", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("(object)1"), open), "Formatting.cs") },
                { "convert-expression-body", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("Square"), open), "Formatting.cs") },
                { "convert-local-function-to-method", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("Local", 0), open), "Formatting.cs") },
                { "convert-primary-to-regular-constructor", new(static (fixture, open) => CreateLocationRequest(fixture.GetSelection("PrimaryConstructorSamples(int value)"), open), "Formatting.cs") },
                { "convert-try-cast-to-direct-cast", new(static (fixture, open) => CreateLocationRequest(fixture.GetSelection("value as string"), open), "Formatting.cs") },
                { "invert-conditional", new(static (fixture, open) => CreateLocationRequest(fixture.GetSelection("count == 0 ? \"zero\" : \"non-zero\""), open), "Formatting.cs") },
                { "invert-if", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("if (left > 0)"), open), "Formatting.cs") },
                { "make-local-function-static", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("Local", 0), open), "Formatting.cs") },
                { "move-declaration-near-reference", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("int moved;"), open), "Formatting.cs") },
                { "name-tuple-element", new(static (fixture, open) => CreateLocationRequest(fixture.GetCursor("return (1 + 1, 2);", 0, "return (".Length), open), "Formatting.cs") },
                { "replace-doc-comment-text-with-tag", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("System.IDisposable"), open), "Formatting.cs") },
                { "reverse-for-statement", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("for (var i = 0; i < values.Length; i++)", 0), open), "Formatting.cs") },
                { "use-explicit-type", new(static (fixture, open) => CreateLocationRequest(fixture.GetCursor("var explicitBuilder", 0, "var".Length), open), "Formatting.cs") },
                { "use-implicit-type", new(static (fixture, open) => CreateLocationRequest(fixture.GetCursor("StringBuilder implicitBuilder", 0, "StringBuilder".Length), open), "Formatting.cs") },
                { "use-named-arguments", new(static (fixture, open) => CreateUseNamedArgumentsRequest(fixture.GetCursor("Sum(1, 2)", 0, 4), open, includeTrailingArguments: false), "Formatting.cs") },
                { "use-recursive-patterns", new(static (fixture, open) => CreateLocationRequest(fixture.GetCursor("cf != null && cf.C != 0", 0, "cf != null ".Length), open), "Formatting.cs") },
                { "add-await", new(static (fixture, open) => CreateAddAwaitRequest(fixture.GetCursorAfter("GetValueAsync()", 2), open, AddAwaitKind.Await), "Formatting.cs") },
                { "add-debugger-display", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("GreetingFormatter", 0), open), "Formatting.cs") },
                { "add-import", new(static (fixture, open) => CreateAddImportRequest(fixture.GetCursor("System.Net.Http.HttpClient"), open, simplifyAllOccurrences: false), "Formatting.cs") },
                { "add-null-checks", new(static (fixture, open) => CreateLocationRequest(fixture.GetCursorInDocument("AddParameterCheck.cs", "object value"), open), "AddParameterCheck.cs") },
                { "convert-if-to-switch", new(static (fixture, open) => CreateConvertIfToSwitchRequest(fixture.GetLocation("if (value == 0)"), open, ConvertIfToSwitchKind.Statement), "Formatting.cs") },
                {
                    "generate-comparison-operators",
                    new(
                        static (fixture, open) => CreateLocationRequest(
                            fixture.GetCursorInDocument(
                                "CandidateRefactorings.cs",
                                "ComparisonOperatorCandidate"),
                            open),
                        "CandidateRefactorings.cs")
                },
                {
                    "implement-interface",
                    new(
                        static (fixture, open) => CreateLocationRequest(
                            fixture.GetCursorInDocument(
                                "CandidateRefactorings.cs",
                                "InterfaceImplementationCandidate",
                                0,
                                "InterfaceImplementationCandidate : ICandidateFormatter\r\n{\r\n".Length),
                            open),
                        "CandidateRefactorings.cs")
                },
                { "invert-logical", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("&&"), open), "Formatting.cs") },
                { "introduce-using-statement", new(static (fixture, open) => CreateLocationRequest(fixture.GetSelection("var stream = new MemoryStream();"), open), "Formatting.cs") },
                {
                    "organize-imports",
                    new(
                        static (_, open) => CreateOrganizeImportsRequest(
                            "CandidateRefactorings.cs",
                            open),
                        "CandidateRefactorings.cs")
                },
                { "replace-conditional-with-statements", new(static (fixture, open) => CreateLocationRequest(fixture.GetLocation("value = enabled ? 1 : 2;"), open), "Formatting.cs") },
                {
                    "replace-method-with-property",
                    new(
                        static (fixture, open) => CreateReplaceMethodWithPropertyRequest(
                            fixture.GetCursorInDocument(
                                "CandidateRefactorings.cs",
                                "GetValue"),
                            open,
                            ReplaceMethodWithPropertyKind.GetterAndSetter),
                        "CandidateRefactorings.cs")
                },
                {
                    "replace-property-with-methods",
                    new(
                        static (fixture, open) => CreateLocationRequest(
                            fixture.GetCursorInDocument(
                                "CandidateRefactorings.cs",
                                "public int Value { get; set; }",
                                0,
                                "public int ".Length),
                            open),
                        "CandidateRefactorings.cs")
                },
            };
        }
    }

    [Theory]
    [MemberData(nameof(ReplayMutationCaseNames))]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingReplayWrapper_THEN_ShouldStageStructuredMutation(
        string toolName)
    {
        var testCase = ReplayMutationCases[toolName];
        var fixtureFactory = testCase.FixtureFactory ?? InspectionSampleFixture.Create;
        using var fixture = fixtureFactory();
        await using var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var result = await ExecuteAsync(coordinator, toolName, testCase.RequestFactory(fixture, openResult));
        var preview = await coordinator.PreviewTransactionAsync(TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Transaction!.Revision.Should().Be(1);
        result.Data.Operation.Should().Be(toolName);
        preview.Data!.Documents.Should().ContainSingle(change => change.Document!.Path == testCase.ExpectedDocumentPath);
        var documentPreview = preview.Data.Documents.Single(change => change.Document!.Path == testCase.ExpectedDocumentPath).Preview!;
        (documentPreview.AddedLines + documentPreview.RemovedLines + documentPreview.ChangedLines).Should().BeGreaterThan(0);
    }

    private sealed record ReplayMutationCaseDefinition(
        Func<InspectionSampleFixture, WorkspaceOperationResult<WorkspaceOpenOutcome>, WorkspaceBoundRequest> RequestFactory,
        string ExpectedDocumentPath,
        Func<InspectionSampleFixture>? FixtureFactory = null);

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingMoveTypeToFile_THEN_ShouldStageNewDocumentAndSourceUpdate()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var result = await ExecuteAsync(coordinator, "move-type-to-file", CreateMoveTypeToFileRequest(fixture.GetLocation("AutoPropertySamples"), openResult));
        var preview = await coordinator.PreviewTransactionAsync(TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Operation.Should().Be("move-type-to-file");
        preview.Data!.Documents.Should().Contain(change => change.Document!.Path == "Formatting.cs");
        preview.Data.Documents.Should().Contain(change => change.Document!.Path == "AutoPropertySamples.cs");
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingConvertPropertyToFull_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = InspectionSampleFixture.Create();
        await using var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var result = await ExecuteAsync(coordinator, "convert-property", CreateConvertPropertyRequest(fixture.GetLocation("Goo"), openResult, ConvertPropertyDirection.ToFull));
        var preview = await coordinator.PreviewTransactionAsync(TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Operation.Should().Be("convert-property");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
    }

    [Fact]
    public async Task GIVEN_ActiveTransactionAndBuiltInCodeActions_WHEN_ExecutingConvertPropertyToAutoWhenSafe_THEN_ShouldStageStructuredMutation()
    {
        using var fixture = InspectionSampleFixture.Create(InspectionSampleProfile.AutoProperties);
        await using var coordinator = CreateBuiltInCoordinator();
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var result = await ExecuteAsync(coordinator, "convert-property", CreateConvertPropertyRequest(fixture.GetLocation("Score"), openResult, ConvertPropertyDirection.ToAutoWhenSafe));
        var preview = await coordinator.PreviewTransactionAsync(TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded);
        result.Data!.Operation.Should().Be("convert-property");
        preview.Data!.Documents.Should().ContainSingle(static change => change.Document!.Path == "Formatting.cs");
    }

    private static LocationRefactoringRequest CreateLocationRequest(
        LocationSelector selection,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult)
    {
        return new LocationRefactoringRequest
        {
            Selection = selection,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static FixedCompilerCodeFixRequest CreateCodeFixRequest(
        LocationSelector location,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult)
    {
        return new FixedCompilerCodeFixRequest
        {
            Location = location,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static AddConstructorParametersRequest CreateAddConstructorParametersRequest(
        LocationSelector members,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult,
        AddConstructorParametersKind kind)
    {
        return new AddConstructorParametersRequest
        {
            Members = members,
            Kind = kind,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static OrganizeImportsRequest CreateOrganizeImportsRequest(
        string documentPath,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult)
    {
        return new OrganizeImportsRequest
        {
            Document = new DocumentSelector
            {
                Path = documentPath,
            },
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static ReplaceMethodWithPropertyRequest CreateReplaceMethodWithPropertyRequest(
        LocationSelector method,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult,
        ReplaceMethodWithPropertyKind kind)
    {
        return new ReplaceMethodWithPropertyRequest
        {
            Method = method,
            Kind = kind,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static ConvertAnonymousTypeToClassRequest CreateAnonymousTypeToClassRequest(
        LocationSelector selection,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult,
        ConvertAnonymousTypeToClassKind kind)
    {
        return new ConvertAnonymousTypeToClassRequest
        {
            Selection = selection,
            Kind = kind,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static ConvertAutoPropertyToFullPropertyRequest CreateAutoPropertyRequest(
        LocationSelector selection,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult)
    {
        return new ConvertAutoPropertyToFullPropertyRequest
        {
            Selection = selection,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static AddAwaitRequest CreateAddAwaitRequest(
        LocationSelector selection,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult,
        AddAwaitKind kind)
    {
        return new AddAwaitRequest
        {
            Selection = selection,
            Kind = kind,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static AddImportRequest CreateAddImportRequest(
        LocationSelector selection,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult,
        bool simplifyAllOccurrences)
    {
        return new AddImportRequest
        {
            Selection = selection,
            SimplifyAllOccurrences = simplifyAllOccurrences,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static ConvertIfToSwitchRequest CreateConvertIfToSwitchRequest(
        LocationSelector selection,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult,
        ConvertIfToSwitchKind kind)
    {
        return new ConvertIfToSwitchRequest
        {
            Selection = selection,
            Kind = kind,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static UseNamedArgumentsRequest CreateUseNamedArgumentsRequest(
        LocationSelector selection,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult,
        bool includeTrailingArguments)
    {
        return new UseNamedArgumentsRequest
        {
            Selection = selection,
            IncludeTrailingArguments = includeTrailingArguments,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static MoveTypeToFileRequest CreateMoveTypeToFileRequest(
        LocationSelector selection,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult)
    {
        return new MoveTypeToFileRequest
        {
            Type = new SymbolSelector
            {
                Location = selection,
            },
            PreserveNamespace = true,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static ConvertPropertyRequest CreateConvertPropertyRequest(
        LocationSelector selection,
        WorkspaceOperationResult<WorkspaceOpenOutcome> openResult,
        ConvertPropertyDirection direction)
    {
        return new ConvertPropertyRequest
        {
            Selection = selection,
            Direction = direction,
            ExpectedSnapshot = CreateSnapshot(openResult),
        };
    }

    private static SnapshotPrecondition CreateSnapshot(WorkspaceOperationResult<WorkspaceOpenOutcome> openResult)
    {
        return new SnapshotPrecondition
        {
            WorkspaceEpoch = openResult.Context.WorkspaceEpoch!.Value,
            TransactionRevision = 0,
        };
    }

    private static ComponentWorkspace CreateBuiltInCoordinator()
    {
        return BundledComponentWorkspaceFactory.CreateBuiltInCodeActionWorkspace();
    }

    private static async Task<CodeActionExecutionResult<MutationData>> ExecuteAsync(
        ComponentWorkspace coordinator,
        string toolName,
        WorkspaceBoundRequest request)
    {
        var session = new CodeActionComponentTestSession(coordinator);
        var cancellationToken = TestContext.Current.CancellationToken;
        var result = (toolName, request) switch
        {
            ("add-anonymous-type-member-name", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<AddAnonymousTypeMemberNameTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("add-constructor-parameters", AddConstructorParametersRequest typed) => await session.ExecuteMutationAsync<AddConstructorParametersTool, AddConstructorParametersRequest>(toolName, typed, cancellationToken),
            ("add-conditional-interpolation-parentheses", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<AddConditionalInterpolationParenthesesTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("add-explicit-cast", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<AddExplicitCastTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("add-inheritdoc", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<AddInheritdocTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("add-obsolete-attribute", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<AddObsoleteAttributeTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("assign-out-parameters", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<AssignOutParametersTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("declare-as-nullable", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<DeclareAsNullableTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("fix-incorrect-constraint", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<FixIncorrectConstraintTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("fix-return-type", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<FixReturnTypeTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("remove-in-keyword", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<RemoveInKeywordTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("remove-new-modifier", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<RemoveNewModifierTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("replace-default-literal", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<ReplaceDefaultLiteralTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("use-explicit-type-for-const", FixedCompilerCodeFixRequest typed) => await session.ExecuteMutationAsync<UseExplicitTypeForConstTool, FixedCompilerCodeFixRequest>(toolName, typed, cancellationToken),
            ("convert-between-regular-and-verbatim-interpolated-string", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ConvertBetweenRegularAndVerbatimInterpolatedStringTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("convert-between-regular-and-verbatim-string", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ConvertBetweenRegularAndVerbatimStringTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("convert-foreach-to-for", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ConvertForEachToForTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("convert-for-to-foreach", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ConvertForToForeachTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("convert-anonymous-type-to-tuple", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ConvertAnonymousTypeToTupleTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("convert-anonymous-type-to-class", ConvertAnonymousTypeToClassRequest typed) => await session.ExecuteMutationAsync<ConvertAnonymousTypeToClassTool, ConvertAnonymousTypeToClassRequest>(toolName, typed, cancellationToken),
            ("convert-auto-property-to-full-property", ConvertAutoPropertyToFullPropertyRequest typed) => await session.ExecuteMutationAsync<ConvertAutoPropertyToFullPropertyTool, ConvertAutoPropertyToFullPropertyRequest>(toolName, typed, cancellationToken),
            ("convert-to-record", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ConvertToRecordTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("convert-direct-cast-to-try-cast", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ConvertDirectCastToTryCastTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("convert-expression-body", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ConvertExpressionBodyTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("convert-local-function-to-method", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ConvertLocalFunctionToMethodTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("convert-primary-to-regular-constructor", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ConvertPrimaryToRegularConstructorTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("convert-try-cast-to-direct-cast", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ConvertTryCastToDirectCastTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("invert-conditional", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<InvertConditionalTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("invert-if", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<InvertIfTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("make-local-function-static", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<MakeLocalFunctionStaticTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("move-declaration-near-reference", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<MoveDeclarationNearReferenceTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("name-tuple-element", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<NameTupleElementTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("replace-doc-comment-text-with-tag", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ReplaceDocCommentTextWithTagTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("reverse-for-statement", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ReverseForStatementTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("use-explicit-type", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<UseExplicitTypeTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("use-implicit-type", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<UseImplicitTypeTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("use-named-arguments", UseNamedArgumentsRequest typed) => await session.ExecuteMutationAsync<UseNamedArgumentsTool, UseNamedArgumentsRequest>(toolName, typed, cancellationToken),
            ("use-recursive-patterns", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<UseRecursivePatternsTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("add-await", AddAwaitRequest typed) => await session.ExecuteMutationAsync<AddAwaitTool, AddAwaitRequest>(toolName, typed, cancellationToken),
            ("add-debugger-display", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<AddDebuggerDisplayTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("add-import", AddImportRequest typed) => await session.ExecuteMutationAsync<AddImportTool, AddImportRequest>(toolName, typed, cancellationToken),
            ("add-null-checks", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<AddNullChecksTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("convert-if-to-switch", ConvertIfToSwitchRequest typed) => await session.ExecuteMutationAsync<ConvertIfToSwitchTool, ConvertIfToSwitchRequest>(toolName, typed, cancellationToken),
            ("generate-comparison-operators", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<GenerateComparisonOperatorsTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("implement-interface", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ImplementInterfaceTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("invert-logical", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<InvertLogicalTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("introduce-using-statement", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<IntroduceUsingStatementTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("organize-imports", OrganizeImportsRequest typed) => await session.ExecuteMutationAsync<OrganizeImportsTool, OrganizeImportsRequest>(toolName, typed, cancellationToken),
            ("replace-conditional-with-statements", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ReplaceConditionalWithStatementsTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("replace-method-with-property", ReplaceMethodWithPropertyRequest typed) => await session.ExecuteMutationAsync<ReplaceMethodWithPropertyTool, ReplaceMethodWithPropertyRequest>(toolName, typed, cancellationToken),
            ("replace-property-with-methods", LocationRefactoringRequest typed) => await session.ExecuteMutationAsync<ReplacePropertyWithMethodsTool, LocationRefactoringRequest>(toolName, typed, cancellationToken),
            ("move-type-to-file", MoveTypeToFileRequest typed) => await session.ExecuteMutationAsync<MoveTypeToFileTool, MoveTypeToFileRequest>(toolName, typed, cancellationToken),
            ("convert-property", ConvertPropertyRequest typed) => await session.ExecuteMutationAsync<ConvertPropertyTool, ConvertPropertyRequest>(toolName, typed, cancellationToken),
            _ => throw new InvalidOperationException($"Replay case '{toolName}' does not have a typed component handler mapping."),
        };

        result.Outcome.Should().Be(CodeActionExecutionOutcome.Succeeded, result.Error?.Message);
        return result;
    }
}
