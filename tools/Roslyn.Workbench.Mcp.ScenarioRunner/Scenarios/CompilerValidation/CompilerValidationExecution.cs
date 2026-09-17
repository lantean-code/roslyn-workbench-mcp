namespace Roslyn.Workbench.Mcp.ScenarioRunner.Scenarios.CompilerValidation;

internal sealed record CompilerValidationExecution
{
    public required double StagingMilliseconds { get; init; }

    public required double PreviewMilliseconds { get; init; }

    public required int PreviewDocumentCount { get; init; }

    public required CompilerValidationInvocationMeasurement ColdValidation { get; init; }

    public required CompilerValidationInvocationMeasurement RepeatedValidation { get; init; }
}
