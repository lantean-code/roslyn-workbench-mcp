namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetSymbolInfoToolTests
{
    [Fact]
    public void GIVEN_CollectionLimitsAreNullOrZero_WHEN_GettingEffectiveValues_THEN_ShouldUseDefaultsOrZero()
    {
        var target = new GetSymbolInfoRequest
        {
            Symbol = new SymbolSelector(),
            ParametersLimit = null,
            DeclarationsLimit = null,
        };

        target.EffectiveParametersLimit.Should().Be(64);
        target.EffectiveDeclarationsLimit.Should().Be(32);

        target = target with
        {
            ParametersLimit = 0,
            DeclarationsLimit = 0,
        };

        target.EffectiveParametersLimit.Should().Be(0);
        target.EffectiveDeclarationsLimit.Should().Be(0);
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetSymbolInfoTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult.Rejected<SymbolInfoData>(new PluginExecutionError
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
            .ReturnsAsync(ToolResolutionResult.Rejected<ISymbol, SymbolInfoData>(expected));

        var result = await target.ExecuteAsync(new GetSymbolInfoRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

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
                public string Format(string value, int count)
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
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, SymbolInfoData>(symbol));

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
            ParametersLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("Format");
        result.Data.Accessibility.Should().Be("Public");
        result.Data.Modifiers.Should().BeEmpty();
        result.Data.Parameters!.Items.Should().ContainSingle();
        result.Data.Parameters.Items[0].Name.Should().Be("value");
        result.Data.Parameters.HasMore.Should().BeTrue();
        result.Data.Parameters.TotalCount.Should().Be(2);
        result.Data.ReturnType!.DisplayName.Should().Be("string");
        result.Data.Documentation.Should().Contain("Formats a value.");
        result.Data.Declarations.Items.Should().ContainSingle();
        result.Data.Declarations.Items[0].Document!.Path.Should().Be("Code.cs");
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
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, SymbolInfoData>(symbol));

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
            DeclarationsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Documentation.Should().BeNull();
        result.Data.Parameters.Should().BeNull();
        result.Data.ReturnType.Should().BeNull();
        result.Data.Declarations.Items.Should().ContainSingle();
        result.Data.Declarations.Items.Select(item => item.Document!.Path).Should().Equal("A.cs");
        result.Data.Declarations.HasMore.Should().BeTrue();
        result.Data.Declarations.TotalCount.Should().BeNull();
    }
}
