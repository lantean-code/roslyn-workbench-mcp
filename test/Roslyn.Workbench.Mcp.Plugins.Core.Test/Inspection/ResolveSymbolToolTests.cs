using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class ResolveSymbolToolTests
{
    [Fact]
    public void GIVEN_DeclarationsLimitIsNullOrZero_WHEN_GettingEffectiveValue_THEN_ShouldUseDefaultOrZero()
    {
        var target = new ResolveSymbolRequest
        {
            Location = new LocationSelector(),
            DeclarationsLimit = null,
        };

        target.EffectiveDeclarationsLimit.Should().Be(32);

        target = target with { DeclarationsLimit = 0 };

        target.EffectiveDeclarationsLimit.Should().Be(0);
    }

    [Fact]
    public async Task GIVEN_SnapshotValidationHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new ResolveSymbolTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<ResolveSymbolData>(new PluginExecutionError
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
            Location = new LocationSelector(),
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveLocationDoesNotResolve_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationRejection()
    {
        var target = new ResolveSymbolTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var requestLocation = SelectorTestFactory.CreateSpanLocationSelector("Code.cs", 10, 6);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(requestLocation, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Ambiguous<Location>());

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
            .ReturnsAsync(SelectorResolveResult.Resolved(sourceLocation));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveSymbolAsync(
                It.Is<SymbolSelector>(selector => selector.Location == requestLocation),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.NotFound<ISymbol>());

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
            .ReturnsAsync(SelectorResolveResult.Resolved(usageLocation));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveSymbolAsync(
                It.Is<SymbolSelector>(selector => selector.Location == requestLocation),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved<ISymbol>(symbol));

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

        var sourceDocumentSelector = new DocumentSelector { Path = "Path" };
        var sourceRange = new TextSpanRange { Start = firstSourceLocation.SourceSpan.Start };
        var sourceSpanSelector = new TextSpanSelector
        {
            Document = sourceDocumentSelector,
            Range = sourceRange,
        };

        var sourceLocationSelector = new LocationSelector
        {
            Span = sourceSpanSelector,
        };

        var sourceSelector = new SymbolSelector
        {
            Location = sourceLocationSelector,
        };

        queryContextMocks.WorkspaceSelectorFactory
            .Setup(item => item.CreateSymbolSelector(It.IsAny<ResolvedLocation?>()))
            .Returns(sourceSelector);

        var result = await target.ExecuteAsync(new ResolveSymbolRequest
        {
            Location = requestLocation,
            DeclarationsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("Formatter");
        result.Data.Selector!.Location!.Span!.Range.Start.Should().Be(firstSourceLocation.SourceSpan.Start);
        result.Data.Declarations.Items.Select(item => item.Document!.Path).Should().Equal(expectedPaths!.Take(1));
        result.Data.Declarations.HasMore.Should().BeTrue();
        result.Data.Declarations.TotalCount.Should().BeNull();
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
            .ReturnsAsync(SelectorResolveResult.Resolved(usageLocation));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveSymbolAsync(
                It.Is<SymbolSelector>(selector => selector.Location == requestLocation),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.Is<Location>(item => item == usageLocation)))
            .Returns(resolvedUsageLocation);

        var fallbackDocumentSelector = new DocumentSelector { Path = "Path" };
        var fallbackRange = new TextSpanRange { Start = usageLocation.SourceSpan.Start };
        var fallbackSpanSelector = new TextSpanSelector
        {
            Document = fallbackDocumentSelector,
            Range = fallbackRange,
        };

        var fallbackLocationSelector = new LocationSelector
        {
            Span = fallbackSpanSelector,
        };

        var fallbackSelector = new SymbolSelector
        {
            Location = fallbackLocationSelector,
        };

        queryContextMocks.WorkspaceSelectorFactory
            .Setup(item => item.CreateSymbolSelector(resolvedUsageLocation))
            .Returns(fallbackSelector);

        var result = await target.ExecuteAsync(new ResolveSymbolRequest
        {
            Location = requestLocation,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("ToUpperInvariant");
        result.Data.Selector!.Location!.Span!.Range.Start.Should().Be(usageLocation.SourceSpan.Start);
        result.Data.Declarations.Items.Should().BeEmpty();
        result.Data.Declarations.HasMore.Should().BeFalse();
        result.Data.Declarations.TotalCount.Should().Be(0);
    }
}
