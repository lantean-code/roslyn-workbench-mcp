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
