using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class ResolveSymbolToolTests
{
    [Fact]
    public async Task GIVEN_SnapshotValidationHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new ResolveSymbolTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<ResolveSymbolData>.Rejected(new PluginExecutionError
        {
            Code = "SnapshotConflict",
            Message = "SnapshotConflict",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<ResolveSymbolData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns(expected);

        var result = await target.ExecuteAsync(new ResolveSymbolRequest
        {
            ExpectedSnapshot = new SnapshotPrecondition(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_LocationIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var target = new ResolveSymbolTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        var result = await target.ExecuteAsync(new ResolveSymbolRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        result.Error.Message.Should().Be("Resolve symbol requires location.");
    }

    [Fact]
    public async Task GIVEN_ResolveLocationDoesNotResolve_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationRejection()
    {
        var target = new ResolveSymbolTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var requestLocation = SelectorTestFactory.CreateSpanLocationSelector("Code.cs", 10, 6);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(requestLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Ambiguous());

        var result = await target.ExecuteAsync(new ResolveSymbolRequest
        {
            Location = requestLocation,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("LocationAmbiguous");
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolDoesNotResolve_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolRejection()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public static class Formatter
            {
                public static string Format(string value)
                {
                    return value;
                }
            }
            """);

        var target = new ResolveSymbolTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var sourceLocation = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync<MethodDeclarationSyntax>(
            document.Document,
            static item => item.Identifier.ValueText == "Format",
            TestContext.Current.CancellationToken);
        var requestLocation = SelectorTestFactory.CreateSpanLocationSelector("Code.cs", sourceLocation.SourceSpan.Start, sourceLocation.SourceSpan.Length);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(requestLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(sourceLocation));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveSymbolAsync(
                It.Is<SymbolSelector>(selector => selector.Location == requestLocation),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<ISymbol>.NotFound());

        var result = await target.ExecuteAsync(new ResolveSymbolRequest
        {
            Location = requestLocation,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("SymbolNotFound");
    }

    [Fact]
    public async Task GIVEN_SourceSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnSourceSelectorAndOrderedDeclarations()
    {
        using var solution = RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "C.cs",
                        Source = """
                            namespace Sample;

                            public partial class Formatter
                            {
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "A.cs",
                        Source = """
                            namespace Sample;

                            public partial class Formatter
                            {
                            }

                            public static class FormatterUsage
                            {
                                public static Formatter Create()
                                {
                                    return new Formatter();
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "B.cs",
                        Source = """
                            namespace Sample;

                            public partial class Formatter
                            {
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new ResolveSymbolTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("A.cs"),
            "Formatter",
            TestContext.Current.CancellationToken);
        var usageLocation = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync<IdentifierNameSyntax>(
            solution.GetDocument("A.cs"),
            static item => item.Identifier.ValueText == "Formatter" && item.Parent is ObjectCreationExpressionSyntax,
            TestContext.Current.CancellationToken);
        var requestLocation = SelectorTestFactory.CreateSpanLocationSelector("A.cs", usageLocation.SourceSpan.Start, usageLocation.SourceSpan.Length);
        var firstSourceLocation = symbol.Locations.First(static item => item.IsInSource);
        var firstSourcePath = Path.GetFileName(firstSourceLocation.SourceTree!.FilePath!);
        var skippedPath = symbol.Locations
            .Where(static item => item.IsInSource)
            .Select(item => Path.GetFileName(item.SourceTree!.FilePath!))
            .First(item => item != null && item != firstSourcePath);
        var expectedPaths = symbol.Locations
            .Where(static item => item.IsInSource)
            .Select(item => Path.GetFileName(item.SourceTree!.FilePath!))
            .Where(item => item != skippedPath)
            .OrderBy(static item => item, StringComparer.Ordinal)
            .ToArray();

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(requestLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(usageLocation));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveSymbolAsync(
                It.Is<SymbolSelector>(selector => selector.Location == requestLocation),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<ISymbol>.Resolved(symbol));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item =>
            {
                var path = Path.GetFileName(item.SourceTree!.FilePath!);
                if (path == skippedPath && path != firstSourcePath)
                {
                    return null;
                }

                return SelectorTestFactory.CreateResolvedLocation(
                    path!,
                    item.SourceSpan.Start,
                    item.SourceSpan.Length);
            });

        var result = await target.ExecuteAsync(new ResolveSymbolRequest
        {
            Location = requestLocation,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("Formatter");
        result.Data.Selector!.Location!.Span!.Start.Should().Be(firstSourceLocation.SourceSpan.Start);
        result.Data.Declarations.Select(item => item.Document!.Path).Should().Equal(expectedPaths!);
    }

    [Fact]
    public async Task GIVEN_MetadataSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnFallbackLocationSelectorAndNoDeclarations()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public static class Formatter
            {
                public static string Format(string value)
                {
                    return value.ToUpperInvariant();
                }
            }
            """);

        var target = new ResolveSymbolTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredInvocationTargetSymbolAsync(
            document.Document,
            static item => item.ToString().Contains("ToUpperInvariant()", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);
        var usageLocation = await RoslynDocumentTestHelper.GetSingleNodeLocationAsync<InvocationExpressionSyntax>(
            document.Document,
            static item => item.ToString().Contains("ToUpperInvariant()", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);
        var requestLocation = SelectorTestFactory.CreateSpanLocationSelector("Code.cs", usageLocation.SourceSpan.Start, usageLocation.SourceSpan.Length);
        var resolvedUsageLocation = SelectorTestFactory.CreateResolvedLocation("Code.cs", usageLocation.SourceSpan.Start, usageLocation.SourceSpan.Length);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(requestLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(usageLocation));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveSymbolAsync(
                It.Is<SymbolSelector>(selector => selector.Location == requestLocation),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<ISymbol>.Resolved(symbol));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(item => item == usageLocation)))
            .Returns(resolvedUsageLocation);

        var result = await target.ExecuteAsync(new ResolveSymbolRequest
        {
            Location = requestLocation,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("ToUpperInvariant");
        result.Data.Selector!.Location!.Span!.Start.Should().Be(usageLocation.SourceSpan.Start);
        result.Data.Declarations.Should().BeEmpty();
    }
}
