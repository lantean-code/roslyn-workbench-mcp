using System.Diagnostics.Tracing;

namespace Roslyn.Workbench.Mcp.Workspace.Diagnostics;

[EventSource(Name = ProviderName)]
internal sealed class WorkbenchPerformanceEventSource : EventSource
{
    public const string ProviderName = "Roslyn-Workbench-Mcp";
    public const string CandidateProjectionPhase = "candidate-projection";
    public const string ContextAcquisitionPhase = "context-acquisition";
    public const string ContextConstructionPhase = "context-construction";
    public const string DiscoveryPhase = "discovery";
    public const string DocumentProjectionPhase = "document-projection";
    public const string ExternalChangeDetectionPhase = "external-change-detection";
    public const string FolderSelectionPhase = "folder-selection";
    public const string HandlerExecutionPhase = "handler-execution";
    public const string ManifestConstructionPhase = "manifest-construction";
    public const string MutationStagingPhase = "mutation-staging";
    public const string ProjectProjectionPhase = "project-projection";
    public const string ProjectReferenceProjectionPhase = "project-reference-projection";
    public const string ProjectSelectionPhase = "project-selection";
    public const string RequestBindingPhase = "request-binding";
    public const string ResultEnrichmentPhase = "result-enrichment";
    public const string ResultSelectionPhase = "result-selection";
    public const string ResponseProjectionPhase = "response-projection";
    public const string SolutionHierarchyPhase = "solution-hierarchy";
    public const string TargetFrameworkEvaluationPhase = "target-framework-evaluation";
    public const string ToolTotalPhase = "tool-total";
    public const string WorkspaceCompatibilityPhase = "workspace-compatibility";
    public const string WorkspaceLeaseAcquisitionPhase = "workspace-lease-acquisition";
    public const string WorkspaceLoadPhase = "workspace-load";

    public static WorkbenchPerformanceEventSource Log { get; } = new();

    [NonEvent]
    public PerformanceTraceScope StartPhase(string operation, string phase)
    {
        return IsEnabled()
            ? new PerformanceTraceScope(this, operation, phase)
            : default;
    }

    [Event(1, Level = EventLevel.Informational)]
    public void PhaseCompleted(double elapsedMilliseconds, string operation, string phase)
    {
        WriteEvent(1, elapsedMilliseconds, operation, phase);
    }
}
