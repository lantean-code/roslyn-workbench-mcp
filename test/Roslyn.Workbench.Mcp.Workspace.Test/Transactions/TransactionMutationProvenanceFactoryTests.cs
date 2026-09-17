namespace Roslyn.Workbench.Mcp.Workspace.Test.Transactions;

public sealed class TransactionMutationProvenanceFactoryTests : IDisposable
{
    private readonly AdhocWorkspace _workspace = new();

    [Fact]
    public void GIVEN_TransactionHistory_WHEN_CreatingProvenance_THEN_ShouldProjectOnlyActiveRevisions()
    {
        var provider = new MutationProviderIdentity
        {
            TypeName = "Provider.Type",
            AssemblyName = "Provider.Assembly",
            AssemblyVersion = "1.0.0.0",
        };

        var codeAction = new CodeActionMutationProvenance
        {
            Kind = CodeActionMutationKind.CodeFix,
            Provider = provider,
        };

        var revision = CreateRevision("Operation1", "Summary1", codeAction);
        var transaction = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(7),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = revision.Solution,
            Revisions =
            [
                revision,
                CreateRevision("Operation2", "Summary2", codeAction),
            ],
            CurrentRevision = 1,
            MaxRevisions = 5,
        };

        var result = TransactionMutationProvenanceFactory.Create(transaction);

        result.Should().ContainSingle().Which.Should().BeEquivalentTo(new TransactionMutationProvenance
        {
            Revision = 1,
            Operation = "Operation1",
            Summary = "Summary1",
            CodeAction = codeAction,
        });

        TransactionMutationProvenanceFactory.Create(transaction with { CurrentRevision = 2 })
            .Should().HaveCount(2);
    }

    [Fact]
    public void GIVEN_TransactionAtBaseline_WHEN_CreatingProvenance_THEN_ShouldReturnEmptyProjection()
    {
        var solution = _workspace.AddProject("Project", LanguageNames.CSharp).Solution;
        var transaction = new WorkspaceTransaction
        {
            TransactionId = new WorkspaceTransactionId(7),
            BaselineSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1),
            BaselineSolution = solution,
            Revisions = [CreateRevision("Operation", "Summary", codeAction: null)],
            CurrentRevision = 1,
            MaxRevisions = 5,
        };

        var result = TransactionMutationProvenanceFactory.Create(transaction);

        result.Should().ContainSingle();
        result[0].CodeAction.Should().BeNull();
        TransactionMutationProvenanceFactory.Create(transaction with { CurrentRevision = 0 }).Should().BeEmpty();
    }

    public void Dispose()
    {
        _workspace.Dispose();
    }

    private WorkspaceTransactionRevision CreateRevision(
        string operation,
        string summary,
        CodeActionMutationProvenance? codeAction)
    {
        var solution = _workspace.AddProject(Guid.NewGuid().ToString(), LanguageNames.CSharp).Solution;
        return new WorkspaceTransactionRevision
        {
            SnapshotId = WorkspaceSnapshotTestFactory.CreateId(2),
            Solution = solution,
            Changes = new ChangeSummary(),
            Operation = operation,
            Summary = summary,
            Preview = new MutationPreview { Summary = summary },
            CodeActionProvenance = codeAction,
        };
    }
}
