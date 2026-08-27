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
        Open only fully trusted C# workspaces; build logic and analysers run unsandboxed with Host permissions.

        Prefer queries before mutations. Start transactions only when ready; keep each to one coherent change or tightly related set, inspect transaction-preview, then call transaction-commit or transaction-rollback promptly.

        transaction-commit writes source files but does not create a Git commit.
        Guide: {{agentGuideUrl}}
        """;
    }
}
