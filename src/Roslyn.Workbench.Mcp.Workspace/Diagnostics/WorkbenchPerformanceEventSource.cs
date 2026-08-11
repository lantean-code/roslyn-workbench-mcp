using System.Diagnostics.Tracing;

namespace Roslyn.Workbench.Mcp.Workspace.Diagnostics;

[EventSource(Name = ProviderName)]
internal sealed class WorkbenchPerformanceEventSource : EventSource
{
    public const string ProviderName = "Roslyn-Workbench-Mcp";
    public const string CandidateProjectionPhase = "candidate-projection";
    public const string BuiltInAnalyzerActivationPhase = "built-in-analyzer-activation";
    public const string CodeActionProjectionPhase = "code-action-projection";
    public const string CodeFixDiscoveryPhase = "code-fix-discovery";
    public const string CommitApplicationPhase = "commit-application";
    public const string CommitCleanupPhase = "commit-cleanup";
    public const string CommitLockAcquisitionPhase = "commit-lock-acquisition";
    public const string CommitPlanningPhase = "commit-planning";
    public const string CommitApplyingPersistencePhase = "commit-applying-persistence";
    public const string CommitPlanPersistencePhase = "commit-plan-persistence";
    public const string CommitRecoveryPhase = "commit-recovery";
    public const string CommitRevalidationPhase = "commit-revalidation";
    public const string CommitValidationPhase = "commit-validation";
    public const string CommitWorkspacePromotionPhase = "commit-workspace-promotion";
    public const string ContextAcquisitionPhase = "context-acquisition";
    public const string ContextConstructionPhase = "context-construction";
    public const string DiscoveryPhase = "discovery";
    public const string DiagnosticCollectionPhase = "diagnostic-collection";
    public const string DocumentProjectionPhase = "document-projection";
    public const string ExternalChangeDetectionPhase = "external-change-detection";
    public const string FolderSelectionPhase = "folder-selection";
    public const string HandlerExecutionPhase = "handler-execution";
    public const string ManifestConstructionPhase = "manifest-construction";
    public const string MutationStagingPhase = "mutation-staging";
    public const string ProjectProjectionPhase = "project-projection";
    public const string ProjectReferenceProjectionPhase = "project-reference-projection";
    public const string ProjectSelectionPhase = "project-selection";
    public const string RefactoringDiscoveryPhase = "refactoring-discovery";
    public const string RequestBindingPhase = "request-binding";
    public const string ResultEnrichmentPhase = "result-enrichment";
    public const string ResultSelectionPhase = "result-selection";
    public const string ResponseProjectionPhase = "response-projection";
    public const string SolutionHierarchyPhase = "solution-hierarchy";
    public const string TargetFrameworkEvaluationPhase = "target-framework-evaluation";
    public const string ToolTotalPhase = "tool-total";
    public const string TransactionCommitOperation = "transaction-commit";
    public const string WorkspaceCompatibilityPhase = "workspace-compatibility";
    public const string WorkspaceLeaseAcquisitionPhase = "workspace-lease-acquisition";
    public const string WorkspaceLoadPhase = "workspace-load";
    public const string WorkspaceQueryCacheFamily = "workspace-query";
    public const string PluginQueryCacheFamily = "plugin-query";
    public const string CodeActionReferenceCacheFamily = "code-action-reference";

    public static WorkbenchPerformanceEventSource Log { get; } = new();

    [NonEvent]
    public PerformanceTraceScope StartPhase(string operation, string phase)
    {
        if (!IsEnabled())
        {
            return default;
        }

        return new PerformanceTraceScope(this, operation, phase);
    }

    [Event(1, Level = EventLevel.Informational)]
    public void PhaseCompleted(double elapsedMilliseconds, string operation, string phase)
    {
        WriteEvent(1, elapsedMilliseconds, operation, phase);
    }

    [Event(2, Level = EventLevel.Informational)]
    public void CacheMetric(string family, string metric, long value)
    {
        WriteEvent(2, family, metric, value);
    }

    [Event(3, Level = EventLevel.Informational)]
    public void AtomicFileCommitRetry(int retryNumber, int delayMilliseconds)
    {
        WriteEvent(3, retryNumber, delayMilliseconds);
    }
}
