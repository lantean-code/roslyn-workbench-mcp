using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class FindCalleesToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterQueryTool()
    {
        var registry = new Mock<IPluginRegistry>();

        FindCalleesTool.Register(registry.Object);

        registry.Verify(item => item.RegisterQueryTool<FindCalleesRequest, CalleeSearchData>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "find-callees"
                && metadata.Title == "Find Callees"
                && metadata.Description == "Returns symbols directly invoked by a method or selected executable body."),
            It.IsAny<IQueryToolHandler<FindCalleesRequest, CalleeSearchData>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_SymbolAndLocationAreBothMissing_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        var result = await target.ExecuteAsync(new FindCalleesRequest(), queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "InvalidRequest",
            Message = "Specify exactly one of symbol or location.",
        });
        queryContextMocks.RequestResolver.Verify(item => item.ResolveSymbolAsync<CalleeSearchData>(
            It.IsAny<SymbolSelector?>(),
            It.IsAny<SnapshotPrecondition?>(),
            It.IsAny<IToolExecutionContext>(),
            It.IsAny<CancellationToken>()), Times.Never);
        queryContextMocks.WorkspaceResolver.Verify(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_SymbolResolutionHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<CalleeSearchData>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CalleeSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CalleeSearchData>
            {
                Rejection = expected,
            });

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task GIVEN_SymbolDeclarationIsOutsideCurrentSolution_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        using var symbolDocument = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                }
            }
            """);
        using var emptyWorkspace = new AdhocWorkspace();

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            symbolDocument.Document,
            "Run",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(emptyWorkspace.CurrentSolution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CalleeSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CalleeSearchData>
            {
                Value = symbol,
            });

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "InvalidRequest",
            Message = "The selected symbol does not have an executable source body.",
        });
    }

    [Fact]
    public async Task GIVEN_SymbolDoesNotHaveExecutableSourceBody_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                }
            }
            """);

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "Formatter",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CalleeSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CalleeSearchData>
            {
                Value = symbol,
            });

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "InvalidRequest",
            Message = "The selected symbol does not have an executable source body.",
        });
    }

    [Fact]
    public async Task GIVEN_LocationAndValidateSnapshotReturnsConflict_WHEN_CallingExecuteAsync_THEN_ShouldReturnConflictResult()
    {
        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var expected = PluginExecutionResult<CalleeSearchData>.Conflict(new ToolError
        {
            Code = "SnapshotMismatch",
            Message = "SnapshotMismatch",
        });

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CalleeSearchData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns(expected);

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Location = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Should().BeEquivalentTo(expected);
        queryContextMocks.WorkspaceResolver.Verify(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GIVEN_LocationAndResolveLocationReturnsNotFound_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();

        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CalleeSearchData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<CalleeSearchData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.NotFound());

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "LocationNotFound",
            Message = "The location selector did not match any result.",
        });
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_LocationAndCurrentSolutionDoesNotContainResolvedDocument_WHEN_CallingExecuteAsync_THEN_ShouldReturnLocationNotFoundResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    First();
                }

                void First()
                {
                }
            }
            """);
        using var emptyWorkspace = new AdhocWorkspace();

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<BlockSyntax>(item => item.Parent is MethodDeclarationSyntax method && method.Identifier.ValueText == "Run");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(emptyWorkspace.CurrentSolution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CalleeSearchData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<CalleeSearchData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "LocationNotFound",
            Message = "The location selector did not resolve to a source document.",
        });
        result.RequiredAction.Should().Be(RequiredAction.ResolveTargetAgain);
    }

    [Fact]
    public async Task GIVEN_LocationDoesNotResolveToExecutableCode_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    First();
                }

                void First()
                {
                }
            }
            """);

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<ClassDeclarationSyntax>(item => item.Identifier.ValueText == "Formatter");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CalleeSearchData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<CalleeSearchData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "InvalidRequest",
            Message = "The selected location does not resolve to executable code.",
        });
    }

    [Fact]
    public async Task GIVEN_LocationResolvesToExecutableBody_WHEN_CallingExecuteAsync_THEN_ShouldReturnDirectCallees()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    First();
                    var created = new Created();
                }

                void First()
                {
                }

                private sealed class Created
                {
                }
            }
            """);

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var selectedLocation = document.GetSingleNodeLocation<BlockSyntax>(item => item.Parent is MethodDeclarationSyntax method && method.Identifier.ValueText == "Run");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ValidateSnapshot<CalleeSearchData>(
                queryContextMocks.QueryContext.Object,
                It.IsAny<SnapshotPrecondition?>()))
            .Returns((PluginExecutionResult<CalleeSearchData>?)null);
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.ResolveLocationAsync(It.IsAny<LocationSelector>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(SelectorResolveResult<Location>.Resolved(selectedLocation));
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Location = new LocationSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Source!.DisplayName.Should().Be("Run");
        result.Data.Callees.Items.Select(item => item.DisplayName).Should().Equal(".ctor", "First");
        result.Data.Callees.HasMore.Should().BeFalse();
    }

    [Fact]
    public async Task GIVEN_SymbolAndIncludeIndirectIsTrue_WHEN_CallingExecuteAsync_THEN_ShouldReturnExpandedCallees()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                void Run()
                {
                    First();
                    var created = new Created();
                }

                void First()
                {
                    Second();
                }

                void Second()
                {
                }

                private sealed class Created
                {
                }
            }
            """);

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Run",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CalleeSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CalleeSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Symbol = new SymbolSelector(),
            IncludeIndirect = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Source!.DisplayName.Should().Be("Run");
        result.Data.Callees.Items.Select(item => item.DisplayName).Should().Equal(".ctor", "First", "Second");
    }

    [Fact]
    public async Task GIVEN_IndirectCalleeDoesNotHaveExecutableBody_WHEN_CallingExecuteAsync_THEN_ShouldReturnDirectCalleeOnly()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            interface IThing
            {
                void Execute();
            }

            class Formatter
            {
                void Run(IThing thing)
                {
                    thing.Execute();
                }
            }
            """);

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Run",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.QueryContext
            .SetupGet(item => item.DefaultMaxResults)
            .Returns(10);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CalleeSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CalleeSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Symbol = new SymbolSelector(),
            IncludeIndirect = true,
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Callees.Items.Select(item => item.DisplayName).Should().Equal("Execute");
    }

    [Fact]
    public async Task GIVEN_SymbolUsesExpressionBodiedMethod_WHEN_CallingExecuteAsync_THEN_ShouldReturnCallees()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Expression(int value) => Transform(value);

                int Transform(int value)
                {
                    return value;
                }
            }
            """);

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredMethodSymbolAsync(
            document.Document,
            "Expression",
            null,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CalleeSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CalleeSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
    }

    [Fact]
    public async Task GIVEN_SymbolUsesBlockBodiedLocalFunction_WHEN_CallingExecuteAsync_THEN_ShouldReturnCallees()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Host()
                {
                    int Local(int value)
                    {
                        return Transform(value);
                    }

                    return Local(1);
                }

                int Transform(int value)
                {
                    return value;
                }
            }
            """);

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredLocalFunctionSymbolAsync(
            document.Document,
            "Local",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CalleeSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CalleeSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
    }

    [Fact]
    public async Task GIVEN_SymbolUsesExpressionBodiedLocalFunction_WHEN_CallingExecuteAsync_THEN_ShouldReturnCallees()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Host()
                {
                    int Local(int value) => Transform(value);
                    return Local(1);
                }

                int Transform(int value)
                {
                    return value;
                }
            }
            """);

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredLocalFunctionSymbolAsync(
            document.Document,
            "Local",
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CalleeSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CalleeSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
    }

    [Fact]
    public async Task GIVEN_SymbolUsesBlockBodiedAccessor_WHEN_CallingExecuteAsync_THEN_ShouldReturnCallees()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Value
                {
                    get
                    {
                        return Transform(0);
                    }
                }

                int Transform(int value)
                {
                    return value;
                }
            }
            """);

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await GetAccessorSymbolAsync(document.Document, "Value");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CalleeSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CalleeSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
    }

    [Fact]
    public async Task GIVEN_SymbolUsesExpressionBodiedAccessor_WHEN_CallingExecuteAsync_THEN_ShouldReturnCallees()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            class Formatter
            {
                int Value
                {
                    get => Transform(0);
                }

                int Transform(int value)
                {
                    return value;
                }
            }
            """);

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await GetAccessorSymbolAsync(document.Document, "Value");

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CalleeSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CalleeSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
    }

    [Fact]
    public async Task GIVEN_SymbolUsesAnonymousFunctionBody_WHEN_CallingExecuteAsync_THEN_ShouldReturnCallees()
    {
        using var document = RoslynTestFactory.CreateDocument("""
            using System;

            class Formatter
            {
                void Host()
                {
                    Func<int, int> local = value => Transform(value);
                    local(1);
                }

                int Transform(int value)
                {
                    return value;
                }
            }
            """);

        var target = new FindCalleesTool();
        var queryContextMocks = QueryContextMockHelper.Create();
        var symbol = await RoslynDocumentTestHelper.GetRequiredAnonymousFunctionSymbolAsync(
            document.Document,
            TestContext.Current.CancellationToken);

        queryContextMocks.QueryContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);
        queryContextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<CalleeSearchData>(
                It.IsAny<SymbolSelector?>(),
                It.IsAny<SnapshotPrecondition?>(),
                queryContextMocks.QueryContext.Object,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, CalleeSearchData>
            {
                Value = symbol,
            });
        queryContextMocks.WorkspaceResolver
            .Setup(item => item.CreateSymbolReference(It.IsAny<ISymbol>()))
            .Returns<ISymbol>(item => string.IsNullOrWhiteSpace(item.Name)
                ? new SymbolReference
                {
                    DisplayName = "Anonymous",
                    Kind = item.Kind.ToString(),
                }
                : SelectorTestFactory.CreateSymbolReference(item));

        var result = await target.ExecuteAsync(new FindCalleesRequest
        {
            Symbol = new SymbolSelector(),
        }, queryContextMocks.QueryContext.Object, TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
    }
    private static async Task<IMethodSymbol> GetAccessorSymbolAsync(Document document, string propertyName)
    {
        var syntaxRoot = await document.GetSyntaxRootAsync(TestContext.Current.CancellationToken);
        var semanticModel = await document.GetSemanticModelAsync(TestContext.Current.CancellationToken);
        var accessor = syntaxRoot!.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .Single(item => item.Identifier.ValueText == propertyName)
            .AccessorList!
            .Accessors
            .Single();

        return (IMethodSymbol)(semanticModel!.GetDeclaredSymbol(accessor, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException($"The accessor for '{propertyName}' could not be resolved."));
    }
}
