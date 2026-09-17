using Roslyn.Workbench.Mcp.ScenarioRunner.Hosting;
using Roslyn.Workbench.Mcp.ScenarioRunner.Validation;

namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CompilerValidation;

internal sealed record CompilerValidationMeasurement
{
    public required int Iteration { get; init; }

    public required double StagingMilliseconds { get; init; }

    public required double PreviewMilliseconds { get; init; }

    public required int PreviewDocumentCount { get; init; }

    public required CompilerValidationInvocationMeasurement ColdValidation { get; init; }

    public required CompilerValidationInvocationMeasurement RepeatedValidation { get; init; }

    public required HostShutdownResult HostShutdown { get; init; }

    public required RunValidationResult Validation { get; init; }
}
