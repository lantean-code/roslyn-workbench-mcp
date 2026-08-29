using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.ErrorReporting.Preparation;

internal interface IPreparedSubmissionStore
{
    bool TryAdd(PreparedSubmission submission);

    bool TryGet(string handle, [NotNullWhen(true)] out PreparedSubmission? submission);

    SubmissionAcquisition TryBeginSubmission(string handle);

    bool TryConfirmSubmission(string handle);

    void Complete(string handle, ErrorSubmissionReceipt receipt);

    void ReleaseForRetry(string handle);

    void Discard(string handle);
}
