using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

internal interface IToolRequestBinder
{
    bool TryBind<TRequest>(
        IDictionary<string, JsonElement> arguments,
        [NotNullWhen(true)] out TRequest? request,
        [NotNullWhen(false)] out string? errorMessage)
        where TRequest : class;
}
