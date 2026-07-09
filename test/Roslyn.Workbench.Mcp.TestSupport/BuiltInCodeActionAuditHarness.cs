using System.Collections.Immutable;
using Roslyn.Workbench.Mcp.Contracts.CodeActions;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Server;
using Roslyn.Workbench.Mcp.Plugins;
using Roslyn.Workbench.Mcp.Plugins.Core;

namespace Roslyn.Workbench.Mcp.TestSupport;

public sealed record BuiltInCodeActionAuditProbe
{
    public SelectorResolveStatus LocationStatus { get; init; }

    public ToolOutcome VisibilityOutcome { get; init; }

    public BuiltInCodeActionRuntimeAuditOutcome RuntimeOutcome { get; init; }

    public int MatchingActionCount { get; init; }

    public bool IsVisibleInList { get; init; }

    public IReadOnlyList<string> CandidateTitles { get; init; } = [];

    public string? FailureMessage { get; init; }
}

public static class BuiltInCodeActionAuditHarness
{
    public static async Task<BuiltInCodeActionAuditProbe> ProbeAsync(BuiltInCodeActionAuditCase auditCase)
    {
        ArgumentNullException.ThrowIfNull(auditCase);

        using var fixture = await auditCase.FixtureFactory();
        var runtime = new CodeActionRuntimeComposer()
            .Compose(new CodeActionRuntimeOptions
            {
                IncludeBuiltInAssemblies = true,
            });
        var coordinator = WorkspaceCoordinatorFactory.CreateWithCodeActionRuntime(runtime, BundledCoreToolExecutionServicesFactory.Create());
        var openResult = await coordinator.OpenAsync(new WorkspaceOpenRequest
        {
            Path = fixture.ProjectPath,
        }, CancellationToken.None);
        var registry = BundledPluginRegistryFactory.CreateRegistry();

        await using var queryLease = coordinator.CreateQueryContext(new ListCodeActionsRequest(), CancellationToken.None);
        var queryContext = queryLease.Context!;
        var location = auditCase.LocationFactory(fixture);
        var resolution = await queryContext.WorkspaceResolver.ResolveLocationAsync(location, CancellationToken.None);
        if (resolution.Status != SelectorResolveStatus.Resolved || resolution.Value is null)
        {
            return new BuiltInCodeActionAuditProbe
            {
                LocationStatus = resolution.Status,
                VisibilityOutcome = ToolOutcome.Rejected,
                RuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.NotOffered,
                FailureMessage = "The audit location selector did not resolve.",
            };
        }

        var document = queryContext.CurrentSolution.GetDocument(resolution.Value.SourceTree);
        if (document is null)
        {
            return new BuiltInCodeActionAuditProbe
            {
                LocationStatus = SelectorResolveStatus.NotFound,
                VisibilityOutcome = ToolOutcome.Rejected,
                RuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.NotOffered,
                FailureMessage = "The resolved location did not map to a source document.",
            };
        }

        IReadOnlyList<DiscoveredAuditCodeAction> discovered;
        ImmutableArray<Diagnostic> codeFixDiagnostics = [];
        if (auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix)
        {
            codeFixDiagnostics = await GetDocumentDiagnosticsAsync(document, resolution.Value.SourceSpan, CancellationToken.None);
            discovered = await DiscoverCodeFixesAsync(
                runtime.CodeFixProviders.Single(candidate => string.Equals(GetProviderId(candidate), auditCase.ProviderId, StringComparison.Ordinal)),
                document,
                resolution.Value.SourceSpan,
                codeFixDiagnostics,
                CancellationToken.None);
        }
        else
        {
            discovered = await DiscoverRefactoringsAsync(
                runtime.RefactoringProviders.Single(candidate => string.Equals(GetProviderId(candidate), auditCase.ProviderId, StringComparison.Ordinal)),
                document,
                resolution.Value.SourceSpan,
                CancellationToken.None);
        }
        var matching = discovered
            .Where(action => MatchesTitle(auditCase, action.Title))
            .Where(action => auditCase.ActionPath.Count == 0 || action.ActionPath.SequenceEqual(auditCase.ActionPath))
            .ToArray();
        var visibilityResult = await ((ICodeActionQueryContext)queryContext).ListCodeActionsAsync(new ListCodeActionsRequest
        {
            Location = location,
            IncludeCodeFixes = auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix,
            IncludeRefactorings = auditCase.Kind == BuiltInCodeActionAuditKind.Refactoring,
            ExpectedSnapshot = new SnapshotPrecondition
            {
                WorkspaceEpoch = openResult.WorkspaceEpoch!.Value,
            },
        }, CancellationToken.None);

        if (matching.Length == 0)
        {
            return new BuiltInCodeActionAuditProbe
            {
                LocationStatus = resolution.Status,
                VisibilityOutcome = visibilityResult.Outcome,
                RuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.NotOffered,
                MatchingActionCount = 0,
                IsVisibleInList = IsVisible(visibilityResult, auditCase),
                CandidateTitles = discovered.Select(static action => action.Title).Distinct(StringComparer.Ordinal).OrderBy(static title => title, StringComparer.Ordinal).ToArray(),
                FailureMessage = auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix
                    ? BuildCodeFixDiagnosticMessage(codeFixDiagnostics)
                    : null,
            };
        }

        try
        {
            var operations = await matching[0].Action.GetOperationsAsync(queryContext.CurrentSolution, new Progress<CodeAnalysisProgress>(), CancellationToken.None);
            if (TryGetSupportedApplyChangesOperation(operations, out var applyChanges))
            {
                var changedDocumentCount = await CountChangedSourceDocumentsAsync(queryContext.CurrentSolution, applyChanges!.ChangedSolution);
                return new BuiltInCodeActionAuditProbe
                {
                    LocationStatus = resolution.Status,
                    VisibilityOutcome = visibilityResult.Outcome,
                    RuntimeOutcome = changedDocumentCount > 0
                        ? BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable
                        : BuiltInCodeActionRuntimeAuditOutcome.OfferedButNotReplayable,
                    MatchingActionCount = matching.Length,
                    IsVisibleInList = IsVisible(visibilityResult, auditCase),
                    CandidateTitles = discovered.Select(static action => action.Title).Distinct(StringComparer.Ordinal).OrderBy(static title => title, StringComparer.Ordinal).ToArray(),
                    FailureMessage = changedDocumentCount > 0 ? null : "The matched action did not change any source documents.",
                };
            }

            return new BuiltInCodeActionAuditProbe
            {
                LocationStatus = resolution.Status,
                VisibilityOutcome = visibilityResult.Outcome,
                RuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedButNotReplayable,
                MatchingActionCount = matching.Length,
                IsVisibleInList = IsVisible(visibilityResult, auditCase),
                CandidateTitles = discovered.Select(static action => action.Title).Distinct(StringComparer.Ordinal).OrderBy(static title => title, StringComparer.Ordinal).ToArray(),
                FailureMessage = "The matched action produced unsupported operations.",
            };
        }
        catch (Exception exception)
        {
            return new BuiltInCodeActionAuditProbe
            {
                LocationStatus = resolution.Status,
                VisibilityOutcome = visibilityResult.Outcome,
                RuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedButNotReplayable,
                MatchingActionCount = matching.Length,
                IsVisibleInList = IsVisible(visibilityResult, auditCase),
                CandidateTitles = discovered.Select(static action => action.Title).Distinct(StringComparer.Ordinal).OrderBy(static title => title, StringComparer.Ordinal).ToArray(),
                FailureMessage = exception.Message,
            };
        }
    }

    private static async Task<IReadOnlyList<DiscoveredAuditCodeAction>> DiscoverRefactoringsAsync(
        CodeRefactoringProvider provider,
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var rootActions = new List<CodeAction>();
        var context = new CodeRefactoringContext(document, span, action => rootActions.Add(action), cancellationToken);
        await provider.ComputeRefactoringsAsync(context);

        return Flatten(rootActions, GetProviderId(provider));
    }

    private static async Task<IReadOnlyList<DiscoveredAuditCodeAction>> DiscoverCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        TextSpan span,
        ImmutableArray<Diagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var matchingDiagnostics = diagnostics
            .Where(diagnostic => provider.FixableDiagnosticIds.Contains(diagnostic.Id, StringComparer.Ordinal))
            .ToImmutableArray();
        if (matchingDiagnostics.IsDefaultOrEmpty)
        {
            return [];
        }

        var discovered = new List<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics)>();
        try
        {
            await RegisterCodeFixesAsync(provider, document, span, matchingDiagnostics, discovered, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException)
        {
            discovered.Clear();
            foreach (var diagnostic in matchingDiagnostics)
            {
                await RegisterCodeFixesAsync(provider, document, diagnostic.Location.SourceSpan, [diagnostic], discovered, cancellationToken).ConfigureAwait(false);
            }
        }

        return discovered
            .SelectMany(entry => Flatten([entry.Action], GetProviderId(provider)))
            .ToArray();
    }

    private static async Task RegisterCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        TextSpan requestedSpan,
        ImmutableArray<Diagnostic> diagnostics,
        ICollection<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics)> discovered,
        CancellationToken cancellationToken)
    {
        var contextSpan = ExpandCodeFixContextSpan(requestedSpan, diagnostics);
        var context = new CodeFixContext(document, contextSpan, diagnostics, (action, actionDiagnostics) => discovered.Add((action, actionDiagnostics)), cancellationToken);
        await provider.RegisterCodeFixesAsync(context).ConfigureAwait(false);
    }

    private static TextSpan ExpandCodeFixContextSpan(TextSpan requestedSpan, ImmutableArray<Diagnostic> diagnostics)
    {
        var start = requestedSpan.Start;
        var end = requestedSpan.End;

        foreach (var diagnostic in diagnostics)
        {
            if (!diagnostic.Location.IsInSource)
            {
                continue;
            }

            start = Math.Min(start, diagnostic.Location.SourceSpan.Start);
            end = Math.Max(end, diagnostic.Location.SourceSpan.End);
        }

        return TextSpan.FromBounds(start, end);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var compilation = await document.Project.GetCompilationAsync(cancellationToken).ConfigureAwait(false);
        if (compilation is null)
        {
            return [];
        }

        var diagnostics = compilation.GetDiagnostics(cancellationToken).ToList();
        var analyzers = document.Project.AnalyzerReferences
            .SelectMany(reference => reference.GetAnalyzers(document.Project.Language))
            .ToImmutableArray();
        if (!analyzers.IsDefaultOrEmpty)
        {
            diagnostics.AddRange(await compilation
                .WithAnalyzers(analyzers, document.Project.AnalyzerOptions)
                .GetAnalyzerDiagnosticsAsync(cancellationToken)
                .ConfigureAwait(false));
        }

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken).ConfigureAwait(false);
        return diagnostics
            .Where(diagnostic => diagnostic.Location.IsInSource && diagnostic.Location.SourceTree == syntaxTree)
            .Where(diagnostic => diagnostic.Location.SourceSpan.IntersectsWith(span))
            .ToImmutableArray();
    }

    private static IReadOnlyList<DiscoveredAuditCodeAction> Flatten(
        IReadOnlyList<CodeAction> rootActions,
        string providerId)
    {
        var discovered = new List<DiscoveredAuditCodeAction>();

        for (var index = 0; index < rootActions.Count; index++)
        {
            FlattenCore(rootActions[index], providerId, [index], discovered);
        }

        return discovered;
    }

    private static void FlattenCore(
        CodeAction action,
        string providerId,
        IReadOnlyList<int> path,
        ICollection<DiscoveredAuditCodeAction> discovered)
    {
        var nested = action.NestedActions;
        if (!nested.IsDefaultOrEmpty)
        {
            for (var index = 0; index < nested.Length; index++)
            {
                FlattenCore(nested[index], providerId, path.Concat([index]).ToArray(), discovered);
            }

            return;
        }

        discovered.Add(new DiscoveredAuditCodeAction
        {
            Action = action,
            ProviderId = providerId,
            Title = action.Title,
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = path.ToArray(),
        });
    }

    private static async Task<int> CountChangedSourceDocumentsAsync(Solution before, Solution after)
    {
        var count = 0;

        foreach (var document in before.Projects.SelectMany(static project => project.Documents))
        {
            var updatedDocument = after.GetDocument(document.Id);
            if (updatedDocument is null)
            {
                continue;
            }

            var originalText = await document.GetTextAsync(CancellationToken.None);
            var updatedText = await updatedDocument.GetTextAsync(CancellationToken.None);
            if (!originalText.ContentEquals(updatedText))
            {
                count++;
            }
        }

        return count;
    }

    private static bool TryGetSupportedApplyChangesOperation(
        IReadOnlyList<CodeActionOperation> operations,
        out ApplyChangesOperation? applyChanges)
    {
        applyChanges = null;

        foreach (var operation in operations)
        {
            if (operation is ApplyChangesOperation candidate)
            {
                if (applyChanges is not null)
                {
                    applyChanges = null;
                    return false;
                }

                applyChanges = candidate;
                continue;
            }

            if (!string.Equals(
                operation.GetType().FullName,
                "Microsoft.CodeAnalysis.Wrapping.WrapItemsAction+RecordCodeActionOperation",
                StringComparison.Ordinal))
            {
                applyChanges = null;
                return false;
            }
        }

        return applyChanges is not null;
    }

    private static bool IsVisible(PluginExecutionResult<CodeActionListData> result, BuiltInCodeActionAuditCase auditCase)
    {
        return result.Data?.Actions.Any(action =>
            string.Equals(action.ProviderId, auditCase.ProviderId, StringComparison.Ordinal)
            && MatchesTitle(auditCase, action.Title)) == true;
    }

    private static bool MatchesTitle(BuiltInCodeActionAuditCase auditCase, string title)
    {
        if (!string.IsNullOrWhiteSpace(auditCase.Title))
        {
            return string.Equals(title, auditCase.Title, StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(auditCase.TitlePrefix))
        {
            return title.StartsWith(auditCase.TitlePrefix, StringComparison.Ordinal);
        }

        return true;
    }

    private static string? BuildCodeFixDiagnosticMessage(ImmutableArray<Diagnostic> diagnostics)
    {
        if (diagnostics.IsDefaultOrEmpty)
        {
            return "No diagnostics intersected the requested span.";
        }

        return "Diagnostics: " + string.Join(", ", diagnostics.Select(static diagnostic => diagnostic.Id).Distinct(StringComparer.Ordinal).OrderBy(static id => id, StringComparer.Ordinal));
    }

    private static string GetProviderId(object provider)
    {
        return provider.GetType().FullName ?? provider.GetType().Name;
    }

    private sealed record DiscoveredAuditCodeAction
    {
        public CodeAction Action { get; init; } = null!;

        public string ProviderId { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<int> ActionPath { get; init; } = [];
    }
}
