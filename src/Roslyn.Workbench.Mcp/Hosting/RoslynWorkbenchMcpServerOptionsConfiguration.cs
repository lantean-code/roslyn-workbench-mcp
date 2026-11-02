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
        Discover Roslyn C# tools by task: workflow, everyday inspection, specialist inspection.
        Skill: skill://roslyn-workbench/SKILL.md

        Workflow: workspace lifecycle, status, error recovery and Roslyn Code Action discovery/preparation; when mutations are enabled, transactions, rename, formatting and Code Action staging. Discover fixes/refactorings with list-code-actions; preview or review staged changes before committing according to the operating mode above.
        Everyday inspection: start with get-solution-structure, get-project-details, get-document-outline, get-code-context, search-symbols, get-symbol-info, get-symbol-members, find-references and get-diagnostics as the task requires.
        Specialist inspection: search for callers/callees, implementations/overrides, inheritance, overloads, attributes, partial declarations, API surface, document configuration, dependencies/cycles, change/test impact, async/nullability/disposal, unused symbols, duplicate code, data/control flow or operation trees when those questions arise. Use resolve-symbol to bind a known source location and go-to-definition for declaration navigation.
        If your client defers tool definitions, search for the relevant capability or exact tool name, then read its schema before calling. These groups guide selection; they do not hide tools or control client loading. The live tools/list and server-status are authoritative for availability and policy. Reuse returned selectors and snapshots; follow structured failure continuations.
        Read the skill entry point and only the task-relevant references. The embedded skill resource is readable guidance; native skill activation depends on client installation.
        Docs: {{agentGuideUrl}}
        """;
    }
}
