using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetCodeContextToolTests
{
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
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                WorkspaceEpoch = 1,
            },
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
            .Returns(new ResolvedLocation());

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
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Text.Should().Contain("var unused = 42;");
        result.Data.EnclosingSymbols.Select(item => item.DisplayName).Should().Contain("Formatter.Run(string)");
        result.Data.EnclosingSymbols.Select(item => item.DisplayName).Should().Contain("Formatter");
        result.Data.Diagnostics.Select(item => item.Id).Should().Contain("CS0219");
    }
}
