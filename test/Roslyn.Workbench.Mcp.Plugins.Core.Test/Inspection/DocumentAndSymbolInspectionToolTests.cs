using Microsoft.CodeAnalysis.CSharp;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetDocumentOptionsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<DocumentOptionsData>.Rejected(new ToolError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var context = CreateContext(requestResolver: requestResolver.Object);
        var target = new GetDocumentOptionsTool();

        requestResolver
            .Setup(resolver => resolver.ResolveDocument<DocumentOptionsData>(
                It.IsAny<DocumentSelector?>(),
                It.IsAny<IToolExecutionContext>()))
            .Returns(new ToolResolutionResult<Document, DocumentOptionsData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetDocumentOptionsRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_CSharpDocumentWithNullableEnabled_WHEN_CallingExecuteAsync_THEN_ShouldReturnDocumentOptions()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            #nullable enable
            namespace Sample;

            public sealed class Formatter
            {
                public string Format(string value)
                {
                    return value;
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new GetDocumentOptionsTool();

        var result = await target.ExecuteAsync(new GetDocumentOptionsRequest
        {
            Document = new DocumentSelector
            {
                Path = "Sample.cs",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Document!.Path.Should().Be("Sample.cs");
        result.Data.LanguageVersion.Should().NotBeNullOrWhiteSpace();
        result.Data.NullableContext.Should().Be(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).NullableContextOptions.ToString());
        result.Data.ParseOptions.Should().NotBeNull();
        result.Data.AnalyzerConfig.Should().NotBeNull();
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

public sealed class GetPartialDeclarationsToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<PartialDeclarationsData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var context = CreateContext(requestResolver: requestResolver.Object);
        var target = new GetPartialDeclarationsTool();

        requestResolver
            .Setup(resolver => resolver.ResolveSymbolAsync<PartialDeclarationsData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IQueryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, PartialDeclarationsData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetPartialDeclarationsRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_PartialTypeWithDeclarationLimit_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedDeclarations()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp(
        [
            ("Part1.cs", """
                namespace Sample;

                public partial class Formatter
                {
                }
                """),
            ("Part2.cs", """
                namespace Sample;

                public partial class Formatter
                {
                }
                """),
        ]);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithDefaultMaxResults(1)
            .Build();
        var target = new GetPartialDeclarationsTool();

        var result = await target.ExecuteAsync(new GetPartialDeclarationsRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.Formatter",
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Contain("Formatter");
        result.Data.Declarations.Items.Should().HaveCount(1);
        result.Data.Declarations.HasMore.Should().BeTrue();
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

public sealed class GetSymbolAttributesToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<SymbolAttributesData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });
        var requestResolver = new Mock<IToolRequestResolver>();
        var context = CreateContext(requestResolver: requestResolver.Object);
        var target = new GetSymbolAttributesTool();

        requestResolver
            .Setup(resolver => resolver.ResolveSymbolAsync<SymbolAttributesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                It.IsAny<IQueryContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, SymbolAttributesData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetSymbolAttributesRequest(), context, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_DerivedTypeIncludingInheritedAttributes_WHEN_CallingExecuteAsync_THEN_ShouldReturnDeclaredAndInheritedAttributes()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            using System;

            namespace Sample;

            [Obsolete("Base")]
            public class BaseType
            {
            }

            [Serializable]
            public class DerivedType : BaseType
            {
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(workspace.CreateResolver(workspaceIdentity))
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new GetSymbolAttributesTool();

        var result = await target.ExecuteAsync(new GetSymbolAttributesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "T:Sample.DerivedType",
            },
            IncludeInherited = true,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Attributes.Items.Should().Contain(attribute => attribute.Name == "System.SerializableAttribute" && !attribute.Inherited);
        result.Data.Attributes.Items.Should().Contain(attribute => attribute.Name == "System.ObsoleteAttribute" && attribute.Inherited);
    }

    [Fact]
    public async Task GIVEN_MethodSymbolWithoutInheritedAttributes_WHEN_CallingExecuteAsync_THEN_ShouldReturnDeclaredAttributesOnly()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            using System;

            namespace Sample;

            public sealed class Formatter
            {
                [Obsolete("Method")]
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
        var target = new GetSymbolAttributesTool();

        var result = await target.ExecuteAsync(new GetSymbolAttributesRequest
        {
            Symbol = new SymbolSelector
            {
                DocumentationCommentId = "M:Sample.Formatter.Format",
            },
            IncludeInherited = true,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Attributes.Items.Should().ContainSingle(attribute => attribute.Name == "System.ObsoleteAttribute");
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
