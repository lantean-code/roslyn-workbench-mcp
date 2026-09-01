using System.Reflection;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.Hosting;

/// <summary>
/// Applies server-wide safety instructions and routes dynamic plugin tool requests.
/// </summary>
internal sealed class RoslynWorkbenchMcpServerOptionsConfiguration : IConfigureOptions<McpServerOptions>
{
    private const string _documentationUrlPrefix = "https://lantean-code.github.io/roslyn-workbench-mcp";
    private const string _sourceTagMetadataKey = "RoslynWorkbenchSourceTag";
    private static readonly string _instructions = CreateInstructions();
    private readonly IPluginMcpRequestHandler _pluginRequestHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoslynWorkbenchMcpServerOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="pluginRequestHandler">The handler that lists and invokes loaded plugin tools.</param>
    public RoslynWorkbenchMcpServerOptionsConfiguration(IPluginMcpRequestHandler pluginRequestHandler)
    {
        _pluginRequestHandler = pluginRequestHandler;
    }

    /// <summary>
    /// Applies server instructions and routes MCP tool requests through the plugin request handler.
    /// </summary>
    /// <param name="options">The MCP server options to configure.</param>
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

        var documentationVersion = StringComparer.Ordinal.Equals(sourceTag, "0.0.0-dev")
            ? "dev"
            : sourceTag;
        var agentGuideUrl = $"{_documentationUrlPrefix}/{documentationVersion}/agent/";

        return $$"""
        Open only fully trusted C# workspaces; build logic and analysers run unsandboxed with Host permissions.

        Prefer queries before mutations. Start transactions only when ready; keep each to one coherent change or tightly related set, inspect transaction-preview, then call transaction-commit or transaction-rollback promptly.

        transaction-commit writes source files but does not create a Git commit.
        Docs: {{agentGuideUrl}}
        """;
    }
}
