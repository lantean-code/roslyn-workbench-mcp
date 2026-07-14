using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetOperationTreeToolTests
{
    [Fact]
    public async Task GIVEN_ValidateSnapshotReturnsRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetOperationTreeTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<OperationTreeData>.Rejected(new PluginExecutionError
        {
            Code = "SnapshotConflict",
            Message = "SnapshotConflict",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<OperationTreeData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns(expected);

        var result = await target.ExecuteAsync(new GetOperationTreeRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_LocationSelectorIsMissing_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new GetOperationTreeTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<OperationTreeData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<OperationTreeData>?)null);

        var result = await target.ExecuteAsync(new GetOperationTreeRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_LocationSelectorDoesNotResolve_WHEN_CallingExecuteAsync_THEN_ShouldReturnStatusRejection()
    {
        var target = new GetOperationTreeTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<OperationTreeData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<OperationTreeData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Ambiguous());

        var result = await target.ExecuteAsync(new GetOperationTreeRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationAmbiguous");
    }

    [Fact]
    public async Task GIVEN_ResolvedLocationProjectionIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    Format("value");
                }

                string Format(string value)
                {
                    return value;
                }
            }
            """);

        var target = new GetOperationTreeTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (InvocationExpressionSyntax item) => item.ToString().Contains("Format(\"value\")", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<OperationTreeData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<OperationTreeData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns((ResolvedLocation?)null);

        var result = await target.ExecuteAsync(new GetOperationTreeRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_ResolvedLocationDoesNotContainDocumentPath_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    Format("value");
                }

                string Format(string value)
                {
                    return value;
                }
            }
            """);

        var target = new GetOperationTreeTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (InvocationExpressionSyntax item) => item.ToString().Contains("Format(\"value\")", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<OperationTreeData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<OperationTreeData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns(new ResolvedLocation
            {
                Document = new DocumentReference(),
            });

        var result = await target.ExecuteAsync(new GetOperationTreeRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_ResolvedLocationSourceTreeIsNotInCurrentSolution_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    Format("value");
                }

                string Format(string value)
                {
                    return value;
                }
            }
            """);

        var target = new GetOperationTreeTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (InvocationExpressionSyntax item) => item.ToString().Contains("Format(\"value\")", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<OperationTreeData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<OperationTreeData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetOperationTreeRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationNotFound");
    }

    [Fact]
    public async Task GIVEN_SelectedRegionDoesNotResolveToOperationTree_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        using var document = RoslynTestFactory.CreateDocument("class Formatter {}");

        var target = new GetOperationTreeTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (ClassDeclarationSyntax item) => item.Identifier.ValueText == "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<OperationTreeData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<OperationTreeData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetOperationTreeRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_SelectedRegionResolvesToLiteralOperation_WHEN_CallingExecuteAsync_THEN_ShouldReturnConstantValue()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Run()
                {
                    return 42;
                }
            }
            """);

        var target = new GetOperationTreeTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (LiteralExpressionSyntax item) => item.Token.ValueText == "42",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<OperationTreeData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<OperationTreeData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetOperationTreeRequest
        {
            Location = new LocationSelector(),
            MaxDepth = 4,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Root!.Kind.Should().Contain("Literal");
        result.Data.Root.ConstantValue.Should().Be("42");
        result.Data.Root.Truncated.Should().BeFalse();
        result.Data.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_SelectedNodeHasNoOperationButChildHasOperation_WHEN_CallingExecuteAsync_THEN_ShouldReturnChildOperationTree()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    Format("value");
                }

                string Format(string value)
                {
                    return value;
                }
            }
            """);

        var target = new GetOperationTreeTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (ArgumentListSyntax item) => item.ToString().Contains("\"value\"", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<OperationTreeData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<OperationTreeData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetOperationTreeRequest
        {
            Location = new LocationSelector(),
            MaxDepth = 8,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Root!.Kind.Should().Contain("Argument");
    }

    [Fact]
    public async Task GIVEN_SelectedRegionResolvesToOperationTreeAndMaxDepthIsZero_WHEN_CallingExecuteAsync_THEN_ShouldReturnTruncatedTree()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    Format("value");
                }

                string Format(string value)
                {
                    return value;
                }
            }
            """);

        var target = new GetOperationTreeTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var location = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(
            document.Document,
            static (InvocationExpressionSyntax item) => item.ToString().Contains("Format(\"value\")", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<OperationTreeData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<OperationTreeData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(location));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, document.Document.Name));

        var result = await target.ExecuteAsync(new GetOperationTreeRequest
        {
            Location = new LocationSelector(),
            MaxDepth = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Root!.Truncated.Should().BeTrue();
        result.Data.Root.Children.Should().BeEmpty();
        result.Data.Truncated.Should().BeTrue();
    }
}
