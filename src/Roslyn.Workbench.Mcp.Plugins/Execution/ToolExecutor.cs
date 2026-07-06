using System.Text.Json;
using Roslyn.Workbench.Mcp.Contracts.Results;

namespace Roslyn.Workbench.Mcp.Plugins.Execution;

public sealed class ToolExecutor
{
    private readonly IToolExecutionContextFactory _contextFactory;

    public ToolExecutor(IToolExecutionContextFactory contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async ValueTask<CallToolResult> ExecuteAsync(
        RegisteredTool tool,
        IDictionary<string, JsonElement> arguments,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            var effectiveResult = await tool.Invoker
                .ExecuteAsync(tool, arguments, _contextFactory, cancellationToken)
                .ConfigureAwait(false);
            var structuredContent = tool.ResponseWriter(effectiveResult);

            return new CallToolResult
            {
                Content = [],
                StructuredContent = structuredContent,
                IsError = IsErrorOutcome(effectiveResult.Outcome),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            var fault = PluginExecutionResultBox.CreateUnhandledException();
            var structuredContent = tool.ResponseWriter(fault);

            return new CallToolResult
            {
                Content = [],
                StructuredContent = structuredContent,
                IsError = true,
            };
        }
    }

    private static bool IsErrorOutcome(ToolOutcome outcome)
    {
        return outcome is ToolOutcome.Rejected or ToolOutcome.Conflict or ToolOutcome.Faulted;
    }
}
