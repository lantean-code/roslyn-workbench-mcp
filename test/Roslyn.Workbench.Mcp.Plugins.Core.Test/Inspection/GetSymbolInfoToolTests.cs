namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetSymbolInfoToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetSymbolInfoTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<SymbolInfoData>.Rejected(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolInfoData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolInfoData>.Rejected(expected));

        var result = await target.ExecuteAsync(new GetSymbolInfoRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_MethodSymbolAndIncludeDocumentationIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnMethodMetadataAndDocumentation()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            namespace Sample;

            public sealed class Formatter
            {
                /// <summary>Formats a value.</summary>
                public string Format(string value)
                {
                    return value;
                }
            }
            """);

        var target = new GetSymbolInfoTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Format",
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolInfoData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolInfoData>.Resolved(symbol));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item => SelectorTestFactory.CreateResolvedLocation(
                Path.GetFileName(item.SourceTree!.FilePath!)!,
                item.SourceSpan.Start,
                item.SourceSpan.Length));

        var result = await target.ExecuteAsync(new GetSymbolInfoRequest
        {
            Symbol = new SymbolSelector(),
            IncludeDocumentation = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("Format");
        result.Data.Accessibility.Should().Be("Public");
        result.Data.Parameters.Should().ContainSingle();
        result.Data.Parameters![0].Name.Should().Be("value");
        result.Data.ReturnType!.DisplayName.Should().Be("string");
        result.Data.Documentation.Should().Contain("Formats a value.");
        result.Data.Declarations.Should().ContainSingle();
        result.Data.Declarations[0].Document!.Path.Should().Be("Code.cs");
    }

    [Fact]
    public async Task GIVEN_NamedTypeSymbolAndIncludeDocumentationIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedSourceDeclarationsWithoutNullLocations()
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

        var target = new GetSymbolInfoTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            solution.GetDocument("A.cs"),
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolInfoData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolInfoData>.Resolved(symbol));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateResolvedLocation(It.IsAny<Location>()))
            .Returns<Location>(item =>
            {
                var path = Path.GetFileName(item.SourceTree!.FilePath!);
                if (path == "B.cs")
                {
                    return null;
                }

                return SelectorTestFactory.CreateResolvedLocation(
                    path!,
                    item.SourceSpan.Start,
                    item.SourceSpan.Length);
            });

        var result = await target.ExecuteAsync(new GetSymbolInfoRequest
        {
            Symbol = new SymbolSelector(),
            IncludeDocumentation = false,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Documentation.Should().BeNull();
        result.Data.Parameters.Should().BeNull();
        result.Data.ReturnType.Should().BeNull();
        result.Data.Declarations.Should().HaveCount(2);
        result.Data.Declarations.Select(item => item.Document!.Path).Should().Equal("A.cs", "C.cs");
    }
}
