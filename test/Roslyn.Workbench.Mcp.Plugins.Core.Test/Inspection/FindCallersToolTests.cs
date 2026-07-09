using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindCallersToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        FindCallersTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<FindCallersRequest, CallerSearchData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "find-callers"
                && metadata.Title == "Find Callers"
                && metadata.Description == "Returns direct source call sites and containing symbols."),
            It.IsAny<IQueryToolHandler<FindCallersRequest, CallerSearchData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_SymbolResolutionHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindCallersTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<CallerSearchData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CallerSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CallerSearchData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindCallersRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        queryContextMocks.RequestResolver.Verify(item => item.ResolveDocuments<CallerSearchData>(It.IsAny<ScopeSelector?>(), It.IsAny<IToolExecutionContext>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
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
                        Name = "Code.cs",
                        Source = """
                            class Target
                            {
                                void Callee()
                                {
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new FindCallersTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await GetMethodSymbolAsync(solution.GetDocument("Code.cs"), "Callee");
        var expected = PluginExecutionResult<CallerSearchData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CallerSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CallerSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<CallerSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, CallerSearchData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindCallersRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_IncludeContextIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedCallersAndFilteredLocations()
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
                        Name = "Target.cs",
                        Source = """
                            class Target
                            {
                                public void Callee()
                                {
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Callers.cs",
                        Source = """
                            class AlphaCaller
                            {
                                public void RunAlpha()
                                {
                                    var target = new Target();
                                    target.Callee();
                                }
                            }

                            class BetaCaller
                            {
                                public void RunBeta()
                                {
                                    var target = new Target();
                                    target.Callee();
                                    target.Callee();
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new FindCallersTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await GetMethodSymbolAsync(solution.GetDocument("Target.cs"), "Callee");
        var callerDocument = solution.GetDocument("Callers.cs");
        var betaCalleeLocations = await GetCalleeIdentifierLocationsAsync(callerDocument, "RunBeta");
        var omittedSpanStart = betaCalleeLocations[0].SourceSpan.Start;

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CallerSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CallerSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<CallerSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, CallerSearchData>
            {
                Value = solution.Solution.Projects.Single().Documents.ToArray(),
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(resolved => SelectorTestFactory.CreateSymbolReference(resolved));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(location =>
            {
                if (location.SourceSpan.Start == omittedSpanStart)
                {
                    return null;
                }

                return SelectorTestFactory.CreateResolvedLocation(location, Path.GetFileName(location.SourceTree!.FilePath!));
            });

        var result = await target.ExecuteAsync(new FindCallersRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("Callee");
        result.Data.Callers.Items.Select(item => item.Caller!.DisplayName).Should().Equal("RunAlpha", "RunBeta");
        result.Data.Callers.Items[0].Contexts.Should().BeEmpty();
        result.Data.Callers.Items[1].Locations.Should().ContainSingle();
        result.Data.Callers.Items[1].Locations[0].Document!.Path.Should().Be("Callers.cs");
        queryContextMocks.ToolExecutionServices.VerifyGet(item => item.InspectionContextService, Times.Never);
    }

    [Fact]
    public async Task GIVEN_IncludeContextIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnOnlyNonWhitespaceContexts()
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
                        Name = "Target.cs",
                        Source = """
                            class Target
                            {
                                public void Callee()
                                {
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Callers.cs",
                        Source = """
                            class AlphaCaller
                            {
                                public void RunAlpha()
                                {
                                    var target = new Target();
                                    target.Callee();
                                }
                            }

                            class BetaCaller
                            {
                                public void RunBeta()
                                {
                                    var target = new Target();
                                    target.Callee();
                                    target.Callee();
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new FindCallersTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var inspectionContextService = new Mock<IInspectionContextService>();
        var symbol = await GetMethodSymbolAsync(solution.GetDocument("Target.cs"), "Callee");
        var callerDocument = solution.GetDocument("Callers.cs");
        var alphaCalleeLocation = (await GetCalleeIdentifierLocationsAsync(callerDocument, "RunAlpha")).Single();
        var betaCalleeLocations = await GetCalleeIdentifierLocationsAsync(callerDocument, "RunBeta");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.InspectionContextService)
            .Returns(inspectionContextService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CallerSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CallerSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<CallerSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, CallerSearchData>
            {
                Value = solution.Solution.Projects.Single().Documents.ToArray(),
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(resolved => SelectorTestFactory.CreateSymbolReference(resolved));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(location => SelectorTestFactory.CreateResolvedLocation(location, Path.GetFileName(location.SourceTree!.FilePath!)));
        inspectionContextService
            .Setup(item => item.ReadContextAsync(
                callerDocument,
                It.Is<TextSpan>(span => span.Start == alphaCalleeLocation.SourceSpan.Start),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("target.Callee();");
        inspectionContextService
            .Setup(item => item.ReadContextAsync(
                callerDocument,
                It.Is<TextSpan>(span => span.Start == betaCalleeLocations[0].SourceSpan.Start),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("   ");
        inspectionContextService
            .Setup(item => item.ReadContextAsync(
                callerDocument,
                It.Is<TextSpan>(span => span.Start == betaCalleeLocations[1].SourceSpan.Start),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("target.Callee();");

        var result = await target.ExecuteAsync(new FindCallersRequest
        {
            Symbol = new SymbolSelector(),
            IncludeContext = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Callers.Items.Select(item => item.Caller!.DisplayName).Should().Equal("RunAlpha", "RunBeta");
        result.Data.Callers.Items[0].Contexts.Should().Equal("target.Callee();");
        result.Data.Callers.Items[1].Contexts.Should().Equal("target.Callee();");
        inspectionContextService.Verify(item => item.ReadContextAsync(
            callerDocument,
            It.IsAny<TextSpan>(),
            It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    private static async Task<IMethodSymbol> GetMethodSymbolAsync(Document document, string methodName)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(TestContext.Current.CancellationToken);
        var method = syntaxRoot!.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(item => item.Identifier.ValueText == methodName);

        return (IMethodSymbol)(semanticModel!.GetDeclaredSymbol(method, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException($"The method '{methodName}' could not be resolved."));
    }

    private static async Task<IReadOnlyList<Location>> GetCalleeIdentifierLocationsAsync(Document document, string methodName)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var method = syntaxRoot!.DescendantNodes().OfType<MethodDeclarationSyntax>().Single(item => item.Identifier.ValueText == methodName);

        return method.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(static item => item.Identifier.ValueText == "Callee")
            .Select(static item => item.GetLocation())
            .ToArray();
    }
}
