using System.Diagnostics.Tracing;

namespace Roslyn.Workbench.Mcp.Workspace.Diagnostics;

/// <summary>
/// Emits low-overhead timing, cache, retry, and input-monitor telemetry for workspace operations.
/// </summary>
[EventSource(Name = ProviderName)]
internal sealed class WorkbenchPerformanceEventSource : EventSource
{
    /// <summary>
    /// Identifies the <c>Roslyn-Workbench-Mcp</c> event provider.
    /// </summary>
    public const string ProviderName = "Roslyn-Workbench-Mcp";
    /// <summary>
    /// Identifies the <c>candidate-projection</c> performance phase.
    /// </summary>
    public const string CandidateProjectionPhase = "candidate-projection";
    /// <summary>
    /// Identifies the <c>built-in-analyzer-activation</c> performance phase.
    /// </summary>
    public const string BuiltInAnalyzerActivationPhase = "built-in-analyzer-activation";
    /// <summary>
    /// Identifies the <c>code-action-projection</c> performance phase.
    /// </summary>
    public const string CodeActionProjectionPhase = "code-action-projection";
    /// <summary>
    /// Identifies the <c>code-fix-discovery</c> performance phase.
    /// </summary>
    public const string CodeFixDiscoveryPhase = "code-fix-discovery";
    /// <summary>
    /// Identifies the <c>commit-application</c> performance phase.
    /// </summary>
    public const string CommitApplicationPhase = "commit-application";
    /// <summary>
    /// Identifies the <c>commit-cleanup</c> performance phase.
    /// </summary>
    public const string CommitCleanupPhase = "commit-cleanup";
    /// <summary>
    /// Identifies the <c>commit-lock-acquisition</c> performance phase.
    /// </summary>
    public const string CommitLockAcquisitionPhase = "commit-lock-acquisition";
    /// <summary>
    /// Identifies the <c>commit-planning</c> performance phase.
    /// </summary>
    public const string CommitPlanningPhase = "commit-planning";
    /// <summary>
    /// Identifies the <c>commit-applying-persistence</c> performance phase.
    /// </summary>
    public const string CommitApplyingPersistencePhase = "commit-applying-persistence";
    /// <summary>
    /// Identifies the <c>commit-plan-persistence</c> performance phase.
    /// </summary>
    public const string CommitPlanPersistencePhase = "commit-plan-persistence";
    /// <summary>
    /// Identifies the <c>commit-recovery</c> performance phase.
    /// </summary>
    public const string CommitRecoveryPhase = "commit-recovery";
    /// <summary>
    /// Identifies the <c>commit-revalidation</c> performance phase.
    /// </summary>
    public const string CommitRevalidationPhase = "commit-revalidation";
    /// <summary>
    /// Identifies the <c>commit-validation</c> performance phase.
    /// </summary>
    public const string CommitValidationPhase = "commit-validation";
    /// <summary>
    /// Identifies the <c>commit-workspace-promotion</c> performance phase.
    /// </summary>
    public const string CommitWorkspacePromotionPhase = "commit-workspace-promotion";
    /// <summary>
    /// Identifies the <c>context-acquisition</c> performance phase.
    /// </summary>
    public const string ContextAcquisitionPhase = "context-acquisition";
    /// <summary>
    /// Identifies the <c>context-construction</c> performance phase.
    /// </summary>
    public const string ContextConstructionPhase = "context-construction";
    /// <summary>
    /// Identifies the <c>discovery</c> performance phase.
    /// </summary>
    public const string DiscoveryPhase = "discovery";
    /// <summary>
    /// Identifies the <c>diagnostic-collection</c> performance phase.
    /// </summary>
    public const string DiagnosticCollectionPhase = "diagnostic-collection";
    /// <summary>
    /// Identifies the <c>document-projection</c> performance phase.
    /// </summary>
    public const string DocumentProjectionPhase = "document-projection";
    /// <summary>
    /// Identifies the <c>external-change-detection</c> performance phase.
    /// </summary>
    public const string ExternalChangeDetectionPhase = "external-change-detection";
    /// <summary>
    /// Identifies the <c>external-membership-check</c> performance phase.
    /// </summary>
    public const string ExternalMembershipCheckPhase = "external-membership-check";
    /// <summary>
    /// Identifies the <c>folder-selection</c> performance phase.
    /// </summary>
    public const string FolderSelectionPhase = "folder-selection";
    /// <summary>
    /// Identifies the <c>handler-execution</c> performance phase.
    /// </summary>
    public const string HandlerExecutionPhase = "handler-execution";
    /// <summary>
    /// Identifies the <c>manifest-construction</c> performance phase.
    /// </summary>
    public const string ManifestConstructionPhase = "manifest-construction";
    /// <summary>
    /// Identifies the <c>mutation-staging</c> performance phase.
    /// </summary>
    public const string MutationStagingPhase = "mutation-staging";
    /// <summary>
    /// Identifies the <c>project-projection</c> performance phase.
    /// </summary>
    public const string ProjectProjectionPhase = "project-projection";
    /// <summary>
    /// Identifies the <c>project-reference-projection</c> performance phase.
    /// </summary>
    public const string ProjectReferenceProjectionPhase = "project-reference-projection";
    /// <summary>
    /// Identifies the <c>project-selection</c> performance phase.
    /// </summary>
    public const string ProjectSelectionPhase = "project-selection";
    /// <summary>
    /// Identifies the <c>refactoring-discovery</c> performance phase.
    /// </summary>
    public const string RefactoringDiscoveryPhase = "refactoring-discovery";
    /// <summary>
    /// Identifies the <c>request-binding</c> performance phase.
    /// </summary>
    public const string RequestBindingPhase = "request-binding";
    /// <summary>
    /// Identifies the <c>result-enrichment</c> performance phase.
    /// </summary>
    public const string ResultEnrichmentPhase = "result-enrichment";
    /// <summary>
    /// Identifies the <c>result-selection</c> performance phase.
    /// </summary>
    public const string ResultSelectionPhase = "result-selection";
    /// <summary>
    /// Identifies the <c>response-projection</c> performance phase.
    /// </summary>
    public const string ResponseProjectionPhase = "response-projection";
    /// <summary>
    /// Identifies the <c>solution-hierarchy</c> performance phase.
    /// </summary>
    public const string SolutionHierarchyPhase = "solution-hierarchy";
    /// <summary>
    /// Identifies the <c>target-framework-evaluation</c> performance phase.
    /// </summary>
    public const string TargetFrameworkEvaluationPhase = "target-framework-evaluation";
    /// <summary>
    /// Identifies the <c>tool-total</c> performance phase.
    /// </summary>
    public const string ToolTotalPhase = "tool-total";
    /// <summary>
    /// Identifies the <c>transaction-commit</c> traced operation.
    /// </summary>
    public const string TransactionCommitOperation = "transaction-commit";
    /// <summary>
    /// Identifies the <c>workspace-compatibility</c> performance phase.
    /// </summary>
    public const string WorkspaceCompatibilityPhase = "workspace-compatibility";
    /// <summary>
    /// Identifies the <c>workspace-lease-acquisition</c> performance phase.
    /// </summary>
    public const string WorkspaceLeaseAcquisitionPhase = "workspace-lease-acquisition";
    /// <summary>
    /// Identifies the <c>workspace-load</c> performance phase.
    /// </summary>
    public const string WorkspaceLoadPhase = "workspace-load";
    /// <summary>
    /// Identifies the <c>workspace-query</c> cache-metric family.
    /// </summary>
    public const string WorkspaceQueryCacheFamily = "workspace-query";
    /// <summary>
    /// Identifies the <c>plugin-query</c> cache-metric family.
    /// </summary>
    public const string PluginQueryCacheFamily = "plugin-query";
    /// <summary>
    /// Identifies the <c>code-action-reference</c> cache-metric family.
    /// </summary>
    public const string CodeActionReferenceCacheFamily = "code-action-reference";

    /// <summary>
    /// Gets the process-wide workspace performance event source.
    /// </summary>
    public static WorkbenchPerformanceEventSource Log { get; } = new();

    /// <summary>
    /// Starts a timed phase when the event source is enabled.
    /// </summary>
    /// <param name="operation">The containing operation.</param>
    /// <param name="phase">The phase within the operation.</param>
    /// <returns>A scope that emits the elapsed duration when disposed, or an empty scope when tracing is disabled.</returns>
    [NonEvent]
    public PerformanceTraceScope StartPhase(string operation, string phase)
    {
        if (!IsEnabled())
        {
            return default;
        }

        return new PerformanceTraceScope(this, operation, phase);
    }

    /// <summary>
    /// Emits the elapsed duration for a completed operation phase.
    /// </summary>
    /// <param name="elapsedMilliseconds">The elapsed duration in milliseconds.</param>
    /// <param name="operation">The containing operation.</param>
    /// <param name="phase">The completed phase.</param>
    [Event(1, Level = EventLevel.Informational)]
    public void PhaseCompleted(double elapsedMilliseconds, string operation, string phase)
    {
        WriteEvent(1, elapsedMilliseconds, operation, phase);
    }

    /// <summary>
    /// Emits a numeric cache metric.
    /// </summary>
    /// <param name="family">The cache family.</param>
    /// <param name="metric">The metric name.</param>
    /// <param name="value">The metric value.</param>
    [Event(2, Level = EventLevel.Informational)]
    public void CacheMetric(string family, string metric, long value)
    {
        WriteEvent(2, family, metric, value);
    }

    /// <summary>
    /// Emits an atomic-file commit retry and its scheduled delay.
    /// </summary>
    /// <param name="retryNumber">The one-based retry number.</param>
    /// <param name="delayMilliseconds">The delay before retrying, in milliseconds.</param>
    [Event(3, Level = EventLevel.Informational)]
    public void AtomicFileCommitRetry(int retryNumber, int delayMilliseconds)
    {
        WriteEvent(3, retryNumber, delayMilliseconds);
    }

    /// <summary>
    /// Emits the effective input-monitor coverage for a workspace.
    /// </summary>
    /// <param name="externalRootCount">The number of external roots being monitored.</param>
    /// <param name="evaluatedGlobCount">The number of evaluated MSBuild globs.</param>
    /// <param name="externalWatcherCount">The number of external file-system watchers.</param>
    [Event(4, Level = EventLevel.Informational)]
    public void WorkspaceInputMonitorConfigured(int externalRootCount, int evaluatedGlobCount, int externalWatcherCount)
    {
        WriteEvent(4, externalRootCount, evaluatedGlobCount, externalWatcherCount);
    }
}
