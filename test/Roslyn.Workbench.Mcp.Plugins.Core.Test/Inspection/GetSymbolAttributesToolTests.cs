namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetSymbolAttributesToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        GetSymbolAttributesTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<GetSymbolAttributesRequest, SymbolAttributesData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "get-symbol-attributes"
                && metadata.Title == "Get Symbol Attributes"
                && metadata.Description == "Returns declared and inherited attributes for a resolved symbol."),
            It.IsAny<IQueryToolHandler<GetSymbolAttributesRequest, SymbolAttributesData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetSymbolAttributesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<SymbolAttributesData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolAttributesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, SymbolAttributesData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new GetSymbolAttributesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_NamedTypeIncludesInheritedAttributes_WHEN_CallingExecuteAsync_THEN_ShouldReturnDeclaredAndBaseAttributesInOrder()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            public sealed class MarkerAttribute : Attribute
            {
                public MarkerAttribute(string value)
                {
                    Identifier = value;
                }

                public string Identifier
                {
                    get;
                }

                public string? Note
                {
                    get;
                    set;
                }
            }

            [Marker("Base", Note = "BaseNote")]
            public class BaseType
            {
            }

            [Marker("Derived", Note = "DerivedNote")]
            public class DerivedType : BaseType
            {
            }
            """);

        var target = new GetSymbolAttributesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "DerivedType",
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolAttributesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, SymbolAttributesData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolAttributesRequest
        {
            Symbol = new SymbolSelector(),
            IncludeInherited = true,
            AttributesLimit = new CollectionLimit
            {
                MaxResults = 1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Symbol!.DisplayName.Should().Be("DerivedType");
        result.Data.Attributes.Items.Should().ContainSingle();
        result.Data.Attributes.Items[0].Name.Should().Be("MarkerAttribute");
        result.Data.Attributes.Items[0].Inherited.Should().BeFalse();
        result.Data.Attributes.Items[0].ConstructorArguments.Should().ContainSingle(item => item.Value == "Derived");
        result.Data.Attributes.Items[0].NamedArguments.Should().ContainSingle(item => item.Name == "Note" && item.Value == "DerivedNote");
        result.Data.Attributes.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_NonNamedTypeSymbolAndIncludeInheritedIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnDeclaredAttributesOnly()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            public sealed class Formatter
            {
                public void Format(int value)
                {
                }
            }
            """);

        var target = new GetSymbolAttributesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var method = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Format",
            null,
            TestContext.Current.CancellationToken);
        var symbol = method.Parameters.Single();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolAttributesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, SymbolAttributesData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolAttributesRequest
        {
            Symbol = new SymbolSelector(),
            IncludeInherited = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Attributes.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_NamedTypeSymbolAndIncludeInheritedIsFalse_WHEN_CallingExecuteAsync_THEN_ShouldExcludeInheritedAttributes()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            public sealed class MarkerAttribute : Attribute
            {
            }

            [Marker]
            public class BaseType
            {
            }

            [Marker]
            public class DerivedType : BaseType
            {
            }
            """);

        var target = new GetSymbolAttributesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "DerivedType",
            TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolAttributesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, SymbolAttributesData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        var result = await target.ExecuteAsync(new GetSymbolAttributesRequest
        {
            Symbol = new SymbolSelector(),
            IncludeInherited = false,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Attributes.Items.Should().ContainSingle();
        result.Data.Attributes.Items[0].Inherited.Should().BeFalse();
    }
}
