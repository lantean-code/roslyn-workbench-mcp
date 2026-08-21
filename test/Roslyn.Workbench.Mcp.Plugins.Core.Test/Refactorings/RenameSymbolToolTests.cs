namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

public sealed class RenameSymbolToolTests
{
    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult.Rejected<MutationCandidate>(new PluginExecutionError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });

        var contextMocks = MutationContextMockHelper.Create();
        var request = new RenameSymbolRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Symbol = new SymbolSelector(),
            NewName = "NewName",
        };

        var target = new RenameSymbolTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationCandidate>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(ToolResolutionResult.Rejected<ISymbol, MutationCandidate>(expected));

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Should().BeEquivalentTo(expected);
        contextMocks.MutationContext.VerifyGet(item => item.CurrentSolution, Times.Never);
    }

    [Fact]
    public async Task GIVEN_NewNameIsWhitespace_WHEN_CallingExecuteAsync_THEN_ShouldReturnInvalidRequestResult()
    {
        var contextMocks = MutationContextMockHelper.Create();
        var request = new RenameSymbolRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Symbol = new SymbolSelector(),
            NewName = " ",
        };

        var symbol = new Mock<ISymbol>();
        var target = new RenameSymbolTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationCandidate>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, MutationCandidate>(symbol.Object));

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new PluginExecutionError
        {
            Code = "InvalidRequest",
            Message = "A newName value is required.",
        });

        contextMocks.MutationContext.VerifyGet(item => item.CurrentSolution, Times.Never);
    }

    [Fact]
    public async Task GIVEN_NewNameMatchesCurrentName_WHEN_CallingExecuteAsync_THEN_ShouldReturnNoChangeResult()
    {
        var contextMocks = MutationContextMockHelper.Create();
        var request = new RenameSymbolRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Symbol = new SymbolSelector(),
            NewName = "ExistingName",
        };

        var symbol = new Mock<ISymbol>();
        var target = new RenameSymbolTool();

        symbol
            .SetupGet(item => item.Name)
            .Returns("ExistingName");

        contextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationCandidate>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, MutationCandidate>(symbol.Object));

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.NoChange);
        result.Data.Should().BeNull();
        contextMocks.MutationContext.VerifyGet(item => item.CurrentSolution, Times.Never);
    }

    [Fact]
    public async Task GIVEN_RenameChangesSolution_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationCandidate()
    {
        using var document = RoslynTestFactory.CreateDocument("public sealed class ExistingName { }", "ExistingName.cs");
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "ExistingName",
            CancellationToken.None);

        var contextMocks = MutationContextMockHelper.Create();
        var request = new RenameSymbolRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Symbol = new SymbolSelector(),
            NewName = "UpdatedName",
            RenameOverloads = true,
            RenameFile = true,
        };

        var target = new RenameSymbolTool();

        contextMocks.MutationContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        contextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationCandidate>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, MutationCandidate>(symbol));

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(PluginExecutionOutcome.Succeeded);
        result.Data.Should().NotBeNull();
        result.Data!.CandidateSolution.Should().NotBeSameAs(document.Solution);
        result.Data.Summary.Should().Be("Rename 'ExistingName' to 'UpdatedName'.");

        var renamedDocument = result.Data.CandidateSolution.GetDocument(document.Document.Id);
        var documentDirectory = Path.GetDirectoryName(document.Document.FilePath)
            ?? throw new InvalidOperationException("The source document does not have a parent directory.");

        var expectedPath = Path.Combine(documentDirectory, "UpdatedName.cs");
        renamedDocument!.Name.Should().Be("UpdatedName.cs");
        renamedDocument.FilePath.Should().Be(expectedPath);
    }

    [Fact]
    public async Task GIVEN_FileRenameChangesReferenceDocumentText_WHEN_CallingExecuteAsync_THEN_ShouldRetainReferenceDocumentPath()
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
                        Name = "ExistingName.cs",
                        Source = "public sealed class ExistingName { }",
                    },
                    new InMemoryRoslynDocumentDefinition
                    {
                        Name = "Consumer.cs",
                        Source = "public sealed class Consumer { public ExistingName Value { get; } = new(); }",
                    },
                ],
            },
        ]);

        var declarationDocument = solution.GetDocument("ExistingName.cs");
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            declarationDocument,
            "ExistingName",
            CancellationToken.None);

        var contextMocks = MutationContextMockHelper.Create();
        var request = new RenameSymbolRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Symbol = new SymbolSelector(),
            NewName = "UpdatedName",
            RenameFile = true,
        };

        contextMocks.MutationContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(solution.Solution);

        contextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationCandidate>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, MutationCandidate>(symbol));

        var target = new RenameSymbolTool();
        var result = await target.ExecuteAsync(
            request,
            contextMocks.MutationContext.Object,
            CancellationToken.None);

        var candidateSolution = result.Data?.CandidateSolution
            ?? throw new InvalidOperationException("The candidate solution was not returned.");

        var declaration = candidateSolution.GetDocument(declarationDocument.Id)
            ?? throw new InvalidOperationException("The renamed declaration document was not found.");

        var consumerDocument = solution.GetDocument("Consumer.cs");
        var consumer = candidateSolution.GetDocument(consumerDocument.Id)
            ?? throw new InvalidOperationException("The consumer document was not found.");

        var consumerText = await consumer.GetTextAsync(CancellationToken.None);
        var declarationDirectory = Path.GetDirectoryName(declarationDocument.FilePath)
            ?? throw new InvalidOperationException("The declaration document does not have a parent directory.");

        var expectedDeclarationPath = Path.Combine(declarationDirectory, "UpdatedName.cs");
        declaration.FilePath.Should().Be(expectedDeclarationPath);
        consumer.FilePath.Should().Be(consumerDocument.FilePath);
        consumerText.ToString().Should().Contain("UpdatedName Value");
    }

    [Fact]
    public async Task GIVEN_RenameInStringsIsEnabled_WHEN_CallingExecuteAsync_THEN_ShouldRenameMatchingStringTextOnly()
    {
        const string source = """
            public sealed class ExistingName
            {
                // ExistingName is referenced in this comment.
                public const string Description = "ExistingName is referenced in this string.";
            }
            """;

        using var document = RoslynTestFactory.CreateDocument(source, "ExistingName.cs");
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "ExistingName",
            CancellationToken.None);

        var contextMocks = MutationContextMockHelper.Create();
        var request = new RenameSymbolRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Symbol = new SymbolSelector(),
            NewName = "UpdatedName",
            RenameInStrings = true,
        };

        var target = new RenameSymbolTool();

        contextMocks.MutationContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        contextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationCandidate>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, MutationCandidate>(symbol));

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        var candidateDocument = result.Data!.CandidateSolution.GetDocument(document.Document.Id);
        var candidateText = await candidateDocument!.GetTextAsync(CancellationToken.None);

        candidateText.ToString().Should().Contain("ExistingName is referenced in this comment.");
        candidateText.ToString().Should().Contain("UpdatedName is referenced in this string.");
    }

    [Fact]
    public async Task GIVEN_RenameInCommentsIsEnabled_WHEN_CallingExecuteAsync_THEN_ShouldRenameMatchingCommentTextOnly()
    {
        const string source = """
            public sealed class ExistingName
            {
                // ExistingName is referenced in this comment.
                public const string Description = "ExistingName is referenced in this string.";
            }
            """;

        using var document = RoslynTestFactory.CreateDocument(source, "ExistingName.cs");
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "ExistingName",
            CancellationToken.None);

        var contextMocks = MutationContextMockHelper.Create();
        var request = new RenameSymbolRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Symbol = new SymbolSelector(),
            NewName = "UpdatedName",
            RenameInComments = true,
        };

        var target = new RenameSymbolTool();

        contextMocks.MutationContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        contextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationCandidate>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, MutationCandidate>(symbol));

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        var candidateDocument = result.Data!.CandidateSolution.GetDocument(document.Document.Id);
        var candidateText = await candidateDocument!.GetTextAsync(CancellationToken.None);

        candidateText.ToString().Should().Contain("UpdatedName is referenced in this comment.");
        candidateText.ToString().Should().Contain("ExistingName is referenced in this string.");
    }

    [Fact]
    public async Task GIVEN_RenameOverloadsIsEnabled_WHEN_CallingExecuteAsync_THEN_ShouldRenameEveryOverload()
    {
        const string source = """
            public sealed class Sample
            {
                public void ExistingName()
                {
                }

                public void ExistingName(int value)
                {
                }
            }
            """;

        using var document = RoslynTestFactory.CreateDocument(source);
        var compilation = await document.Document.Project.GetCompilationAsync(CancellationToken.None);
        var type = compilation!.GetTypeByMetadataName("Sample");
        var symbol = type!.GetMembers("ExistingName").Single(static member => member is IMethodSymbol method && method.Parameters.Length == 0);
        var contextMocks = MutationContextMockHelper.Create();
        var request = new RenameSymbolRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Symbol = new SymbolSelector(),
            NewName = "UpdatedName",
            RenameOverloads = true,
        };

        var target = new RenameSymbolTool();

        contextMocks.MutationContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        contextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationCandidate>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, MutationCandidate>(symbol));

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        var candidateDocument = result.Data!.CandidateSolution.GetDocument(document.Document.Id);
        var candidateText = await candidateDocument!.GetTextAsync(CancellationToken.None);
        var updatedSource = candidateText.ToString();

        updatedSource.Should().Contain("void UpdatedName()");
        updatedSource.Should().Contain("void UpdatedName(int value)");
        updatedSource.Should().NotContain("void ExistingName");
    }

    [Fact]
    public async Task GIVEN_InterfaceMember_WHEN_CallingExecuteAsync_THEN_ShouldRenameItsImplementation()
    {
        const string source = """
            public interface IContract
            {
                void ExistingName();
            }

            public sealed class Implementation : IContract
            {
                public void ExistingName()
                {
                }
            }
            """;

        using var document = RoslynTestFactory.CreateDocument(source);
        var compilation = await document.Document.Project.GetCompilationAsync(CancellationToken.None);
        var contract = compilation!.GetTypeByMetadataName("IContract");
        var symbol = contract!.GetMembers("ExistingName").Single();
        var contextMocks = MutationContextMockHelper.Create();
        var request = new RenameSymbolRequest
        {
            ExpectedSnapshot = WorkspaceSnapshotTestFactory.CreatePrecondition(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            Symbol = new SymbolSelector(),
            NewName = "UpdatedName",
        };

        var target = new RenameSymbolTool();

        contextMocks.MutationContext
            .SetupGet(item => item.CurrentSolution)
            .Returns(document.Solution);

        contextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationCandidate>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(ToolResolutionResult.Resolved<ISymbol, MutationCandidate>(symbol));

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        var candidateDocument = result.Data!.CandidateSolution.GetDocument(document.Document.Id);
        var candidateText = await candidateDocument!.GetTextAsync(CancellationToken.None);
        var sourceText = candidateText.ToString();

        sourceText.Should().Contain("void UpdatedName();");
        sourceText.Should().Contain("public void UpdatedName()");
    }
}
