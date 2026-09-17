namespace Roslyn.Workbench.Mcp.Test.Contracts.Transactions;

public sealed class TransactionMutationProvenanceMapperTests
{
    [Fact]
    public void GIVEN_NonCodeActionProvenance_WHEN_Mapping_THEN_ShouldOmitProviderDictionary()
    {
        var provenance = CreateTransactionProvenance(codeAction: null);

        var projection = TransactionMutationProvenanceMapper.Create([provenance]);

        projection.Providers.Should().BeNull();
        projection.Provenance.Should().ContainSingle().Which.Should().BeEquivalentTo(
            new TransactionMutationProvenanceData
            {
                Revision = 1,
                Operation = "Operation",
                Summary = "Summary",
            });
    }

    [Fact]
    public void GIVEN_CodeActionAndFixAllProviders_WHEN_Mapping_THEN_ShouldAssignFirstSeenReferencesAndDeduplicateProviders()
    {
        var provider = CreateProvider("B.Provider");
        var fixAllProvider = CreateProvider("A.FixAllProvider");
        var ordinaryCodeAction = new CodeActionMutationProvenance
        {
            Kind = CodeActionMutationKind.CodeFix,
            Provider = provider,
        };
        var fixAllCodeAction = new CodeActionMutationProvenance
        {
            Kind = CodeActionMutationKind.FixAll,
            Provider = provider,
            FixAllProvider = fixAllProvider,
        };

        var projection = TransactionMutationProvenanceMapper.Create(
            [
                CreateTransactionProvenance(ordinaryCodeAction),
                CreateTransactionProvenance(codeAction: null),
                CreateTransactionProvenance(fixAllCodeAction),
            ]);

        projection.Providers.Should().HaveCount(2);
        projection.Providers!["p1"].Should().BeSameAs(provider);
        projection.Providers["p2"].Should().BeSameAs(fixAllProvider);
        projection.Provenance[0].CodeAction.Should().BeEquivalentTo(new CodeActionMutationProvenanceData
        {
            Kind = CodeActionMutationKind.CodeFix,
            ProviderId = "p1",
        });
        projection.Provenance[1].CodeAction.Should().BeNull();
        projection.Provenance[2].CodeAction.Should().BeEquivalentTo(new CodeActionMutationProvenanceData
        {
            Kind = CodeActionMutationKind.FixAll,
            ProviderId = "p1",
            FixAllProviderId = "p2",
        });
    }

    [Fact]
    public void GIVEN_EqualProviderValues_WHEN_Mapping_THEN_ShouldReuseFirstProviderReference()
    {
        var firstProvider = CreateProvider("Provider");
        var equalProvider = CreateProvider("Provider");
        var firstCodeAction = new CodeActionMutationProvenance
        {
            Kind = CodeActionMutationKind.CodeFix,
            Provider = firstProvider,
        };
        var secondCodeAction = new CodeActionMutationProvenance
        {
            Kind = CodeActionMutationKind.Refactoring,
            Provider = equalProvider,
        };

        var projection = TransactionMutationProvenanceMapper.Create(
            [
                CreateTransactionProvenance(firstCodeAction),
                CreateTransactionProvenance(secondCodeAction),
            ]);

        projection.Providers.Should().ContainSingle();
        projection.Providers!["p1"].Should().BeSameAs(firstProvider);
        projection.Provenance.Select(static item => item.CodeAction!.ProviderId).Should().Equal("p1", "p1");
    }

    private static TransactionMutationProvenance CreateTransactionProvenance(
        CodeActionMutationProvenance? codeAction)
    {
        return new TransactionMutationProvenance
        {
            Revision = 1,
            Operation = "Operation",
            Summary = "Summary",
            CodeAction = codeAction,
        };
    }

    private static MutationProviderIdentity CreateProvider(string typeName)
    {
        return new MutationProviderIdentity
        {
            TypeName = typeName,
            AssemblyName = "AssemblyName",
            AssemblyVersion = "AssemblyVersion",
        };
    }
}
