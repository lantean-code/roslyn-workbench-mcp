namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class WorkspaceCompatibilityIntegrationTests
{
    [Theory]
    [InlineData("Sample.sln")]
    [InlineData("Sample.slnx")]
    public async Task GIVEN_SupportedSolutionFormat_WHEN_OpeningWorkspace_THEN_ShouldLoadEverySupportedProject(string solutionName)
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            workspaceAsset: AcceptanceWorkspaceAsset.SolutionHierarchy);

        try
        {
            var result = await OpenWorkspaceAsync(target, Path.Combine(target.WorkspaceRoot, solutionName));

            result.IsError.Should().NotBeTrue();
            var open = AcceptanceProtocol.GetSuccessData(result);
            open.GetProperty("projectCount").GetInt32().Should().Be(2);
            open.GetProperty("documentCount").GetInt32().Should().BeGreaterThanOrEqualTo(2);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_SolutionWithUnsupportedProjects_WHEN_OpeningWorkspace_THEN_ShouldLoadSupportedProjectsAndReportDiagnostics()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            workspaceAsset: AcceptanceWorkspaceAsset.MixedSolution);

        try
        {
            var solutionPath = Path.Combine(target.WorkspaceRoot, "MixedSolution.slnx");
            var result = await OpenWorkspaceAsync(target, solutionPath);

            result.IsError.Should().NotBeTrue();
            var open = AcceptanceProtocol.GetSuccessData(result);
            open.GetProperty("projectCount").GetInt32().Should().Be(1);
            open.GetProperty("loadDiagnostics").GetArrayLength().Should().BeGreaterThan(0);

            var searchResult = await target.CallToolAsync(
                "search-symbols",
                new Dictionary<string, object?>
                {
                    ["query"] = "Class1",
                    ["scope"] = new Dictionary<string, object?>
                    {
                        ["kind"] = "Project",
                        ["project"] = new Dictionary<string, object?>
                        {
                            ["name"] = "Supported",
                        },
                    },
                },
                TestContext.Current.CancellationToken);

            searchResult.IsError.Should().NotBeTrue();
            AcceptanceProtocol.GetSuccessData(searchResult)
                .GetProperty("symbols")
                .GetProperty("items")
                .GetArrayLength()
                .Should()
                .Be(1);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_MalformedSdkProject_WHEN_OpeningWorkspace_THEN_ShouldRejectWithStructuredLoadFailure()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            workspaceAsset: AcceptanceWorkspaceAsset.MalformedSdkProject);

        try
        {
            var projectPath = Path.Combine(target.WorkspaceRoot, "Broken.csproj");
            var result = await OpenWorkspaceAsync(target, projectPath);

            result.IsError.Should().BeTrue();
            var error = AcceptanceProtocol.GetError(result);
            error.GetProperty("code").GetString().Should().Be("WorkspaceLoadFailed");
            error.GetProperty("message").GetString().Should().NotBeNullOrWhiteSpace();
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task<ModelContextProtocol.Protocol.CallToolResult> OpenWorkspaceAsync(
        AcceptanceProcessFixture target,
        string path)
    {
        return await target.CallToolAsync(
            "workspace-open",
            new Dictionary<string, object?>
            {
                ["path"] = path,
                ["workspaceRoot"] = target.WorkspaceRoot,
            },
            TestContext.Current.CancellationToken);
    }
}
