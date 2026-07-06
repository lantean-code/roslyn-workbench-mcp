namespace Roslyn.Workbench.Mcp.Plugins.Core.Test.Inspection;

public sealed class GetDocumentOutlineToolTests
{
    [Fact]
    public async Task GIVEN_IncludeMembersDisabled_WHEN_CallingExecute_THEN_ShouldOmitMemberNodes()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
                public string Format(string value)
                {
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
        var target = new GetDocumentOutlineTool();

        var result = await target.ExecuteAsync(new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector
            {
                Path = "Sample.cs",
            },
            IncludeMembers = false,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        var typeNode = Enumerate(result.Data!.Root!).Single(static node => node.Name == "GreetingFormatter");
        typeNode.Children.Should().BeEmpty();
    }

    [Fact]
    public async Task GIVEN_IncludeMembersEnabled_WHEN_CallingExecute_THEN_ShouldIncludeMemberNodes()
    {
        using var workspace = MiniWorkspaceFactory.CreateCSharp("""
            namespace Sample;

            public sealed class GreetingFormatter
            {
                public string Format(string value)
                {
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
        var target = new GetDocumentOutlineTool();

        var result = await target.ExecuteAsync(new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector
            {
                Path = "Sample.cs",
            },
            IncludeMembers = true,
        }, context, CancellationToken.None);

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        Enumerate(result.Data!.Root!).Should().Contain(static node => node.Name == "Format");
    }

    [Fact]
    public async Task GIVEN_InspectionWorkspace_WHEN_ExecutingTool_THEN_ShouldReturnOutline()
    {
        using var fixture = await InspectionSampleFixture.CreateAsync();
        var coordinator = BundledCoreToolTestHarness.CreateInspectionCoordinator();
        await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var target = new GetDocumentOutlineTool();

        var result = await BundledCoreToolTestHarness.ExecuteQueryAsync(coordinator, "get-document-outline", target, new GetDocumentOutlineRequest
        {
            Document = new DocumentSelector
            {
                Path = "Formatting.cs",
            },
        });

        result.Outcome.Should().Be(ToolOutcome.Succeeded);
        Enumerate(result.Data!.Root!).Should().Contain(static node => node.Name == "GreetingFormatter");
    }

    private static IEnumerable<OutlineNode> Enumerate(OutlineNode root)
    {
        yield return root;

        foreach (var child in root.Children)
        {
            foreach (var descendant in Enumerate(child))
            {
                yield return descendant;
            }
        }
    }
}
