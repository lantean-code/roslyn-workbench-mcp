namespace Roslyn.Workbench.Mcp.CodeActions.Test.Composition;

public sealed class CodeActionMutationProvenanceFactoryTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    public void GIVEN_OrdinaryAction_WHEN_CreatingProvenance_THEN_ShouldRetainDeterministicAuditFields(
        int actionKindValue,
        int expectedKindValue)
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var actionKind = (DiscoveredActionKind)actionKindValue;
        var expectedKind = (CodeActionMutationKind)expectedKindValue;
        var provider = CodeActionExecutionTestFactory.CreateProviderIdentity();
        var action = CreateAction(actionKind, roslyn.Solution);

        var result = CodeActionMutationProvenanceFactory.Create(action, provider);

        result.Kind.Should().Be(expectedKind);
        result.Provider.Should().BeSameAs(provider);
        result.FixAllProvider.Should().BeNull();
        result.DiagnosticIds.Should().Equal("A001", "Z001");
        result.EquivalenceKey.Should().Be("EquivalenceKey");
        result.FixAllScope.Should().BeNull();
    }

    [Fact]
    public void GIVEN_FixAllAction_WHEN_CreatingProvenance_THEN_ShouldRetainBothProvidersAndScope()
    {
        using var roslyn = RoslynTestFactory.CreateDocument("class C { }");
        var provider = CodeActionExecutionTestFactory.CreateProviderIdentity();
        var fixAllProvider = provider with { TypeName = "FixAll.Provider" };

        var result = CodeActionMutationProvenanceFactory.CreateFixAll(
            CreateAction(DiscoveredActionKind.CodeFix, roslyn.Solution),
            provider,
            fixAllProvider,
            CodeActionFixAllScope.Solution);

        result.Kind.Should().Be(CodeActionMutationKind.FixAll);
        result.Provider.Should().BeSameAs(provider);
        result.FixAllProvider.Should().BeSameAs(fixAllProvider);
        result.FixAllScope.Should().Be(nameof(CodeActionFixAllScope.Solution));
    }

    private static DiscoveredCodeAction CreateAction(
        DiscoveredActionKind kind,
        Solution solution)
    {
        return new DiscoveredCodeAction
        {
            Action = Microsoft.CodeAnalysis.CodeActions.CodeAction.Create("Title", _ => Task.FromResult(solution)),
            Kind = kind,
            ProviderId = "ProviderId",
            Title = "Title",
            TargetSpan = default,
            EquivalenceKey = "EquivalenceKey",
            DiagnosticIds = ["Z001", "A001", "Z001"],
        };
    }
}
