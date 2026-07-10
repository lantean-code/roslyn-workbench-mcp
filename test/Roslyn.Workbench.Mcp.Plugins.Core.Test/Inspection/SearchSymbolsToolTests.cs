namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class SearchSymbolsToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        SearchSymbolsTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<SearchSymbolsRequest, SymbolSearchData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "search-symbols"
                && metadata.Title == "Search Symbols"
                && metadata.Description == "Searches declarations by name, metadata name and optional semantic filters."),
            It.IsAny<IQueryToolHandler<SearchSymbolsRequest, SymbolSearchData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveProjectsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<SymbolSearchData>.Rejected(new PluginExecutionError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new SearchSymbolsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_QueryAndMetadataNameAreEmpty_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
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
                        Source = "namespace Sample; public sealed class Formatter { }",
                    },
                ],
            },
        ]);

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Value = [project],
            });

        var result = await target.ExecuteAsync(new SearchSymbolsRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
        result.Error.Message.Should().Be("Search symbols requires query or metadataName.");
    }

    [Fact]
    public async Task GIVEN_MetadataNameDoesNotMatchDeclarations_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyResults()
    {
        using var solution = CreateSearchSymbolsSolution();

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            Query = "Format",
            MetadataName = "Missing",
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_KindFilterDoesNotMatchDeclarations_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyResults()
    {
        using var solution = CreateSearchSymbolsSolution();

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            Query = "Format",
            Kinds = ["Property"],
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_AccessibilityFilterDoesNotMatchDeclarations_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyResults()
    {
        using var solution = CreateSearchSymbolsSolution();

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            Query = "Format",
            Accessibilities = ["Protected"],
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_NamespaceFilterDoesNotMatchDeclarations_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyResults()
    {
        using var solution = CreateSearchSymbolsSolution();

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            Query = "Format",
            Namespace = "Missing",
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_NamespaceFilterAndGlobalNamespaceDeclarations_WHEN_CallingExecuteAsync_THEN_ShouldReturnEmptyResults()
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
                        Name = "Global.cs",
                        Source = """
                            public sealed class Formatter
                            {
                                public void Format()
                                {
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            MetadataName = "Format",
            Namespace = "Sample",
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_MetadataNameAndEmptyCollections_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedOrderedMatches()
    {
        using var solution = CreateSearchSymbolsSolution();

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            MetadataName = "Format",
            Kinds = [],
            Accessibilities = [],
            SymbolsLimit = new CollectionLimit
            {
                MaxResults = 2,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().HaveCount(2);
        result.Data.Symbols.Items.Select(item => item.Location!.Document!.Path).Should().Equal("Alpha.cs", "Beta.cs");
        result.Data.Symbols.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_QueryAndMatchingKindFilter_WHEN_CallingExecuteAsync_THEN_ShouldReturnFilteredSymbols()
    {
        using var solution = CreateSearchSymbolsSolution();

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item =>
            {
                var sourceLocation = item.Locations.FirstOrDefault(static value => value.IsInSource);
                var path = Path.GetFileName(sourceLocation?.SourceTree?.FilePath);

                if (path == "Alpha.cs")
                {
                    return SelectorTestFactory.CreateSymbolReference(
                        item.Name,
                        item.Kind,
                        item.GetDocumentationCommentId());
                }

                return CreateSearchSymbolReference(item);
            });

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            MetadataName = "Format",
            Kinds = ["Method"],
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().HaveCount(3);
        result.Data.Symbols.Items[0].Location.Should().BeNull();
        result.Data.Symbols.Items.Skip(1).Select(item => item.Location!.Document!.Path).Should().Equal("Beta.cs", "Gamma.cs");
    }

    [Fact]
    public async Task GIVEN_MatchingKindFilterAndProjectedLocationHasNoDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnFilteredSymbols()
    {
        using var solution = CreateSearchSymbolsSolution();

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item =>
            {
                var sourceLocation = item.Locations.FirstOrDefault(static value => value.IsInSource);
                var path = Path.GetFileName(sourceLocation?.SourceTree?.FilePath);

                if (path == "Alpha.cs")
                {
                    return new SymbolReference
                    {
                        DisplayName = item.Name,
                        Kind = item.Kind.ToString(),
                        DocumentationCommentId = item.GetDocumentationCommentId(),
                        Location = new ResolvedLocation(),
                    };
                }

                return CreateSearchSymbolReference(item);
            });

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            MetadataName = "Format",
            Kinds = ["Method"],
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().HaveCount(3);
        result.Data.Symbols.Items[0].Location!.Document.Should().BeNull();
        result.Data.Symbols.Items.Skip(1).Select(item => item.Location!.Document!.Path).Should().Equal("Beta.cs", "Gamma.cs");
    }

    [Fact]
    public async Task GIVEN_QueryAndMatchingAccessibilityFilter_WHEN_CallingExecuteAsync_THEN_ShouldReturnFilteredSymbols()
    {
        using var solution = CreateSearchSymbolsSolution();

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            MetadataName = "Format",
            Accessibilities = ["Internal"],
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().HaveCount(2);
        result.Data.Symbols.Items.Select(item => item.Location!.Document!.Path).Should().Equal("Beta.cs", "Beta.cs");
    }

    [Fact]
    public async Task GIVEN_QueryAndMatchingNamespaceFilter_WHEN_CallingExecuteAsync_THEN_ShouldReturnFilteredSymbols()
    {
        using var solution = CreateSearchSymbolsSolution();

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(new ToolResolutionResult<IReadOnlyList<Project>, SymbolSearchData>
            {
                Value = [project],
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            MetadataName = "Format",
            Namespace = "Sample",
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().HaveCount(4);
        result.Data.Symbols.Items.Select(item => item.Location!.Document!.Path).Should().OnlyContain(item => item == "Alpha.cs" || item == "Beta.cs");
    }

    private static InMemoryRoslynSolution CreateSearchSymbolsSolution()
    {
        return RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Project",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Alpha.cs",
                        Source = """
                            namespace Sample;

                            public sealed class AlphaFormatter
                            {
                                public void Format()
                                {
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Beta.cs",
                        Source = """
                            namespace Sample;

                            internal sealed class BetaFormatter
                            {
                                internal void Format()
                                {
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Gamma.cs",
                        Source = """
                            namespace Other;

                            public sealed class GammaFormatter
                            {
                                public void Format()
                                {
                                }
                            }
                            """,
                    },
                ],
            },
        ]);
    }

    private static SymbolReference CreateSearchSymbolReference(ISymbol symbol)
    {
        var sourceLocation = symbol.Locations.FirstOrDefault(static item => item.IsInSource);
        var location = sourceLocation is null
            ? null
            : SelectorTestFactory.CreateResolvedLocation(
                Path.GetFileName(sourceLocation.SourceTree!.FilePath!)!,
                sourceLocation.SourceSpan.Start,
                sourceLocation.SourceSpan.Length);

        return SelectorTestFactory.CreateSymbolReference(
            symbol.Name,
            symbol.Kind,
            symbol.GetDocumentationCommentId(),
            location);
    }
}
