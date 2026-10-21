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
    private readonly IPluginMcpRequestHandler _pluginRequestHandler;
    private readonly OperationalPolicy _operationalPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoslynWorkbenchMcpServerOptionsConfiguration"/> class.
    /// </summary>
    /// <param name="pluginRequestHandler">The handler that lists and invokes loaded plugin tools.</param>
    /// <param name="operationalPolicy">The effective immutable operational policy.</param>
    public RoslynWorkbenchMcpServerOptionsConfiguration(
        IPluginMcpRequestHandler pluginRequestHandler,
        OperationalPolicy operationalPolicy)
    {
        _pluginRequestHandler = pluginRequestHandler;
        _operationalPolicy = operationalPolicy;
    }

    /// <summary>
    /// Applies server instructions and routes MCP tool requests through the plugin request handler.
    /// </summary>
    /// <param name="options">The MCP server options to configure.</param>
    public void Configure(McpServerOptions options)
    {
        options.ServerInstructions = CreateInstructions(_operationalPolicy);
        options.Handlers.ListToolsHandler = _pluginRequestHandler.ListToolsAsync;
        options.Handlers.CallToolHandler = _pluginRequestHandler.CallToolAsync;
    }

    private static string CreateInstructions(OperationalPolicy policy)
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

        var workflow = policy.Mode switch
        {
            OperationalMode.InspectionOnly => "Inspection only; mutations disabled.",
            OperationalMode.Transactional => "One coherent transaction; preview then commit for Host confirmation or roll back promptly.",
            OperationalMode.ApprovalRequired => "One coherent transaction; transaction-review receiptId for commit approval; no transaction-preview.",
            OperationalMode.AutonomousTrusted => "One coherent transaction; preview then commit or roll back promptly.",
            _ => throw new InvalidOperationException("The operational mode is not available for server instruction publication."),
        };

        if (policy.CompilerValidationRequired)
        {
            workflow += " transaction-validate previews compiler checks; review/commit revalidate.";
        }

        var persistenceGuidance = policy.SourceMutationEnabled
            ? "Commit writes files, not a Git commit."
            : "Inspection mode does not sandbox build logic.";

        return $$"""
        Open only fully trusted C# workspaces; build logic and analysers run unsandboxed with Host permissions.
        Prefer queries before mutations. {{workflow}}
        {{persistenceGuidance}}
        Skill: skill://roslyn-workbench/SKILL.md
        Docs: {{agentGuideUrl}}
        """;
    }
}
