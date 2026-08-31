using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Converts MCP tool arguments into validated request objects.
/// </summary>
internal interface IToolRequestBinder
{
    /// <summary>
    /// Attempts to deserialize and validate a tool request.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <param name="arguments">The arguments supplied to the tool invocation.</param>
    /// <param name="request">When binding succeeds, the populated request object.</param>
    /// <param name="errorMessage">When binding fails, a message suitable for returning to the MCP client.</param>
    /// <returns><see langword="true"/> when the arguments produce a valid request; otherwise, <see langword="false"/>.</returns>
    bool TryBind<TRequest>(
        IDictionary<string, JsonElement> arguments,
        [NotNullWhen(true)] out TRequest? request,
        [NotNullWhen(false)] out string? errorMessage)
        where TRequest : class;
}
