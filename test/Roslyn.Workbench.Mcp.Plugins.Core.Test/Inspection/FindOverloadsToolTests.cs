using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindOverloadsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        FindOverloadsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<FindOverloadsRequest, OverloadSearchData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "find-overloads"
                && metadata.Title == "Find Overloads"
                && metadata.Description == "Returns overload signatures for a resolved method or constructor."),
            It.IsAny<IQueryToolHandler<FindOverloadsRequest, OverloadSearchData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindOverloadsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<OverloadSearchData>.Rejected(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<OverloadSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, OverloadSearchData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindOverloadsRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolvedSymbolIsNotMethodOrConstructor_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
            }
            """);

        var target = new FindOverloadsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<OverloadSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, OverloadSearchData>
            {
                Value = symbol,
            });

        var result = await target.ExecuteAsync(new FindOverloadsRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "Find overloads requires a method or constructor symbol.",
        });
    }

    [Fact]
    public async Task GIVEN_ResolvedMethodSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedMethodOverloads()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Format()
                {
                    return 0;
                }

                int Format(string value)
                {
                    return value.Length;
                }

                int Format(int value, string text)
                {
                    return value + text.Length;
                }
            }
            """);

        var target = new FindOverloadsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await GetMethodSymbolAsync(document.Document, "Format", 1);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<OverloadSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, OverloadSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindOverloadsRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("Format");
        result.Data.Overloads.Items.Select(item => item.DisplayName).Should().Equal("Formatter.Format()", "Formatter.Format(string)", "Formatter.Format(int, string)");
        result.Data.Overloads.Items.All(item => item.ReturnType is not null).Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_ResolvedConstructorSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnConstructorOverloads()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                public Formatter()
                {
                }

                public Formatter(string value)
                {
                }
            }
            """);

        var target = new FindOverloadsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await GetConstructorSymbolAsync(document.Document, 0);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<OverloadSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, OverloadSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindOverloadsRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Overloads.Items.Select(item => item.DisplayName).Should().Equal("Formatter.Formatter()", "Formatter.Formatter(string)");
        result.Data.Overloads.Items.All(item => item.ReturnType is null).Should().BeTrue();
        result.Data.Overloads.Items.All(item => item.Kind == MethodKind.Constructor.ToString()).Should().BeTrue();
    }

    private static async Task<IMethodSymbol> GetMethodSymbolAsync(Document document, string methodName, int parameterCount)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(TestContext.Current.CancellationToken);
        var method = syntaxRoot!.DescendantNodes()
            .OfType<MethodDeclarationSyntax>()
            .Single(item => item.Identifier.ValueText == methodName && item.ParameterList.Parameters.Count == parameterCount);

        return (IMethodSymbol)(semanticModel!.GetDeclaredSymbol(method, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException($"The method '{methodName}' could not be resolved."));
    }

    private static async Task<IMethodSymbol> GetConstructorSymbolAsync(Document document, int parameterCount)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(TestContext.Current.CancellationToken);
        var constructor = syntaxRoot!.DescendantNodes()
            .OfType<ConstructorDeclarationSyntax>()
            .Single(item => item.ParameterList.Parameters.Count == parameterCount);

        return (IMethodSymbol)(semanticModel!.GetDeclaredSymbol(constructor, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The constructor could not be resolved."));
    }
}
