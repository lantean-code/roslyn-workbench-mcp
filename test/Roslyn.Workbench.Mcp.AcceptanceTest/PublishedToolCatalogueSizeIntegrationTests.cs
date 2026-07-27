using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Roslyn.Workbench.Mcp.AcceptanceTest;

public sealed class PublishedToolCatalogueSizeIntegrationTests
{
    private static readonly IReadOnlyList<string> _codeActionInfrastructureToolNames =
    [
        "describe-code-action",
        "list-code-actions",
        "stage-code-action",
        "stage-code-fix",
        "stage-fix-all",
    ];

    private static readonly IReadOnlyList<string> _fixedCompilerCodeFixToolNames =
    [
        "add-anonymous-type-member-name",
        "add-conditional-interpolation-parentheses",
        "add-explicit-cast",
        "add-inheritdoc",
        "add-obsolete-attribute",
        "add-yield",
        "change-iterator-return-type",
        "declare-as-nullable",
        "fix-incorrect-constraint",
        "fix-return-type",
        "hide-base-member",
        "make-member-required",
        "order-modifiers",
        "remove-in-keyword",
        "remove-new-modifier",
        "remove-unused-local-function",
        "replace-default-literal",
        "transpose-record-keyword",
        "use-explicit-array-in-expression-tree",
        "use-explicit-type-for-const",
    ];

    [Fact]
    public async Task GIVEN_PublishedHost_WHEN_ListingTools_THEN_ShouldReportCataloguePayloadSize()
    {
        await using var target = await AcceptanceProcessFixture.StartPublishedHostAsync(
            TestContext.Current.CancellationToken);

        try
        {
            var tools = await target.ListToolsAsync(TestContext.Current.CancellationToken);
            var dedicatedCodeActionToolNames = new HashSet<string>(
                CodeActionAcceptanceManifest.LoadToolNames(),
                StringComparer.Ordinal);
            var codeActionToolNames = CreateCodeActionToolNames(dedicatedCodeActionToolNames);
            var codeActionTools = SelectTools(tools, codeActionToolNames);
            var dedicatedCodeActionTools = SelectTools(tools, dedicatedCodeActionToolNames);
            var fixedCompilerCodeFixTools = SelectTools(tools, _fixedCompilerCodeFixToolNames);

            var cataloguePayloadBytes = MeasurePayloadBytes(tools);
            var codeActionPayloadBytes = MeasurePayloadBytes(codeActionTools);
            var dedicatedCodeActionPayloadBytes = MeasurePayloadBytes(dedicatedCodeActionTools);
            var fixedCompilerCodeFixPayloadBytes = MeasurePayloadBytes(fixedCompilerCodeFixTools);

            tools.Should().NotBeEmpty();
            codeActionTools.Should().HaveCount(codeActionToolNames.Count);
            dedicatedCodeActionTools.Should().HaveCount(dedicatedCodeActionToolNames.Count);
            fixedCompilerCodeFixTools.Should().HaveCount(_fixedCompilerCodeFixToolNames.Count);
            cataloguePayloadBytes.Should().BeGreaterThan(codeActionPayloadBytes);
            codeActionPayloadBytes.Should().BeGreaterThan(dedicatedCodeActionPayloadBytes);
            dedicatedCodeActionPayloadBytes.Should().BeGreaterThan(fixedCompilerCodeFixPayloadBytes);

            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Complete tools/list result: {tools.Count} tools, {cataloguePayloadBytes:N0} UTF-8 bytes.");
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Code Action tools/list result: {codeActionTools.Count} tools, {codeActionPayloadBytes:N0} UTF-8 bytes.");
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Dedicated Code Action tools/list result: {dedicatedCodeActionTools.Count} tools, {dedicatedCodeActionPayloadBytes:N0} UTF-8 bytes.");
            TestContext.Current.TestOutputHelper?.WriteLine(
                $"Fixed compiler Code Fix tools/list result: {fixedCompilerCodeFixTools.Count} tools, {fixedCompilerCodeFixPayloadBytes:N0} UTF-8 bytes.");
        }
        catch
        {
            target.RetainRootOnFailure();
            throw;
        }
    }

    private static HashSet<string> CreateCodeActionToolNames(
        IEnumerable<string> dedicatedCodeActionToolNames)
    {
        var toolNames = new HashSet<string>(
            dedicatedCodeActionToolNames,
            StringComparer.Ordinal);

        toolNames.UnionWith(_codeActionInfrastructureToolNames);
        return toolNames;
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
