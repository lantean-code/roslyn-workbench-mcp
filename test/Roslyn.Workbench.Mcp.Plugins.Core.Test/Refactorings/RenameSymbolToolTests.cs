namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Refactorings;

public sealed class RenameSymbolToolTests
{
    [Fact]
    public void GIVEN_PluginRegistry_WHEN_CallingRegister_THEN_ShouldRegisterMutationTool()
    {
        var registry = new Mock<IPluginRegistry>();

        RenameSymbolTool.Register(registry.Object);

        registry.Verify(item => item.RegisterMutationTool<RenameSymbolRequest>(
            It.Is<ToolRegistrationMetadata>(metadata =>
                metadata.Name == "rename-symbol"
                && metadata.Title == "Rename Symbol"
                && metadata.Description == "Stages a symbol rename across the effective solution."
                && metadata.Behavior.Destructive),
            It.IsAny<IMutationToolHandler<RenameSymbolRequest>>()), Times.Once);
    }

    [Fact]
    public async Task GIVEN_ResolveSymbolHasRejection_WHEN_CallingExecuteAsync_THEN_ShouldReturnRejectionResult()
    {
        var expected = PluginExecutionResult<MutationProposal>.Rejected(new ToolError
        {
            Code = "SymbolNotFound",
            Message = "SymbolNotFound",
        });
        var contextMocks = MutationContextMockHelper.Create();
        var request = new RenameSymbolRequest
        {
            Symbol = new SymbolSelector(),
            NewName = "NewName",
        };
        var target = new RenameSymbolTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Rejection = expected,
            });

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
            Symbol = new SymbolSelector(),
            NewName = " ",
        };
        var target = new RenameSymbolTool();

        contextMocks.RequestResolver
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Value = Mock.Of<ISymbol>(),
            });

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error.Should().BeEquivalentTo(new ToolError
        {
            Code = "InvalidRequest",
            Message = "A newName value is required.",
        });
        contextMocks.MutationContext.VerifyGet(item => item.CurrentSolution, Times.Never);
    }

    [Fact]
    public async Task GIVEN_RenameChangesSolution_WHEN_CallingExecuteAsync_THEN_ShouldReturnMutationProposal()
    {
        using var document = RoslynTestFactory.CreateDocument("public sealed class ExistingName { }", "ExistingName.cs");
        var symbol = await RoslynDocumentTestHelper.GetRequiredNamedTypeSymbolAsync(
            document.Document,
            "ExistingName",
            CancellationToken.None);
        var contextMocks = MutationContextMockHelper.Create();
        var request = new RenameSymbolRequest
        {
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
            .Setup(item => item.ResolveSymbolAsync<MutationProposal>(
                request.Symbol,
                request.ExpectedSnapshot,
                contextMocks.MutationContext.Object,
                CancellationToken.None))
            .ReturnsAsync(new ToolResolutionResult<ISymbol, MutationProposal>
            {
                Value = symbol,
            });

        var result = await target.ExecuteAsync(request, contextMocks.MutationContext.Object, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data.Should().NotBeNull();
        result.Data!.CandidateSolution.Should().NotBeSameAs(document.Solution);
        result.Data.Summary.Should().Be("Rename 'ExistingName' to 'UpdatedName'.");
    }
}
