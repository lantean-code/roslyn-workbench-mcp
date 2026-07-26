using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetChangeImpactToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetChangeImpactTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<ChangeImpactData>(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ChangeImpactData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Rejected<ISymbol, ChangeImpactData>(expected));

        var result = await target.ExecuteAsync(new GetChangeImpactRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class BaseType
            {
                public virtual void Run()
                {
                }
            }
            """);

        var target = new GetChangeImpactTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Run",
            null,
            TestContext.Current.CancellationToken);

        var expected = PluginExecutionResult.Rejected<ChangeImpactData>(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ChangeImpactData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ChangeImpactData>(symbol));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ChangeImpactData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Rejected<IReadOnlyList<Document>, ChangeImpactData>(expected));

        var result = await target.ExecuteAsync(new GetChangeImpactRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolveProjectsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class BaseType
            {
                public virtual void Run()
                {
                }
            }
            """);

        var target = new GetChangeImpactTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Run",
            null,
            TestContext.Current.CancellationToken);

        var expected = PluginExecutionResult.Rejected<ChangeImpactData>(new PluginExecutionError
        {
            Code = "ProjectNotFound",
            Message = "ProjectNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<ChangeImpactData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ChangeImpactData>(symbol));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ChangeImpactData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, ChangeImpactData>([document.Document]));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<ChangeImpactData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Rejected<IReadOnlyList<Project>, ChangeImpactData>(expected));

        var result = await target.ExecuteAsync(new GetChangeImpactRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_MethodSymbolHasReferencesCallersAndOverrides_WHEN_CallingExecuteAsync_THEN_ShouldReturnImpactSummaryAndFilteredLocations()
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
                            class BaseType
                            {
                                public virtual string Run(string value)
                                {
                                    return value;
                                }
                            }

                            class DerivedType : BaseType
                            {
                                public override string Run(string value)
                                {
                                    return base.Run(value);
                                }
                            }

                            class Caller
                            {
                                string Execute(BaseType item)
                                {
                                    return item.Run("value");
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new GetChangeImpactTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var inspectionContextService = new Mock<IInspectionContextService>();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            solution.GetDocument("Code.cs"),
            "Run",
            "BaseType",
            TestContext.Current.CancellationToken);

        var document = solution.GetDocument("Code.cs");
        var referenceToSkip = (await RoslynDocumentTestHelper.GetSingleNodeLocationAsync(document, static (IdentifierNameSyntax item) =>
            item.Identifier.ValueText == "Run"
            && item.Parent is MemberAccessExpressionSyntax memberAccessExpressionSyntax
            && memberAccessExpressionSyntax.Expression is IdentifierNameSyntax { Identifier.ValueText: "item" }, TestContext.Current.CancellationToken)).SourceSpan.Start;

        var project = solution.Solution.Projects.Single();

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
            .Setup(item => item.ResolveSymbolAsync<ChangeImpactData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ChangeImpactData>(symbol));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ChangeImpactData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, ChangeImpactData>([document]));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<ChangeImpactData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, ChangeImpactData>([project]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => item.SourceSpan.Start == referenceToSkip ? null : SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item =>
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    return new SymbolReference
                    {
                        DisplayName = item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        Kind = item.Kind.ToString(),
                        DocumentationCommentId = item.GetDocumentationCommentId(),
                    };
                }

                return SelectorTestFactory.CreateSymbolReference(item);
            });

        inspectionContextService
            .Setup(item => item.ReadContextAsync(
                It.IsAny<Document>(),
                It.IsAny<TextSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("return item.Run(\"value\");");

        var result = await target.ExecuteAsync(new GetChangeImpactRequest
        {
            Symbol = new SymbolSelector(),
            LocationsLimit = 0,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Impact!.ReferenceCount.Should().BeGreaterThan(0);
        result.Data.Impact.CallerCount.Should().BeGreaterThan(0);
        result.Data.Impact.OverrideCount.Should().BeGreaterThan(0);
        result.Data.Impact.ImplementationCount.Should().Be(0);
        result.Data.Impact.PublicSurfaceCount.Should().Be(1);
        result.Data.Locations.Items.Should().OnlyContain(item => item.Location != null);
        result.Data.Locations.HasMore.Should().BeTrue();
        inspectionContextService.Verify(item => item.ReadContextAsync(
            It.IsAny<Document>(),
            It.IsAny<TextSpan>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_InterfaceSymbolAndPrivateMethod_WHEN_CallingExecuteAsync_THEN_ShouldReturnImplementationCountAndPrivateSurfaceCount()
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
                            interface IMessageFormatter
                            {
                                string Format(string value);
                            }

                            class AFormatter : IMessageFormatter
                            {
                                public string Format(string value)
                                {
                                    return value;
                                }
                            }

                            class BFormatter : IMessageFormatter
                            {
                                public string Format(string value)
                                {
                                    return value;
                                }
                            }

                            class Worker
                            {
                                private void Run()
                                {
                                    Run();
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new GetChangeImpactTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var inspectionContextService = new Mock<IInspectionContextService>();
        var interfaceSymbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("Code.cs"),
            "IMessageFormatter",
            TestContext.Current.CancellationToken);

        var privateMethod = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            solution.GetDocument("Code.cs"),
            "Run",
            "Worker",
            TestContext.Current.CancellationToken);

        var project = solution.Solution.Projects.Single();
        var document = solution.GetDocument("Code.cs");

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
            .SetupSequence(item => item.ResolveSymbolAsync<ChangeImpactData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ChangeImpactData>(interfaceSymbol))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, ChangeImpactData>(privateMethod));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ChangeImpactData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Document>, ChangeImpactData>([document]));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveProjects<ChangeImpactData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult.Resolved<IReadOnlyList<Project>, ChangeImpactData>([project]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(item, "Code.cs"));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item =>
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    return new SymbolReference
                    {
                        DisplayName = item.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                        Kind = item.Kind.ToString(),
                        DocumentationCommentId = item.GetDocumentationCommentId(),
                    };
                }

                return SelectorTestFactory.CreateSymbolReference(item);
            });

        inspectionContextService
            .Setup(item => item.ReadContextAsync(
                It.IsAny<Document>(),
                It.IsAny<TextSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("Run();");

        var interfaceResult = await target.ExecuteAsync(new GetChangeImpactRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        var privateResult = await target.ExecuteAsync(new GetChangeImpactRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        interfaceResult.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        interfaceResult.Data!.Impact!.ImplementationCount.Should().Be(2);
        interfaceResult.Data.Impact.PublicSurfaceCount.Should().Be(0);

        privateResult.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        privateResult.Data!.Impact!.PublicSurfaceCount.Should().Be(0);
    }
}
