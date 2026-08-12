using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Workspace.Selectors;

namespace Roslyn.Workbench.Mcp.ConsoleOutputPluginFixture;

#pragma warning disable CA1303 // Invariant marker strings are the observable output of this protocol-isolation fixture.

[RoslynPlugin("test.console.output", "Console Output Test Plugin", PluginApiVersions.V1)]
public sealed class ConsoleOutputTestPlugin : IRoslynPlugin
{
    public ConsoleOutputTestPlugin()
    {
        const string marker = "CONSOLE_OUTPUT_PLUGIN_CONSTRUCTED";
        Console.WriteLine(marker);
    }

    public void Configure(IPluginConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        const string marker = "CONSOLE_OUTPUT_PLUGIN_CONFIGURED";
        Console.WriteLine(marker);
        configuration.AddQueryTool<Handler>();
    }

    public sealed record Request : WorkspaceBoundRequest
    {
        public string Value { get; init; } = string.Empty;
    }

    public sealed record Response
    {
        public string Value { get; init; } = string.Empty;
    }

    [RoslynTool("test-console-output", "Test Console Output", "Writes console diagnostics before returning a stable payload.")]
    private sealed class Handler : IQueryToolHandler<Request, Response>
    {
        public Handler()
        {
            const string marker = "CONSOLE_OUTPUT_HANDLER_CONSTRUCTED";
            Console.WriteLine(marker);
        }

        public ValueTask<PluginExecutionResult<Response>> ExecuteAsync(
            Request request,
            IQueryContext context,
            CancellationToken cancellationToken)
        {
            const string marker = "CONSOLE_OUTPUT_HANDLER_EXECUTED";
            Console.WriteLine(marker);

            var response = new Response
            {
                Value = request.Value,
            };

            var result = PluginExecutionResult.Success(response);
            return ValueTask.FromResult(result);
        }
    }
}

#pragma warning restore CA1303
