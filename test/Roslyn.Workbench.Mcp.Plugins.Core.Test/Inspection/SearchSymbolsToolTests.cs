namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class SearchSymbolsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveProjectsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<SymbolSearchData>(new PluginExecutionError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Rejected<IReadOnlyList<Project>, SymbolSearchData>(expected));

        var request = new SearchSymbolsRequest
        {
            Query = "ScopeTarget",
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = new ProjectSelector
                {
                    Name = "Project",
                },
            },
        };

        var result = await target.ExecuteAsync(request, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_DocumentResolutionHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<SymbolSearchData>(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<SymbolSearchData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Rejected<Document, SymbolSearchData>(expected));

        var request = new SearchSymbolsRequest
        {
            Query = "ScopeTarget",
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "AlphaTwo.cs",
                },
            },
        };

        var result = await target.ExecuteAsync(request, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        queryContextMocks.RequestResolver.Verify(item => item.ResolveProjects<SymbolSearchData>(
            It.IsAny<ScopeSelector?>(),
            It.IsAny<IQueryContext>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SolutionScope_WHEN_CallingExecuteAsync_THEN_ShouldReturnMatchesFromEveryProject()
    {
        using var solution = CreateScopedSearchSolution();
        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var request = new SearchSymbolsRequest
        {
            Query = "ScopeTarget",
            Kinds = ["NamedType"],
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Solution,
            },
        };

        var result = await target.ExecuteAsync(request, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Select(static item => item.DisplayName).Should().Equal(
            "ScopeTargetAlphaOne",
            "ScopeTargetAlphaTwo",
            "ScopeTargetBetaOne",
            "ScopeTargetBetaTwo",
            "ScopeTargetGammaOne",
            "ScopeTargetShared");
        result.Data.Symbols.HasMore.Should().BeFalse();
        result.Data.Symbols.TotalCount.Should().Be(6);
    }

    [Fact]
    public async Task GIVEN_ProjectScope_WHEN_CallingExecuteAsync_THEN_ShouldReturnMatchesFromSelectedProject()
    {
        using var solution = CreateScopedSearchSolution();
        var selectedProject = solution.Solution.Projects.Single(static project => project.Name == "Alpha");
        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([selectedProject]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var request = new SearchSymbolsRequest
        {
            Query = "ScopeTarget",
            Kinds = ["NamedType"],
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = new ProjectSelector
                {
                    Name = "Alpha",
                },
            },
        };

        var result = await target.ExecuteAsync(request, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Select(static item => item.DisplayName).Should().Equal(
            "ScopeTargetAlphaOne",
            "ScopeTargetAlphaTwo",
            "ScopeTargetShared");
        result.Data.Symbols.HasMore.Should().BeFalse();
        result.Data.Symbols.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task GIVEN_ProjectsScope_WHEN_CallingExecuteAsync_THEN_ShouldReturnMatchesFromSelectedProjects()
    {
        using var solution = CreateScopedSearchSolution();
        var selectedProjects = solution.Solution.Projects
            .Where(static project => project.Name is "Alpha" or "Gamma")
            .ToArray();

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>(selectedProjects));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var request = new SearchSymbolsRequest
        {
            Query = "ScopeTarget",
            Kinds = ["NamedType"],
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Projects,
                Projects =
                [
                    new ProjectSelector
                    {
                        Name = "Alpha",
                    },
                    new ProjectSelector
                    {
                        Name = "Gamma",
                    },
                ],
            },
        };

        var result = await target.ExecuteAsync(request, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Select(static item => item.DisplayName).Should().Equal(
            "ScopeTargetAlphaOne",
            "ScopeTargetAlphaTwo",
            "ScopeTargetGammaOne",
            "ScopeTargetShared");
        result.Data.Symbols.HasMore.Should().BeFalse();
        result.Data.Symbols.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task GIVEN_DocumentScopeAndLowLimit_WHEN_CallingExecuteAsync_THEN_ShouldFilterBeforeBounding()
    {
        using var solution = CreateScopedSearchSolution();
        var selectedDocument = solution.Solution.Projects
            .Single(static project => project.Name == "Alpha")
            .Documents
            .Single(static document => document.Name == "AlphaTwo.cs");

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocument<SymbolSearchData>(
                It.IsAny<DocumentSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<Document, SymbolSearchData>(selectedDocument));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var request = new SearchSymbolsRequest
        {
            Query = "ScopeTarget",
            Kinds = ["NamedType"],
            SymbolsLimit = 1,
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Document,
                Document = new DocumentSelector
                {
                    Path = "AlphaTwo.cs",
                },
            },
        };

        var result = await target.ExecuteAsync(request, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().ContainSingle().Which.DisplayName.Should().Be("ScopeTargetAlphaTwo");
        result.Data.Symbols.HasMore.Should().BeTrue();
        result.Data.Symbols.TotalCount.Should().Be(2);
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
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([project]));

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
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([project]));

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
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([project]));

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
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([project]));

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
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([project]));

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
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([project]));

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
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([project]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            MetadataName = "Format",
            Kinds = [],
            Accessibilities = [],
            SymbolsLimit = 2,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().HaveCount(2);
        result.Data.Symbols.Items.Select(item => item.Location!.Document!.Path).Should().Equal("Alpha.cs", "Beta.cs");
        result.Data.Symbols.HasMore.Should().BeTrue();
        result.Data.Symbols.TotalCount.Should().Be(6);
    }

    [Fact]
    public async Task GIVEN_QueryAndMatchingKindFilter_WHEN_CallingExecuteAsync_THEN_ShouldReturnFilteredSymbols()
    {
        using var solution = CreateSearchSymbolsSolution();

        var target = new SearchSymbolsTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([project]));

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
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([project]));

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
                        Location = new ResolvedLocation
                        {
                            Snapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(
                                Guid.Parse("11111111-1111-1111-1111-111111111111")),
                        },
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
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([project]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(CreateSearchSymbolReference);

        var projectSelector = new ProjectSelector { Name = "Project" };
        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            MetadataName = "Format",
            Accessibilities = ["Internal"],
            Scope = new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = projectSelector,
            },
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
        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        var project = solution.Solution.Projects.Single();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<SymbolSearchData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, SymbolSearchData>([project]));

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

    private static InMemoryRoslynSolution CreateScopedSearchSolution()
    {
        return RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "Alpha",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "AlphaOne.cs",
                        Source = """
                            public sealed class ScopeTargetAlphaOne
                            {
                            }

                            public partial class ScopeTargetShared
                            {
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "AlphaTwo.cs",
                        Source = """
                            public sealed class ScopeTargetAlphaTwo
                            {
                            }

                            public partial class ScopeTargetShared
                            {
                            }
                            """,
                    },
                ],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Beta",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "BetaOne.cs",
                        Source = """
                            public sealed class ScopeTargetBetaOne
                            {
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "BetaTwo.cs",
                        Source = """
                            public sealed class ScopeTargetBetaTwo
                            {
                            }
                            """,
                    },
                ],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "Gamma",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "GammaOne.cs",
                        Source = """
                            public sealed class ScopeTargetGammaOne
                            {
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
