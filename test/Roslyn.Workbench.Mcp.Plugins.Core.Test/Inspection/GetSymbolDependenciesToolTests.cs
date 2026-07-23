namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetSymbolDependenciesToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new GetSymbolDependenciesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<SymbolDependenciesData>.Rejected(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Rejected(expected));

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_MethodSymbolHasForeignDeclaringSyntaxReference_WHEN_CallingExecuteAsync_THEN_ShouldSkipMissingCurrentSolutionDocument()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            public class Formatter
            {
                public string Format(string value)
                {
                    return value;
                }
            }
            """);

        using var foreignDocument = RoslynTestFactory.CreateDocument("""
            public class ForeignFormatter
            {
                public string Format(string value)
                {
                    return value;
                }
            }
            """);

        var target = new GetSymbolDependenciesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            foreignDocument.Document,
            "Format",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("String");
    }

    [Fact]
    public async Task GIVEN_MethodSymbolUsesDirectDependenciesAndIncludeAssembliesIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnOrderedDependenciesWithoutSelfReference()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            public class Dependency
            {
                public int Field;

                public int Property
                {
                    get;
                    set;
                }

                public event EventHandler? Changed;

                public void Helper()
                {
                }
            }

            public class Formatter
            {
                private readonly Dependency _dependency = new();

                public string Format(string value)
                {
                    Action callback = Format;
                    _dependency.Helper();
                    var dependency = new Dependency();
                    var field = dependency.Field;
                    var property = dependency.Property;
                    var changed = dependency.Changed;
                    callback();
                    return value.ToUpperInvariant();
                }
            }
            """);

        var target = new GetSymbolDependenciesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Format",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(20);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
            IncludeAssemblies = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Dependency");
        result.Data.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Helper");
        result.Data.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Field");
        result.Data.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Property");
        result.Data.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Changed");
        result.Data.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("ToUpperInvariant");
        result.Data.Dependencies.Items.Should().NotContain(item => item.Symbol!.DisplayName == "Format");
        result.Data.Dependencies.Items.Should().OnlyContain(item => item.AssemblyName != null);

        var boundedResult = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
            IncludeAssemblies = true,
            DependenciesLimit = 1,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        boundedResult.Data!.Dependencies.Items.Should().ContainSingle();
        boundedResult.Data.Dependencies.HasMore.Should().BeTrue();
    }

    [Fact]
    public async Task GIVEN_PropertySymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnSignatureDependencies()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            public class Dependency
            {
            }

            public class Formatter
            {
                public Dependency Current
                {
                    get;
                } = new Dependency();
            }
            """);

        var target = new GetSymbolDependenciesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredPropertySymbolAsync(
            document.Document,
            "Current",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
            IncludeAssemblies = false,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Dependencies.Items.Should().Contain(item => item.Symbol!.DisplayName == "Dependency" && item.AssemblyName == null);
    }

    [Fact]
    public async Task GIVEN_FieldSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnSignatureDependencies()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            public class Dependency
            {
            }

            public class Formatter
            {
                public Dependency _dependency = new();
            }
            """);

        var target = new GetSymbolDependenciesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Formatter",
            TestContext.Current.CancellationToken);

        var field = symbol.GetMembers().OfType<IFieldSymbol>().Single(item => item.Name == "_dependency");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Resolved(field));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Dependencies.Items.Should().ContainSingle(item => item.Symbol!.DisplayName == "Dependency");
    }

    [Fact]
    public async Task GIVEN_NamedTypeSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnBaseTypeAndInterfaceDependencies()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            public interface IContract
            {
            }

            public class BaseType
            {
            }

            public class Formatter : BaseType, IContract
            {
            }
            """);

        var target = new GetSymbolDependenciesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("BaseType");
        result.Data.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("IContract");
    }

    [Fact]
    public async Task GIVEN_ExpressionBodiedMethodSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnMethodReferenceDependencies()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            public class Dependency
            {
                public string Helper()
                {
                    return string.Empty;
                }
            }

            public class Formatter
            {
                private readonly Dependency _dependency = new();

                public Func<string> Format() => _dependency.Helper;
            }
            """);

        var target = new GetSymbolDependenciesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Format",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Helper");
        result.Data.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Func");
    }

    [Fact]
    public async Task GIVEN_BlockBodiedLocalFunctionSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocalFunctionDependencies()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            public class Dependency
            {
                public string Format(string value)
                {
                    return value.Trim();
                }
            }

            public class Formatter
            {
                public string Run(string value)
                {
                    string Local(string text)
                    {
                        return new Dependency().Format(text);
                    }

                    return Local(value);
                }
            }
            """);

        var target = new GetSymbolDependenciesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredLocalFunctionSymbolAsync(
            document.Document,
            "Local",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Dependency");
        result.Data.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Format");
    }

    [Fact]
    public async Task GIVEN_ExpressionBodiedLocalFunctionSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocalFunctionDependencies()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            public class Formatter
            {
                public string Run(string value)
                {
                    string Local(string text) => text.Trim();
                    return Local(value);
                }
            }
            """);

        var target = new GetSymbolDependenciesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredLocalFunctionSymbolAsync(
            document.Document,
            "Local",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Trim");
    }

    [Fact]
    public async Task GIVEN_AccessorSymbols_WHEN_CallingExecuteAsync_THEN_ShouldReturnAccessorDependencies()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            public class Dependency
            {
                public int Value
                {
                    get;
                    set;
                }
            }

            public class Formatter
            {
                private readonly Dependency _dependency = new();

                public int BlockValue
                {
                    get
                    {
                        return _dependency.Value;
                    }
                }

                public int ExpressionValue
                {
                    get => _dependency.Value;
                }
            }
            """);

        var target = new GetSymbolDependenciesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var containingType = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Formatter",
            TestContext.Current.CancellationToken);

        var blockGetter = containingType.GetMembers().OfType<IPropertySymbol>().Single(item => item.Name == "BlockValue").GetMethod!;
        var expressionGetter = containingType.GetMembers().OfType<IPropertySymbol>().Single(item => item.Name == "ExpressionValue").GetMethod!;

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Resolved(blockGetter));

        var blockResult = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Resolved(expressionGetter));

        var expressionResult = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        blockResult.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        blockResult.Data!.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Value");
        expressionResult.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        expressionResult.Data!.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Value");
    }

    [Fact]
    public async Task GIVEN_AnonymousFunctionSymbol_WHEN_CallingExecuteAsync_THEN_ShouldReturnAnonymousFunctionDependencies()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            public class Formatter
            {
                public void Run()
                {
                    Func<string, string> formatter = value => value.Trim();
                    _ = formatter("value");
                }
            }
            """);

        var target = new GetSymbolDependenciesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredAnonymousFunctionSymbolAsync(
            document.Document,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<SymbolDependenciesData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ToolResolutionResult<ISymbol, SymbolDependenciesData>.Resolved(symbol));

        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => string.IsNullOrWhiteSpace(item.Name)
                ? SelectorTestFactory.CreateSymbolReference(
                    "AnonymousFunction",
                    item.Kind,
                    item.GetDocumentationCommentId())
                : SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new GetSymbolDependenciesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data!.Dependencies.Items.Select(item => item.Symbol!.DisplayName).Should().Contain("Trim");
    }

}
