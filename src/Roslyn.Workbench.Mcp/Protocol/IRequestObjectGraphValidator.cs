using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

/// <summary>
/// Validates deserialized request objects using their data-annotation constraints.
/// </summary>
internal interface IRequestObjectGraphValidator
{
    /// <summary>
    /// Validates a request and reports all discovered validation failures.
    /// </summary>
    /// <param name="request">The request being processed.</param>
    /// <param name="serializerOptions">The serializer settings used to format property names and validation values.</param>
    /// <param name="errorMessage">When validation fails, a message describing the invalid request fields.</param>
    /// <returns><see langword="true"/> when the request is invalid; otherwise, <see langword="false"/>.</returns>
    bool TryCreateInvalidRequestError(
        object request,
        JsonSerializerOptions serializerOptions,
        [NotNullWhen(true)] out string? errorMessage);
}
