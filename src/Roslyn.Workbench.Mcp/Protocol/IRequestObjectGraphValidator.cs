using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Roslyn.Workbench.Mcp.Protocol;

internal interface IRequestObjectGraphValidator
{
    bool TryCreateInvalidRequestError(
        object request,
        JsonSerializerOptions serializerOptions,
        [NotNullWhen(true)] out string? errorMessage);
}
