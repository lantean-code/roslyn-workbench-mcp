using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Capture;

internal interface ICapturedErrorStore
{
    void Add(CapturedErrorRecord record);

    bool TryGet(Guid correlationId, [NotNullWhen(true)] out CapturedErrorRecord? record);
}
