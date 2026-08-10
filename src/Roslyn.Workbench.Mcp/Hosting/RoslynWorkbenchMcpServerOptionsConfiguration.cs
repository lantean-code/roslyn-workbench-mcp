using System.Reflection;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Hosting;

internal sealed class RoslynWorkbenchMcpServerOptionsConfiguration : IConfigureOptions<McpServerOptions>
{
    private const string _agentGuideUrlPrefix = "https://raw.githubusercontent.com/lantean-code/roslyn-workbench-mcp";
    private const string _sourceTagMetadataKey = "RoslynWorkbenchSourceTag";
    private static readonly string _instructions = CreateInstructions();
    private readonly IPluginMcpRequestHandler _pluginRequestHandler;

    public RoslynWorkbenchMcpServerOptionsConfiguration(IPluginMcpRequestHandler pluginRequestHandler)
    {
        _pluginRequestHandler = pluginRequestHandler;
    }

    public void Configure(McpServerOptions options)
    {
        options.ServerInstructions = _instructions;
        options.Handlers.ListToolsHandler = _pluginRequestHandler.ListToolsAsync;
        options.Handlers.CallToolHandler = _pluginRequestHandler.CallToolAsync;
    }

    private static string CreateInstructions()
    {
        var sourceTag = typeof(RoslynWorkbenchMcpServerOptionsConfiguration)
            .Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(static attribute => attribute.Key == _sourceTagMetadataKey)
            .Value;

        if (string.IsNullOrWhiteSpace(sourceTag))
        {
            throw new InvalidOperationException("The Host build does not identify its Roslyn Workbench source tag.");
        }

        var agentGuideUrl = $"{_agentGuideUrlPrefix}/{sourceTag}/docs/AgentGuide.md";

        return $$"""
        Roslyn Workbench operates on fully trusted C# workspaces and does not sandbox project build logic or analyzers.

        - Prefer queries before mutations.
        - For mutations, start a Workbench transaction only when ready to change source, keep it to one coherent change or tightly related set, inspect transaction-preview, then call transaction-commit or transaction-rollback promptly.
        - Do not accumulate unrelated work in an open transaction. Run queries outside a transaction unless they need its staged state.
        - Treat broad solution-wide mutations as standalone transactions and roll back an unexpectedly large preview.
        - Treat workspace epochs, transaction revisions, and structured next actions as authoritative. Reload or resolve targets again when instructed rather than reusing stale spans, symbols, or references.

        transaction-commit writes source files but does not create a Git commit.
        For more detailed agent guidance for this Host version, read {{agentGuideUrl}}.
        """;
    }
}
