using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindReferencesToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindReferencesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<ReferenceSearchData>.Rejected(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ReferenceSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, ReferenceSearchData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindReferencesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class StateHolder
            {
                public int Current
                {
                    get;
                    set;
                }
            }
            """);

        var target = new FindReferencesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredPropertySymbolAsync(
            document.Document,
            "Current",
            TestContext.Current.CancellationToken);
        var expected = PluginExecutionResult<ReferenceSearchData>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ReferenceSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, ReferenceSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ReferenceSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, ReferenceSearchData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindReferencesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_IncludeDefinitionsAndContextAreFalse_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedReferencesWithoutDefinitionsOrContext()
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
                        Name = "StateHolder.cs",
                        Source = """
                            class StateHolder
                            {
                                public int Current
                                {
                                    get;
                                    set;
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Usage.cs",
                        Source = """
                            class Usage
                            {
                                int Read(StateHolder holder)
                                {
                                    return holder.Current;
                                }

                                void Write(StateHolder holder, int value)
                                {
                                    holder.Current = value;
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new FindReferencesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var inspectionContextService = new Mock<IInspectionContextService>();
        var symbol = await RoslynDocumentTestHelper.GetRequiredPropertySymbolAsync(
            solution.GetDocument("StateHolder.cs"),
            "Current",
            TestContext.Current.CancellationToken);
        var documents = solution.Solution.Projects.Single().Documents.ToArray();

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.ToolExecutionServices
            .SetupGet(item => item.InspectionContextService)
            .Returns(inspectionContextService.Object);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ReferenceSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, ReferenceSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ReferenceSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, ReferenceSearchData>
            {
                Value = documents,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, Path.GetFileName(item.SourceTree!.FilePath!)));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindReferencesRequest
        {
            Symbol = new SymbolSelector(),
            IncludeDefinitions = false,
            IncludeContext = false,
            ReferencesLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.References.Items.Should().NotContain(item => item.IsDefinition);
        result.Data.References.Items.Select(item => item.Location!.Document!.Path).Should().Equal("Usage.cs");
        result.Data.References.Items.Select(item => item.IsWrite).Should().Equal(false);
        result.Data.References.HasMore.Should().BeTrue();
        result.Data.References.Items.All(item => item.Context is null).Should().BeTrue();
        inspectionContextService.Verify(item => item.TryCreateContainingSymbolAsync(
            It.IsAny<Document>(),
            It.IsAny<int>(),
            It.IsAny<CancellationToken>()), Times.Once);
        inspectionContextService.Verify(item => item.ReadContextAsync(
            It.IsAny<Document>(),
            It.IsAny<TextSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_IncludeDefinitionsAndSomeLocationsCannotBeResolved_WHEN_CallingExecuteAsync_THEN_ShouldReturnOnlyResolvedReferences()
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
                        Name = "StateHolder.cs",
                        Source = """
                            class StateHolder
                            {
                                public int Current
                                {
                                    get;
                                    set;
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Usage.cs",
                        Source = """
                            class Usage
                            {
                                int Read(StateHolder holder)
                                {
                                    return holder.Current;
                                }

                                int ReadAgain(StateHolder holder)
                                {
                                    return holder.Current;
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new FindReferencesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var inspectionContextService = new Mock<IInspectionContextService>();
        var symbol = await RoslynDocumentTestHelper.GetRequiredPropertySymbolAsync(
            solution.GetDocument("StateHolder.cs"),
            "Current",
            TestContext.Current.CancellationToken);
        var usageDocument = solution.GetDocument("Usage.cs");
        var usageRoot = await usageDocument.GetSyntaxRootAsync(TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The test document must have a syntax root.");
        var locationToSkip = usageRoot.DescendantNodes()
            .OfType<IdentifierNameSyntax>()
            .Where(static item => item.Identifier.ValueText == "Current")
            .Select(static item => item.SpanStart)
            .Last();

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
            .Setup(item => item.ResolveSymbolAsync<ReferenceSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, ReferenceSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ReferenceSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, ReferenceSearchData>
            {
                Value = solution.Solution.Projects.Single().Documents.ToArray(),
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => Path.GetFileName(item.SourceTree?.FilePath) == "StateHolder.cs" || item.SourceSpan.Start == locationToSkip
                ? null
                : SelectorTestFactory.CreateResolvedLocation(item, Path.GetFileName(item.SourceTree!.FilePath!)));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));
        inspectionContextService
            .Setup(item => item.TryCreateContainingSymbolAsync(
                It.IsAny<Document>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((ISymbol?)null);
        inspectionContextService
            .Setup(item => item.ReadContextAsync(
                It.IsAny<Document>(),
                It.IsAny<Microsoft.CodeAnalysis.Text.TextSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("return holder.Current;");

        var result = await target.ExecuteAsync(new FindReferencesRequest
        {
            Symbol = new SymbolSelector(),
            IncludeDefinitions = true,
            IncludeContext = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.References.Items.Should().NotContain(item => item.IsDefinition);
        result.Data.References.Items.Count(item => !item.IsDefinition).Should().Be(1);
    }

    [Fact]
    public async Task GIVEN_ReferenceIsAssignment_WHEN_CallingExecuteAsync_THEN_ShouldClassifyReferenceAsWrite()
    {
        await AssertWriteClassificationAsync("""
            class StateHolder
            {
                public int Current
                {
                    get;
                    set;
                }
            }

            class Usage
            {
                void Update(StateHolder holder, int value)
                {
                    holder.Current = value;
                }
            }
            """, "holder.Current = value;");
    }

    [Fact]
    public async Task GIVEN_ReferenceIsPrefixIncrement_WHEN_CallingExecuteAsync_THEN_ShouldClassifyReferenceAsWrite()
    {
        await AssertWriteClassificationAsync("""
            class StateHolder
            {
                public int Current
                {
                    get;
                    set;
                }
            }

            class Usage
            {
                void Update(StateHolder holder)
                {
                    ++holder.Current;
                }
            }
            """, "++holder.Current;");
    }

    [Fact]
    public async Task GIVEN_ReferenceIsPostfixDecrement_WHEN_CallingExecuteAsync_THEN_ShouldClassifyReferenceAsWrite()
    {
        await AssertWriteClassificationAsync("""
            class StateHolder
            {
                public int Current
                {
                    get;
                    set;
                }
            }

            class Usage
            {
                void Update(StateHolder holder)
                {
                    holder.Current--;
                }
            }
            """, "holder.Current--;");
    }

    [Fact]
    public async Task GIVEN_ReferenceIsRefArgument_WHEN_CallingExecuteAsync_THEN_ShouldClassifyReferenceAsWrite()
    {
        await AssertWriteClassificationAsync("""
            class StateHolder
            {
                public int Current
                {
                    get;
                    set;
                }
            }

            class Usage
            {
                void Touch(ref int value)
                {
                }

                void Update(StateHolder holder)
                {
                    Touch(ref holder.Current);
                }
            }
            """, "Touch(ref holder.Current);");
    }

    [Fact]
    public async Task GIVEN_ReferenceIsOutArgument_WHEN_CallingExecuteAsync_THEN_ShouldClassifyReferenceAsWrite()
    {
        await AssertWriteClassificationAsync("""
            class StateHolder
            {
                public int Current
                {
                    get;
                    set;
                }
            }

            class Usage
            {
                void Touch(out int value)
                {
                    value = 0;
                }

                void Update(StateHolder holder)
                {
                    Touch(out holder.Current);
                }
            }
            """, "Touch(out holder.Current);");
    }

    private static async Task AssertWriteClassificationAsync(string source, string expectedContext)
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
                        Source = source,
                    },
                ],
            },
        ]);

        var target = new FindReferencesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var inspectionContextService = new Mock<IInspectionContextService>();
        var symbol = await RoslynDocumentTestHelper.GetRequiredPropertySymbolAsync(
            solution.GetDocument("Code.cs"),
            "Current",
            TestContext.Current.CancellationToken);
        var codeDocument = solution.GetDocument("Code.cs");

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
            .Setup(item => item.ResolveSymbolAsync<ReferenceSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, ReferenceSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ReferenceSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, ReferenceSearchData>
            {
                Value = [codeDocument],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, Path.GetFileName(item.SourceTree!.FilePath!)));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));
        inspectionContextService
            .Setup(item => item.TryCreateContainingSymbolAsync(
                It.IsAny<Document>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
                codeDocument,
                "Update",
                null,
                TestContext.Current.CancellationToken));
        inspectionContextService
            .Setup(item => item.ReadContextAsync(
                It.IsAny<Document>(),
                It.IsAny<TextSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedContext);

        var result = await target.ExecuteAsync(new FindReferencesRequest
        {
            Symbol = new SymbolSelector(),
            IncludeContext = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.References.Items.Should().Contain(item => item.IsWrite && item.Context == expectedContext);
    }
}
