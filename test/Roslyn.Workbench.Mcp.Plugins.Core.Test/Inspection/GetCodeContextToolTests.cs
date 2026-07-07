namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetCodeContextToolTests
{
    [Fact]
    public async Task GIVEN_MissingLocationSelector_WHEN_CallingExecute_THEN_ShouldReturnInvalidRequest()
    {
        var target = new GetCodeContextTool();
        var context = new QueryContextBuilder().Build();

        var result = await target.ExecuteAsync(new GetCodeContextRequest(), context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("InvalidRequest");
    }

    [Fact]
    public async Task GIVEN_SnapshotMismatch_WHEN_CallingExecute_THEN_ShouldReturnConflict()
    {
        var resolver = new Mock<IWorkspaceResolver>();
        var target = new GetCodeContextTool();
        var context = new QueryContextBuilder()
            .WithResolver(resolver.Object)
            .Build();

        resolver
            .Setup(mock => mock.ValidateSnapshot(It.IsAny<SnapshotPrecondition?>()))
            .Returns(SnapshotMatchResult.TransactionRevisionMismatch());

        var result = await target.ExecuteAsync(new GetCodeContextRequest
        {
            Location = new LocationSelector(),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = 1,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Conflict);
        result.Error!.Code.Should().Be("SnapshotMismatch");
    }

    [Fact]
    public async Task GIVEN_ResolvedLocationAndDiagnosticsRequested_WHEN_CallingExecute_THEN_ShouldReturnContextAndDiagnostics()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
                public string Format(string value)
                {
                    var unused = 42;
                    return value.Trim();
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var resolver = workspace.CreateResolver(workspaceIdentity);
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(resolver)
            .WithWorkspaceIdentity(workspaceIdentity)
            .Build();
        var target = new GetCodeContextTool();

        var result = await target.ExecuteAsync(new GetCodeContextRequest
        {
            Location = workspace.GetLocationSelector("var unused = 42;"),
            IncludeDiagnostics = true,
            IncludeEnclosingSymbols = true,
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = workspaceIdentity.WorkspaceEpoch,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        result.Data!.Value.Text.Should().Contain("var unused = 42;");
        result.Data.Value.Diagnostics.Should().Contain(static diagnostic => diagnostic.Id == "CS0219");
        result.Data.Value.EnclosingSymbols.Should().Contain(static symbol => symbol.DisplayName.Contains("GreetingFormatter.Format", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GIVEN_ResponseExceedsConfiguredLimit_WHEN_CallingExecute_THEN_ShouldReturnNarrowRequestRejection()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
                public string Format(string value)
                {
                    var unused = 42;
                    return value.Trim();
                }
            }
            """);
        var workspaceIdentity = workspace.CreateWorkspaceIdentity();
        var resolver = workspace.CreateResolver(workspaceIdentity);
        var context = new QueryContextBuilder()
            .WithCurrentSolution(workspace.Solution)
            .WithResolver(resolver)
            .WithWorkspaceIdentity(workspaceIdentity)
            .WithMaxResponseBytes(1)
            .Build();
        var target = new GetCodeContextTool();

        var result = await target.ExecuteAsync(new GetCodeContextRequest
        {
            Location = workspace.GetLocationSelector("var unused = 42;"),
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = workspaceIdentity.WorkspaceEpoch,
            },
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Rejected);
        result.Error!.Code.Should().Be("ResponseLimitExceeded");
        result.RequiredAction.Should().Be(RequiredAction.NarrowRequest);
    }
}
