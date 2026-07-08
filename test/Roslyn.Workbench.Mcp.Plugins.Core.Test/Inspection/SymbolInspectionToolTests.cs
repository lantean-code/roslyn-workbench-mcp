namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class ResolveSymbolToolTests
{
    [Fact]
    public async Task GIVEN_LocationIsNull_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        var target = new ResolveSymbolTool();
        var context = new QueryContextBuilder().Build();

        var result = await target.ExecuteAsync(new ResolveSymbolRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_LocationResolvesToSourceSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnSymbolSelectorAndDeclarations()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new ResolveSymbolTool();

        var result = await target.ExecuteAsync(new ResolveSymbolRequest
        {
            Location = workspace.GetLocationSelector("Format"),
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Contain("Format");
        result.Data.Selector.Should().NotBeNull();
        result.Data.Declarations.Should().ContainSingle();
    }
}

public sealed class GoToDefinitionToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<DefinitionData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var context = CreateContext(requestResolver: requestResolver.Object);
        var target = new GoToDefinitionTool();

        requestResolver
            .Setup(resolver => resolver.ResolveSymbolAsync<DefinitionData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IQueryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, DefinitionData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GoToDefinitionRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_SourceSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnSourceDefinitionLocations()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new GoToDefinitionTool();

        var result = await target.ExecuteAsync(new GoToDefinitionRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.Formatter.Format",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Definitions.Should().ContainSingle();
        result.Data.Definitions[0].Location!.Document!.Path.Should().Be("Sample.cs");
    }

    private static IQueryContext CreateContext(MiniWorkspace? workspace = null, IToolRequestResolver? requestResolver = null)
    {
        var currentWorkspace = workspace ?? MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var workspaceIdentity = currentWorkspace.CreateWorkspaceIdentity();
        var services = new ToolExecutionServicesBuilder()
            .WithRequestResolver(requestResolver ?? Mock.Of<IToolRequestResolver>())
            .Build();

        return new QueryContextBuilder()
            .WithCurrentSolution(currentWorkspace.Solution)
            .WithResolver(currentWorkspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(services)
            .Build();
    }
}

public sealed class FindOverloadsToolTests
{
    [Fact]
    public async Task GIVEN_SymbolIsNotMethodOrConstructor_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new FindOverloadsTool();

        var result = await target.ExecuteAsync(new FindOverloadsRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.Formatter",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_MethodHasOverloads_WHEN_CallingExecuteAsync_THEN_ShouldReturnOverloadSignatures()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }

                public void Format(string value)
                {
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new FindOverloadsTool();

        var result = await target.ExecuteAsync(new FindOverloadsRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.Formatter.Format",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Overloads.Items.Should().HaveCount(2);
    }
}

public sealed class GetSymbolMembersToolTests
{
    [Fact]
    public async Task GIVEN_SymbolIsNotNamedType_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new GetSymbolMembersTool();

        var result = await target.ExecuteAsync(new GetSymbolMembersRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.Formatter.Format",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_NamedTypeIncludesInheritedAndInterfaceMembers_WHEN_CallingExecuteAsync_THEN_ShouldReturnDistinctMembers()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public interface IMessageFormatter
            {
                void Format();
            }

            public class BaseFormatter
            {
                public void Decorate()
                {
                }
            }

            public sealed class Formatter : BaseFormatter, IMessageFormatter
            {
                public void Format()
                {
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new GetSymbolMembersTool();

        var result = await target.ExecuteAsync(new GetSymbolMembersRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.Formatter",
            },
            IncludeInherited = true,
            IncludeExplicitInterface = true,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Members.Items.Should().Contain(member => member.DisplayName.Contains("Format", StringComparison.Ordinal));
        result.Data.Members.Items.Should().Contain(member => member.DisplayName.Contains("Decorate", StringComparison.Ordinal));
    }
}

public sealed class SearchSymbolsToolTests
{
    [Fact]
    public async Task GIVEN_QueryAndMetadataNameAreEmpty_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("namespace Sample;");
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new SearchSymbolsTool();

        var result = await target.ExecuteAsync(new SearchSymbolsRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_QueryAndFiltersMatchSymbols_WHEN_CallingExecuteAsync_THEN_ShouldReturnFilteredSymbols()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }

                private void FormatInternal()
                {
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new SearchSymbolsTool();

        var result = await target.ExecuteAsync(new SearchSymbolsRequest
        {
            Query = "Format",
            Kinds = ["Method"],
            Accessibilities = ["Public"],
            Namespace = "Sample",
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().ContainSingle(symbol => symbol.DisplayName.Contains("Format()", StringComparison.Ordinal));
    }
}

public sealed class GetTestImpactToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var expected = PluginExecutionResult<TestImpactData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(new ToolExecutionServicesBuilder()
                .WithRequestResolver(requestResolver.Object)
                .WithDependencyAnalysisService(dependencyAnalysisService.Object)
                .Build())
            .Build();
        var target = new GetTestImpactTool();

        var compilation = await workspace.Solution.Projects.Single().GetCompilationAsync(TestContext.Current.CancellationToken);
        requestResolver
            .Setup(resolver => resolver.ResolveSymbolAsync<TestImpactData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IQueryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, TestImpactData>
            {
                Value = compilation!.GetTypeByMetadataName("Sample.Formatter")!,
            });
        requestResolver
            .Setup(resolver => resolver.ResolveDocuments<TestImpactData>(
                It.IsAny<ScopeSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<IReadOnlyList<Document>, TestImpactData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetTestImpactRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolvedSymbolAndTestScope_WHEN_CallingExecuteAsync_THEN_ShouldReturnImpactedTests()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp(
        [
            ("App.cs", """
                namespace Sample;

                public sealed class Formatter
                {
                    public void Format()
                    {
                    }
                }
                """),
            ("FormatterTests.cs", """
                namespace Sample.Tests;

                public static class FormatterTests
                {
                    public static void GIVEN_Formatter_WHEN_CallingFormat_THEN_ShouldRun()
                    {
                    }
                }
                """),
        ]);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var dependencyAnalysisService = new Mock<IDependencyAnalysisService>();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithToolExecutionServices(new ToolExecutionServicesBuilder()
                .WithDependencyAnalysisService(dependencyAnalysisService.Object)
                .Build())
            .Build();
        var target = new GetTestImpactTool();

        dependencyAnalysisService
            .Setup(service => service.FindTestImpactsAsync(
                It.IsAny<ISymbol>(),
                It.Is<IReadOnlyList<Document>>(documents => documents.Any(document => document.Name == "FormatterTests.cs")),
                true,
                context,
                CancellationToken.None))
            .ReturnsAsync(
            [
                new TestImpactInfo
                {
                    Test = new SymbolReference
                    {
                        DisplayName = "FormatterTests.GIVEN_Formatter_WHEN_CallingFormat_THEN_ShouldRun()",
                    },
                    Reasons = ["Reason"],
                },
            ]);

        var result = await target.ExecuteAsync(new GetTestImpactRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.Formatter",
            },
            TestScope = new ScopeSelector
            {
                Kind = ScopeKind.Project,
                Project = new ProjectSelector
                {
                    Path = "Sample.csproj",
                },
            },
            IncludeReasons = true,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Tests.Items.Should().ContainSingle(test => test.Test!.DisplayName.Contains("FormatterTests", StringComparison.Ordinal));
    }
}
