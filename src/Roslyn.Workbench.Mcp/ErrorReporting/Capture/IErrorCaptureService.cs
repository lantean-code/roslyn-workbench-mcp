using System.Text.Json;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal interface IErrorCaptureService
{
    CapturedErrorRecord Capture(
        Guid correlationId,
        string toolName,
        IDictionary<string, JsonElement>? arguments,
        TimeSpan duration,
        bool cancellationRequested,
        Exception exception);
}
