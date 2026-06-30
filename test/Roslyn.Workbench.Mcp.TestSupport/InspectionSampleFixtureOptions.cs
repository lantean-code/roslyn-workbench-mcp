namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed record InspectionSampleFixtureOptions
{
    public string Nullable { get; init; } = "enable";

    public string? OutputType { get; init; }

    public string? AdditionalProjectPropertiesText { get; init; }

    public string? AdditionalEditorConfigText { get; init; }

    public bool IncludeConsoleTopLevelDocument { get; init; } = true;

    public bool IncludeConsoleProgramMainDocument { get; init; } = true;
}
