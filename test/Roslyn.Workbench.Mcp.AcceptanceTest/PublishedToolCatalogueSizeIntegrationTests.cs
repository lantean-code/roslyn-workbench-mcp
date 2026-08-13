using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedToolCatalogueSizeIntegrationTests
{
    private static readonly IReadOnlyList<string> _codeActionToolNames =
    [
        "list-code-actions",
        "prepare-fix-all",
        "stage-code-action",
    ];

    private static readonly string[] _nullableReturnDiagnosticIds = ["CS8603"];

    private static readonly string[] _removedCodeActionToolNames =
    [
        "describe-code-action",
        "stage-code-fix",
        "stage-fix-all",
        "move-type-to-file",
        "organize-imports",
    ];

    [Fact]
    public async Task GIVEN_PublishedHost_WHEN_ListingTools_THEN_ShouldReportCataloguePayloadSize()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken);

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            var codeActionTools = SelectTools(tools, _codeActionToolNames);

            var cataloguePayloadBytes = MeasurePayloadBytes(tools);
            var codeActionPayloadBytes = MeasurePayloadBytes(codeActionTools);

            tools.Should().NotBeEmpty();
            codeActionTools.Select(static tool => tool.Name).Should().BeEquivalentTo(_codeActionToolNames);
            tools.Select(static tool => tool.Name).Should().NotContain(_removedCodeActionToolNames);
            cataloguePayloadBytes.Should().BeGreaterThan(codeActionPayloadBytes);

            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Complete tools/list result: {tools.Count} tools, {cataloguePayloadBytes:N0} UTF-8 bytes.");
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Code Action tools/list result: {codeActionTools.Count} tools, {codeActionPayloadBytes:N0} UTF-8 bytes.");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    [Fact]
    public async Task GIVEN_PublishedHost_WHEN_ListingCodeActions_THEN_ShouldReportConciseResponseSize()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken,
            AcceptanceWorkspaceAsset.InspectionSample);

        try
        {
            var openResult = await target.CallToolAsync(
                "workspace-open",
                new Dictionary<string, object?>
                {
                    ["path"] = Path.Combine(target.WorkspaceRoot, "Sample.csproj"),
                    ["workspaceRoot"] = target.WorkspaceRoot,
                },
                TestContext.Current.CancellationToken);

            var workspace = AcceptanceWorkspaceIdentity.FromOpenResult(openResult);
            var listResult = await target.CallToolAsync(
                "list-code-actions",
                new Dictionary<string, object?>
                {
                    ["workspace"] = workspace.CreateSelector(),
                    ["document"] = AcceptanceLocationSelectorFactory.CreateDocument("CandidateCodeFixes.cs"),
                    ["expectedSnapshot"] = workspace.CreateSnapshot(transactionRevision: null),
                    ["kinds"] = 1,
                    ["diagnosticIds"] = _nullableReturnDiagnosticIds,
                },
                TestContext.Current.CancellationToken);

            listResult.IsError.Should().NotBeTrue(
                listResult.IsError == true
                    ? AcceptanceProtocol.GetError(listResult).GetRawText()
                    : string.Empty);
            var content = listResult.StructuredContent;
            content.Should().NotBeNull();
            var responseBytes = JsonSerializer.SerializeToUtf8Bytes(
                content.Value,
                McpJsonUtilities.DefaultOptions).Length;
            var responseText = content.Value.GetRawText();

            responseBytes.Should().BeLessThan(32 * 1024);
            responseText.Should().NotContain("providerId");
            responseText.Should().NotContain("equivalenceKey");
            responseText.Should().NotContain("actionPath");
            responseText.Should().NotContain("executorTool");

            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Code Action list response: {responseBytes:N0} UTF-8 bytes.");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static List<McpClientTool> SelectTools(
        IEnumerable<McpClientTool> tools,
        IReadOnlyCollection<string> toolNames)
    {
        var selectedTools = new List<McpClientTool>();
        foreach (var tool in tools)
        {
            if (toolNames.Contains(tool.Name))
            {
                selectedTools.Add(tool);
            }
        }

        return selectedTools;
    }

    private static int MeasurePayloadBytes(IEnumerable<McpClientTool> tools)
    {
        var protocolTools = new List<Tool>();
        foreach (var tool in tools)
        {
            protocolTools.Add(tool.ProtocolTool);
        }

        var result = new ListToolsResult
        {
            Tools = protocolTools,
        };

        return JsonSerializer.SerializeToUtf8Bytes(result, McpJsonUtilities.DefaultOptions).Length;
    }
}
