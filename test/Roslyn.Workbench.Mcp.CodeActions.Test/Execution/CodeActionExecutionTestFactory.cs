namespace Roslyn.Workbench.Mcp.CodeActions.Test.Execution;

internal static class CodeActionExecutionTestFactory
{
    public static CodeActionReplayRecipe CreateReplayRecipe()
    {
        var committedSnapshotId = WorkspaceSnapshotTestFactory.CreateId(1);
        var snapshotIdentity = new WorkspaceSnapshotIdentity(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            workspaceEpoch: 1,
            committedSnapshotId,
            transactionId: null);

        return new CodeActionReplayRecipe
        {
            Kind = DiscoveredActionKind.CodeFix,
            ProviderId = "Provider",
            Title = "Title",
            SnapshotIdentity = snapshotIdentity,
            DocumentPath = "Document.cs",
            ProjectId = "ProjectId",
            Start = 0,
            Length = 0,
        };
    }

    public static InMemoryRoslynSolution CreateTwoProjectSolution()
    {
        return RoslynTestFactory.CreateSolution(
        [
            new InMemoryRoslynProjectDefinition
            {
                Name = "FirstProject",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition { Name = "First.cs", Source = "class First { }" },
                ],
            },
            new InMemoryRoslynProjectDefinition
            {
                Name = "SecondProject",
                Documents =
                [
                    new InMemoryRoslynDocumentDefinition { Name = "Second.cs", Source = "class Second { }" },
                ],
            },
        ]);
    }

    public static PreparedFixAllReplayData CreatePreparedFixAllReplayData(
        CodeActionFixAllScope scope = CodeActionFixAllScope.Document,
        int maximumChangedDocuments = 50)
    {
        var identity = new WorkspaceMutationCandidateIdentity
        {
            Documents = [],
        };

        var precondition = new WorkspaceMutationCandidatePrecondition
        {
            ExpectedIdentity = identity,
            MaximumChangedDocuments = maximumChangedDocuments,
        };

        return new PreparedFixAllReplayData
        {
            Scope = scope,
            CandidatePrecondition = precondition,
        };
    }
}
