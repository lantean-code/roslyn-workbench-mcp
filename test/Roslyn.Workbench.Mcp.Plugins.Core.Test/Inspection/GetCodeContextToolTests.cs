using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetCodeContextToolTests
{
    [Fact]
    public void GIVEN_ContextLineLimitsAreNull_WHEN_GettingEffectiveValues_THEN_ShouldUseDeclaredDefaults()
    {
        var target = new GetCodeContextRequest
        {
            Location = new LocationSelector(),
            BeforeLines = null,
            AfterLines = null,
            EnclosingSymbolsLimit = null,
            DiagnosticsLimit = null,
        };

        target.EffectiveBeforeLines.Should().Be(10);
        target.EffectiveAfterLines.Should().Be(10);
        target.EffectiveEnclosingSymbolsLimit.Should().Be(16);
        target.EffectiveDiagnosticsLimit.Should().Be(50);

        target = target with
        {
            EnclosingSymbolsLimit = 0,
            DiagnosticsLimit = 0,
        };

        target.EffectiveEnclosingSymbolsLimit.Should().Be(0);
        target.EffectiveDiagnosticsLimit.Should().Be(0);
    }

    [Fact]
    public async Task GIVEN_ValidateSnapshotReturnsConflict_WHEN_CallingExecuteAsync_THEN_ShouldReturnConflictResult()
    {
        var target = new GetCodeContextTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Conflict<CodeContextData>(new PluginExecutionError
        {
            Code = "SnapshotMismatch",
            Message = "SnapshotMismatch",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CodeContextData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns(expected);

        var result = await target.ExecuteAsync(new GetCodeContextRequest
        {
            Location = new LocationSelector(),
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                Guid.Parse("11111111-1111-1111-1111-111111111111")),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveLocationReturnsNotFound_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        var target = new GetCodeContextTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CodeContextData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<CodeContextData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.NotFound<Location>());

        var result = await target.ExecuteAsync(new GetCodeContextRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_ResolvedLocationDoesNotProduceSourceDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                }
            }
            """);

        using var emptyWorkspace = new AdhocWorkspace();

        var target = new GetCodeContextTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = document.GetSingleNodeLocation<MethodDeclarationSyntax>(item => item.Identifier.ValueText == "Run");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(emptyWorkspace.CurrentSolution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CodeContextData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<CodeContextData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns(SelectorTestFactory.CreateResolvedLocation("Code.cs", 0, 1));

        var result = await target.ExecuteAsync(new GetCodeContextRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_ResolvedLocationDoesNotProvideResolvedPath_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                }
            }
            """);

        var target = new GetCodeContextTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = document.GetSingleNodeLocation<MethodDeclarationSyntax>(item => item.Identifier.ValueText == "Run");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CodeContextData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<CodeContextData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns(new ResolvedLocation
            {
                Snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                    Guid.Parse("11111111-1111-1111-1111-111111111111")),
            });

        var result = await target.ExecuteAsync(new GetCodeContextRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_IncludeEnclosingSymbolsAndDiagnosticsAreTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnDistinctDiagnosticsAndEnclosingSymbols()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                string Run(string value)
                {
                    var unused = 42;
                    return value.Trim();
                }
            }
            """);

        var target = new GetCodeContextTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (LocalDeclarationStatementSyntax item) => item.ToString().Contains("unused", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CodeContextData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<CodeContextData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => new SymbolReference
            {
                DisplayName = item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                Kind = item.Kind.ToString(),
                DocumentationCommentId = item.GetDocumentationCommentId(),
            });

        var result = await target.ExecuteAsync(new GetCodeContextRequest
        {
            Location = new LocationSelector(),
            IncludeDiagnostics = true,
            IncludeEnclosingSymbols = true,
            BeforeLines = 1,
            AfterLines = 1,
            EnclosingSymbolsLimit = 1,
            DiagnosticsLimit = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Text.Should().Contain("var unused = 42;");
        result.Data.EnclosingSymbols.Items.Select(item => item.DisplayName).Should().Equal("Formatter.Run(string)");
        result.Data.EnclosingSymbols.HasMore.Should().BeTrue();
        result.Data.EnclosingSymbols.TotalCount.Should().Be(4);
        result.Data.Diagnostics.Items.Should().BeEmpty();
        result.Data.Diagnostics.HasMore.Should().BeTrue();
        result.Data.Diagnostics.TotalCount.Should().Be(1);

        var projectedDiagnosticsResult = await target.ExecuteAsync(new GetCodeContextRequest
        {
            Location = new LocationSelector(),
            IncludeDiagnostics = true,
            DiagnosticsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        projectedDiagnosticsResult.Data!.Diagnostics.Items.Select(item => item.Id).Should().Equal("CS0219");
        projectedDiagnosticsResult.Data.Diagnostics.HasMore.Should().BeFalse();
        projectedDiagnosticsResult.Data.Diagnostics.TotalCount.Should().Be(1);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(100)]
    [InlineData(int.MaxValue)]
    public async Task GIVEN_ContextWindowExtendsBeyondDocument_WHEN_CallingExecuteAsync_THEN_ShouldClampWithoutOverflow(int contextLines)
    {
        const string source = """
            class Formatter
            {
                void Run()
                {
                    int value = 0;
                }
            }
            """;

        using var document = RoslynTestFactory.CreateDocument(source);
        var target = new GetCodeContextTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var sourceText = await document.Document.GetTextAsync(TestContext.Current.CancellationToken);
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (LocalDeclarationStatementSyntax item) => item.ToString().Contains("value", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CodeContextData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<CodeContextData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));

        var result = await target.ExecuteAsync(new GetCodeContextRequest
        {
            Location = new LocationSelector(),
            BeforeLines = contextLines,
            AfterLines = contextLines,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Text.Split(Environment.NewLine).Should().HaveCount(sourceText.Lines.Count);
        result.Data.Text.Should().Contain("class Formatter");
        result.Data.Text.Should().Contain("int value = 0;");
    }

    [Fact]
    public async Task GIVEN_EmptyDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyText()
    {
        using var document = RoslynTestFactory.CreateDocument(string.Empty);
        var target = new GetCodeContextTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var root = await document.Document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var location = root!.GetLocation();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CodeContextData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<CodeContextData>?)null);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved(location));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));

        var result = await target.ExecuteAsync(new GetCodeContextRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Text.Should().BeEmpty();
    }
}
