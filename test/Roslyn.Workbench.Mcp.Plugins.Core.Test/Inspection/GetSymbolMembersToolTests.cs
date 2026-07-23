namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetSymbolMembersToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetSymbolMembersTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<SymbolMembersData>.Rejected(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolMembersData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolMembersData>.Rejected(expected));

        var result = await target.ExecuteAsync(new GetSymbolMembersRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_ResolvedSymbolIsNotNamedType_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequest()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public sealed class Formatter
            {
                public void Format()
                {
                }
            }
            """);

        var target = new GetSymbolMembersTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Format",
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolMembersData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolMembersData>.Resolved(symbol));

        var result = await target.ExecuteAsync(new GetSymbolMembersRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_NamedTypeWithoutInheritedOrInterfaceMembers_WHEN_CallingExecuteAsync_THEN_ShouldReturnDeclaredNonImplicitMembersOnly()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public sealed class Formatter
            {
                public string Name
                {
                    get;
                } = string.Empty;

                public void Format()
                {
                }
            }
            """);

        var target = new GetSymbolMembersTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolMembersData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolMembersData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item =>
            {
                if (item.Name == "Decorate")
                {
                    return SelectorTestFactory.CreateSymbolReference(
                        item.Name,
                        item.Kind,
                        item.GetDocumentationCommentId(),
                        new ResolvedLocation());
                }

                var sourceLocation = item.Locations.FirstOrDefault(static location => location.IsInSource);
                var location = sourceLocation == null
                    ? null
                    : SelectorTestFactory.CreateResolvedLocation(
                        Path.GetFileName(sourceLocation.SourceTree!.FilePath!)!,
                        sourceLocation.SourceSpan.Start,
                        sourceLocation.SourceSpan.Length);

                return SelectorTestFactory.CreateSymbolReference(item.Name, item.Kind, item.GetDocumentationCommentId(), location);
            });

        var result = await target.ExecuteAsync(new GetSymbolMembersRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Members.Items.Select(item => item.DisplayName).Should().Equal("Format", "Name", "get_Name");

        var boundedResult = await target.ExecuteAsync(new GetSymbolMembersRequest
        {
            Symbol = new SymbolSelector(),
            MembersLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        boundedResult.Data!.Members.Items.Select(item => item.DisplayName).Should().Equal("Format");
        boundedResult.Data.Members.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_NamedTypeIncludesInheritedAndExplicitInterfaceMembers_WHEN_CallingExecuteAsync_THEN_ShouldReturnDistinctOrderedMembers()
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
                        Name = "BaseFormatter.cs",
                        Source = """
                            namespace Sample;

                            public class BaseFormatter
                            {
                                public void Decorate()
                                {
                                }
                            }
                            """,
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Formatter.cs",
                        Source = """
                            using System;

                            namespace Sample;

                            public sealed class Formatter : BaseFormatter, IDisposable
                            {
                                public void Dispose()
                                {
                                }
                            }
                            """,
                    },
                ],
            },
        ]);

        var target = new GetSymbolMembersTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("Formatter.cs"),
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolMembersData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolMembersData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item =>
            {
                var sourceLocation = item.Locations.FirstOrDefault(static location => location.IsInSource);
                var location = sourceLocation == null
                    ? null
                    : SelectorTestFactory.CreateResolvedLocation(
                        Path.GetFileName(sourceLocation.SourceTree!.FilePath!)!,
                        sourceLocation.SourceSpan.Start,
                        sourceLocation.SourceSpan.Length);

                return SelectorTestFactory.CreateSymbolReference(item.Name, item.Kind, item.GetDocumentationCommentId(), location);
            });

        var result = await target.ExecuteAsync(new GetSymbolMembersRequest
        {
            Symbol = new SymbolSelector(),
            IncludeInherited = true,
            IncludeExplicitInterface = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Members.Items.Should().Contain(item => item.DocumentationCommentId == "M:Sample.BaseFormatter.Decorate");
        result.Data.Members.Items.Count(item => item.DisplayName == "Dispose").Should().Be(2);
        result.Data.Members.Items
            .Where(item => item.DisplayName == "Dispose")
            .Select(item => item.DocumentationCommentId)
            .Should()
            .Equal("M:System.IDisposable.Dispose", "M:Sample.Formatter.Dispose");
    }
}
