using System.Text.Json;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedInspectionResponseIntegrationTests
{
    [Fact]
    public async Task GIVEN_BoundedInspectionRequests_WHEN_CallingPublishedTools_THEN_ShouldReturnBoundedPointerOnlyShapes()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            workspaceAsset: AcceptanceWorkspaceAsset.SolutionHierarchy);

        try
        {
            var duplicateSourcePath = Path.Combine(target.WorkspaceRoot, "App", "DuplicateSamples.cs");
            await File.WriteAllTextAsync(
                duplicateSourcePath,
                """
                namespace Sample;

                public static class DuplicateSamples
                {
                    public static string First(string value)
                    {
                        var formatter = new AppFormatter();
                        var first = formatter.Format(value);
                        var second = formatter.Format(first);
                        return second;
                    }

                    public static string Second(string value)
                    {
                        var formatter = new AppFormatter();
                        var first = formatter.Format(value);
                        var second = formatter.Format(first);
                        return second;
                    }
                }
                """,
                TestContext.Current.CancellationToken);

            var solutionPath = Path.Combine(target.WorkspaceRoot, "Sample.slnx");
            var openResult = await target.CallToolAsync(
                "workspace-open",
                new Dictionary<string, object?>
                {
                    ["path"] = solutionPath,
                    ["workspaceRoot"] = target.WorkspaceRoot,
                },
                TestContext.Current.CancellationToken);

            var workspaceSelector = AcceptanceWorkspaceIdentity.FromOpenResult(openResult).CreateSelector();
            var projectSelector = new Dictionary<string, object?>
            {
                ["path"] = "App/App.csproj",
                ["targetFramework"] = "net10.0",
            };
            var documentSelector = new Dictionary<string, object?>
            {
                ["project"] = projectSelector,
                ["path"] = "App/AppFormatter.cs",
            };
            var formatSymbolSelector = new Dictionary<string, object?>
            {
                ["project"] = projectSelector,
                ["documentationCommentId"] = "M:Sample.AppFormatter.Format(System.String)",
            };
            var duplicateSymbolSelector = new Dictionary<string, object?>
            {
                ["project"] = projectSelector,
                ["documentationCommentId"] = "M:Sample.DuplicateSamples.First(System.String)",
            };

            await AssertSolutionStructureAsync(target, workspaceSelector);
            await AssertDocumentOutlineAsync(target, workspaceSelector, documentSelector);
            await AssertCallersAsync(target, workspaceSelector, formatSymbolSelector);
            await AssertDuplicateCodeAsync(target, workspaceSelector, projectSelector);
            await AssertControlFlowGraphAsync(target, workspaceSelector, duplicateSymbolSelector);
            await AssertOperationTreeAsync(target, workspaceSelector, documentSelector);
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static async Task AssertSolutionStructureAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector)
    {
        var result = await target.CallToolAsync(
            "get-solution-structure",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["includeDocuments"] = true,
                ["projectsLimit"] = 2,
                ["documentsPerProjectLimit"] = 1,
                ["projectReferencesPerProjectLimit"] = 0,
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().NotBeTrue();
        var projects = AcceptanceProtocol.GetSuccessData(result).GetProperty("projects");
        var appProject = projects.GetProperty("items").EnumerateArray()
            .Single(static project => project.GetProperty("name").GetString()!.Contains("App", StringComparison.Ordinal));

        AssertBoundedCollection(appProject.GetProperty("documents"), itemCount: 1, hasMore: true);
        AssertBoundedCollection(appProject.GetProperty("projectReferences"), itemCount: 0, hasMore: true);
    }

    private static async Task AssertDocumentOutlineAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        IReadOnlyDictionary<string, object?> documentSelector)
    {
        var result = await target.CallToolAsync(
            "get-document-outline",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["document"] = documentSelector,
                ["nodesLimit"] = 1,
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().NotBeTrue();
        var data = AcceptanceProtocol.GetSuccessData(result);
        data.GetProperty("truncated").GetBoolean().Should().BeTrue();
        var children = data.GetProperty("root").GetProperty("children");
        children.GetArrayLength().Should().Be(1);
        AssertPointer(children[0].GetProperty("location"));
    }

    private static async Task AssertCallersAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        IReadOnlyDictionary<string, object?> symbolSelector)
    {
        var result = await target.CallToolAsync(
            "find-callers",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["symbol"] = symbolSelector,
                ["callersLimit"] = 10,
                ["callSitesPerCallerLimit"] = 1,
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().NotBeTrue();
        var callers = AcceptanceProtocol.GetSuccessData(result).GetProperty("callers").GetProperty("items").EnumerateArray()
            .Where(static item => item.GetProperty("callSites").GetProperty("hasMore").GetBoolean())
            .ToArray();

        callers.Should().HaveCount(2);
        foreach (var caller in callers)
        {
            caller.TryGetProperty("locations", out _).Should().BeFalse();
            caller.TryGetProperty("contexts", out _).Should().BeFalse();
            var callSites = caller.GetProperty("callSites");
            AssertBoundedCollection(callSites, itemCount: 1, hasMore: true);
            callSites.GetProperty("totalCount").GetInt32().Should().Be(2);
            AssertPointer(callSites.GetProperty("items")[0].GetProperty("location"));
        }
    }

    private static async Task AssertDuplicateCodeAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        IReadOnlyDictionary<string, object?> projectSelector)
    {
        var result = await target.CallToolAsync(
            "find-duplicate-code",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["scope"] = new Dictionary<string, object?>
                {
                    ["kind"] = "Project",
                    ["project"] = projectSelector,
                },
                ["minimumStatements"] = 3,
                ["groupsLimit"] = 1,
                ["occurrencesPerGroupLimit"] = 1,
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().NotBeTrue();
        var group = AcceptanceProtocol.GetSuccessData(result).GetProperty("groups").GetProperty("items")[0];
        var occurrences = group.GetProperty("occurrences");
        AssertBoundedCollection(occurrences, itemCount: 1, hasMore: true);
        var occurrence = occurrences.GetProperty("items")[0];
        occurrence.TryGetProperty("context", out _).Should().BeFalse();
        AssertPointer(occurrence.GetProperty("location"));
    }

    private static async Task AssertControlFlowGraphAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        IReadOnlyDictionary<string, object?> symbolSelector)
    {
        var result = await target.CallToolAsync(
            "get-control-flow-graph",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["symbol"] = symbolSelector,
                ["maxOperationsPerBlock"] = 1,
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().NotBeTrue();
        var blocks = AcceptanceProtocol.GetSuccessData(result).GetProperty("blocks");
        var operations = blocks.GetProperty("items").EnumerateArray()
            .Select(static block => block.GetProperty("operations"))
            .Single(static collection => collection.GetProperty("hasMore").GetBoolean());

        AssertBoundedCollection(blocks, itemCount: 3, hasMore: false);
        AssertBoundedCollection(operations, itemCount: 1, hasMore: true);
        var operation = operations.GetProperty("items")[0];
        operation.TryGetProperty("syntax", out _).Should().BeFalse();
        AssertPointer(operation.GetProperty("location"));
    }

    private static async Task AssertOperationTreeAsync(
        AcceptanceProcessFixture target,
        IReadOnlyDictionary<string, object?> workspaceSelector,
        IReadOnlyDictionary<string, object?> documentSelector)
    {
        var sourcePath = Path.Combine(target.WorkspaceRoot, "App", "AppFormatter.cs");
        var source = await File.ReadAllTextAsync(sourcePath, TestContext.Current.CancellationToken);
        var expressionStart = source.IndexOf("value.Trim()", StringComparison.Ordinal);

        var result = await target.CallToolAsync(
            "get-operation-tree",
            new Dictionary<string, object?>
            {
                ["workspace"] = workspaceSelector,
                ["location"] = new Dictionary<string, object?>
                {
                    ["span"] = new Dictionary<string, object?>
                    {
                        ["document"] = documentSelector,
                        ["range"] = new Dictionary<string, object?>
                        {
                            ["start"] = expressionStart,
                            ["length"] = "value.Trim()".Length,
                        },
                    },
                },
                ["nodesLimit"] = 1,
            },
            TestContext.Current.CancellationToken);

        result.IsError.Should().NotBeTrue();
        var data = AcceptanceProtocol.GetSuccessData(result);
        data.GetProperty("truncated").GetBoolean().Should().BeTrue();
        var root = data.GetProperty("root");
        root.TryGetProperty("syntax", out _).Should().BeFalse();
        root.TryGetProperty("constantValue", out _).Should().BeFalse();
        root.GetProperty("hasConstantValue").GetBoolean().Should().BeFalse();
        root.GetProperty("truncated").GetBoolean().Should().BeTrue();
        root.GetProperty("children").GetArrayLength().Should().Be(0);
        AssertPointer(root.GetProperty("location"));
    }

    private static void AssertBoundedCollection(JsonElement collection, int itemCount, bool hasMore)
    {
        collection.GetProperty("items").GetArrayLength().Should().Be(itemCount);
        collection.GetProperty("hasMore").GetBoolean().Should().Be(hasMore);
    }

    private static void AssertPointer(JsonElement location)
    {
        location.GetProperty("document").GetProperty("path").GetString().Should().NotBeNullOrWhiteSpace();
        location.GetProperty("span").GetProperty("length").GetInt32().Should().BePositive();
    }
}
