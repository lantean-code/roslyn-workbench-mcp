namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetApiSurfaceToolTests
{
    [Fact]
    public async Task GIVEN_ResolveDocumentsHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetApiSurfaceTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<ApiSurfaceData>.Rejected(new PluginExecutionError
        {
            Code = "DocumentNotFound",
            Message = "DocumentNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ApiSurfaceData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, ApiSurfaceData>.Rejected(expected));

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_MinimumAccessibilityIsInvalid_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new GetApiSurfaceTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ApiSurfaceData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, ApiSurfaceData>.Resolved([]));

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest
        {
            MinimumAccessibility = "Private",
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "Minimum accessibility must be Public, Protected, or Internal.",
        });
    }

    [Fact]
    public async Task GIVEN_DocumentDoesNotProvideSemanticData_WHEN_CallingExecuteAsync_THEN_ShouldSkipDocument()
    {
        using var unsupportedDocument = RoslynTestFactory.CreateUnsupportedDocument();

        var target = new GetApiSurfaceTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ApiSurfaceData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, ApiSurfaceData>.Resolved([unsupportedDocument.Document]));

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_ObsoleteSymbolsAndMinimumAccessibilityProtected_WHEN_CallingExecuteAsync_THEN_ShouldReturnEligibleNonObsoleteApiSymbols()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            public delegate void PublicDelegate();

            public interface IPublicContract
            {
            }

            public class Container
            {
                public int PublicField;

                public int PublicProperty
                {
                    get
                    {
                        return PublicField;
                    }
                }

                public event EventHandler Changed
                {
                    add
                    {
                    }
                    remove
                    {
                    }
                }

                protected void ProtectedMethod()
                {
                    int local = 0;
                }

                internal void InternalMethod()
                {
                }
            }

            [Obsolete]
            public class ObsoleteType
            {
            }
            """);

        var target = new GetApiSurfaceTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(20);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ApiSurfaceData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, ApiSurfaceData>.Resolved([document.Document]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest
        {
            MinimumAccessibility = "Protected",
            IncludeObsolete = false,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Changed");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Container");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("IPublicContract");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("ProtectedMethod");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("PublicDelegate");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("PublicField");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("PublicProperty");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().NotContain("InternalMethod");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().NotContain("ObsoleteType");
    }

    [Fact]
    public async Task GIVEN_IncludeObsoleteIsTrueAndMinimumAccessibilityInternal_WHEN_CallingExecuteAsync_THEN_ShouldIncludeInternalAndObsoleteSymbols()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            [Obsolete]
            public class ObsoleteType
            {
            }

            public class Container
            {
                internal void Hidden()
                {
                }
            }
            """);

        var target = new GetApiSurfaceTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ApiSurfaceData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, ApiSurfaceData>.Resolved([document.Document]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest
        {
            MinimumAccessibility = "Internal",
            IncludeObsolete = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Hidden");
        result.Data.Symbols.Items.Should().Contain(item => item.Symbol!.DisplayName == "ObsoleteType" && item.IsObsolete);
    }

    [Fact]
    public async Task GIVEN_PublicThresholdIncludesAttributedTypeAndDestructor_WHEN_CallingExecuteAsync_THEN_ShouldExcludeNonPublicDeclarations()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            [Serializable]
            public class Decorated
            {
                ~Decorated()
                {
                }

                protected void Hidden()
                {
                }

                public void Visible()
                {
                    int local = 0;
                }
            }

            public class Container
            {
                private class PrivateNested
                {
                }
            }
            """);

        var target = new GetApiSurfaceTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(20);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ApiSurfaceData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, ApiSurfaceData>.Resolved([document.Document]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest
        {
            MinimumAccessibility = "Public",
            IncludeObsolete = false,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Decorated");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Visible");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().NotContain("Hidden");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().NotContain("PrivateNested");
    }

    [Fact]
    public async Task GIVEN_InternalThresholdSeesPrivateDeclaration_WHEN_CallingExecuteAsync_THEN_ShouldExcludePrivateMember()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            public class Container
            {
                private void Hidden()
                {
                }

                private protected void VisibleToFamilyAndAssembly()
                {
                }
            }
            """);

        var target = new GetApiSurfaceTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(20);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ApiSurfaceData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, ApiSurfaceData>.Resolved([document.Document]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest
        {
            MinimumAccessibility = "Internal",
            IncludeObsolete = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("VisibleToFamilyAndAssembly");
        result.Data.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().NotContain("Hidden");
    }

    [Fact]
    public async Task GIVEN_RequestedLimitIsLowerThanExportedCount_WHEN_CallingExecuteAsync_THEN_ShouldReturnBoundedOrderedApiSymbols()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            public class ZType
            {
            }

            public class AType
            {
            }
            """);

        var target = new GetApiSurfaceTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveDocuments<ApiSurfaceData>(
                It.IsAny<ScopeSelector?>(),
                queryContextMocks.QueryContext.Object))
            .Returns(ToolResolutionResult<IReadOnlyList<Document>, ApiSurfaceData>.Resolved([document.Document]));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetApiSurfaceRequest
        {
            SymbolsLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Symbols.Items.Select(item => item.Symbol!.DisplayName).Should().Equal("AType");
        result.Data.Symbols.HasMore.Should().BeTrue();
        queryContextMocks.WorkspaceResolver.Verify(item => item.CreateSymbolReference(It.IsAny<ISymbol>()), Times.Once);
    }
}
