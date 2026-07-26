using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.CodeActions.Test;

internal sealed record BuiltInCodeActionAuditProbe
{
    public SelectorResolveStatus LocationStatus { get; init; }

    public CodeActionExecutionOutcome VisibilityOutcome { get; init; }

    public BuiltInCodeActionRuntimeAuditOutcome RuntimeOutcome { get; init; }

    public int MatchingActionCount { get; init; }

    public bool IsVisibleInList { get; init; }

    public IReadOnlyList<string> CandidateTitles { get; init; } = [];

    public IReadOnlyList<string> DiagnosticIds { get; init; } = [];

    public string? FailureMessage { get; init; }
}

internal static class BuiltInCodeActionAuditHarness
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "The compatibility audit deliberately probes arbitrary built-in Roslyn providers and records provider-defined runtime failures as audit outcomes while allowing cancellation to propagate.")]
    public static async Task<BuiltInCodeActionAuditProbe> ProbeAsync(BuiltInCodeActionAuditCase auditCase)
    {
        using var fixture = auditCase.FixtureFactory();
        var providerCatalog = CodeActionProviderCatalogFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        await using var coordinator = ComponentWorkspace.Create(
            new ComponentWorkspaceOptions
            {
                Boundary = ComponentWorkspaceBoundary.CodeActions,
            },
            providerCatalog);

        var session = new CodeActionComponentTestSession(coordinator);
        var openResult = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var location = auditCase.LocationFactory(fixture);
        await using var queryLease = coordinator.CodeActionContextFactory.CreateQueryContext(
            new ListCodeActionsRequest
            {
                Location = location,
            },
            TestContext.Current.CancellationToken);

        var queryContext = queryLease.Context!;
        var resolution = await queryContext.WorkspaceResolver.ResolveLocationAsync(location, TestContext.Current.CancellationToken);
        if (resolution.Status != SelectorResolveStatus.Resolved || resolution.Value is null)
        {
            return new BuiltInCodeActionAuditProbe
            {
                LocationStatus = resolution.Status,
                VisibilityOutcome = CodeActionExecutionOutcome.Rejected,
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
                VisibilityOutcome = CodeActionExecutionOutcome.Rejected,
                RuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.NotOffered,
                FailureMessage = "The resolved location did not map to a source document.",
            };
        }

        IReadOnlyList<DiscoveredAuditCodeAction> discovered;
        ImmutableArray<Diagnostic> codeFixDiagnostics = [];
        if (auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix)
        {
            codeFixDiagnostics = await GetDocumentDiagnosticsAsync(document, resolution.Value.SourceSpan, TestContext.Current.CancellationToken);

            var provider = providerCatalog.CodeFixProviders.Single(
                candidate => string.Equals(GetProviderId(candidate), auditCase.ProviderId, StringComparison.Ordinal));

            discovered = await DiscoverCodeFixesAsync(
                provider,
                document,
                codeFixDiagnostics,
                TestContext.Current.CancellationToken);
        }
        else
        {
            var provider = providerCatalog.RefactoringProviders.Single(
                candidate => string.Equals(GetProviderId(candidate), auditCase.ProviderId, StringComparison.Ordinal));

            discovered = await DiscoverRefactoringsAsync(
                provider,
                document,
                resolution.Value.SourceSpan,
                TestContext.Current.CancellationToken);
        }

        var candidateTitles = GetCandidateTitles(discovered);
        var diagnosticIds = GetDiagnosticIds(codeFixDiagnostics);

        var matching = discovered
            .Where(action => MatchesTitle(auditCase, action.Title))
            .Where(action => auditCase.ActionPath.Count == 0 || action.ActionPath.SequenceEqual(auditCase.ActionPath))
            .ToArray();

        var visibilityResult = await session.ListAsync(
            new ListCodeActionsRequest
            {
                Location = location,
                IncludeCodeFixes = auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix,
                IncludeRefactorings = auditCase.Kind == BuiltInCodeActionAuditKind.Refactoring,
                ExpectedSnapshot = new SnapshotPrecondition
                {
                    WorkspaceEpoch = openResult.Context.WorkspaceEpoch!.Value,
                },
            },
            TestContext.Current.CancellationToken);

        if (matching.Length == 0)
        {
            return new BuiltInCodeActionAuditProbe
            {
                LocationStatus = resolution.Status,
                VisibilityOutcome = visibilityResult.Outcome,
                RuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.NotOffered,
                MatchingActionCount = 0,
                IsVisibleInList = IsVisible(visibilityResult, auditCase),
                CandidateTitles = candidateTitles,
                DiagnosticIds = diagnosticIds,
                FailureMessage = auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix
                    ? BuildCodeFixDiagnosticMessage(diagnosticIds)
                    : null,
            };
        }

        try
        {
            var action = matching[0].Action;
            var progress = new Progress<CodeAnalysisProgress>();
            var operations = await action.GetOperationsAsync(
                queryContext.CurrentSolution,
                progress,
                TestContext.Current.CancellationToken);

            if (TryGetSupportedApplyChangesOperation(operations, out var applyChanges))
            {
                var (changedDocumentCount, expectedChangeFound, unexpectedChangeRemoved) = await InspectChangedSourceDocumentsAsync(
                    queryContext.CurrentSolution,
                    applyChanges!.ChangedSolution,
                    auditCase);

                var isExpectedMutation = changedDocumentCount > 0
                    && expectedChangeFound
                    && unexpectedChangeRemoved;

                return new BuiltInCodeActionAuditProbe
                {
                    LocationStatus = resolution.Status,
                    VisibilityOutcome = visibilityResult.Outcome,
                    RuntimeOutcome = isExpectedMutation
                        ? BuiltInCodeActionRuntimeAuditOutcome.OfferedAndReplayable
                        : BuiltInCodeActionRuntimeAuditOutcome.OfferedButNotReplayable,
                    MatchingActionCount = matching.Length,
                    IsVisibleInList = IsVisible(visibilityResult, auditCase),
                    CandidateTitles = candidateTitles,
                    DiagnosticIds = diagnosticIds,
                    FailureMessage = BuildMutationFailureMessage(
                        changedDocumentCount,
                        expectedChangeFound,
                        unexpectedChangeRemoved),
                };
            }

            return new BuiltInCodeActionAuditProbe
            {
                LocationStatus = resolution.Status,
                VisibilityOutcome = visibilityResult.Outcome,
                RuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedButNotReplayable,
                MatchingActionCount = matching.Length,
                IsVisibleInList = IsVisible(visibilityResult, auditCase),
                CandidateTitles = candidateTitles,
                DiagnosticIds = diagnosticIds,
                FailureMessage = "The matched action produced unsupported operations.",
            };
        }
        catch (OperationCanceledException)
        {
            throw;
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
                CandidateTitles = candidateTitles,
                DiagnosticIds = diagnosticIds,
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
        foreach (var diagnosticGroup in matchingDiagnostics.GroupBy(static diagnostic => diagnostic.Location.SourceSpan))
        {
            await RegisterCodeFixesAsync(
                provider,
                document,
                diagnosticGroup.Key,
                diagnosticGroup.ToImmutableArray(),
                discovered,
                cancellationToken);
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
        List<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics)> discovered,
        CancellationToken cancellationToken)
    {
        var context = new CodeFixContext(document, requestedSpan, diagnostics, (action, actionDiagnostics) => discovered.Add((action, actionDiagnostics)), cancellationToken);
        await provider.RegisterCodeFixesAsync(context);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDocumentDiagnosticsAsync(
        Document document,
        TextSpan span,
        CancellationToken cancellationToken)
    {
        var compilation = await document.Project.GetCompilationAsync(cancellationToken);
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
                );
        }

        var syntaxTree = await document.GetSyntaxTreeAsync(cancellationToken);
        return diagnostics
            .Where(diagnostic => diagnostic.Location.IsInSource && diagnostic.Location.SourceTree == syntaxTree)
            .Where(diagnostic => diagnostic.Location.SourceSpan.IntersectsWith(span))
            .ToImmutableArray();
    }

    private static List<DiscoveredAuditCodeAction> Flatten(
        List<CodeAction> rootActions,
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

    private static async Task<(int ChangedDocumentCount, bool ExpectedChangeFound, bool UnexpectedChangeRemoved)> InspectChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        BuiltInCodeActionAuditCase auditCase)
    {
        var count = 0;
        var expectedChangeFound = string.IsNullOrWhiteSpace(auditCase.ExpectedChangedText);
        var unexpectedChangeRemoved = string.IsNullOrWhiteSpace(auditCase.UnexpectedChangedText);

        foreach (var document in before.Projects.SelectMany(static project => project.Documents))
        {
            var updatedDocument = after.GetDocument(document.Id);
            if (updatedDocument is null)
            {
                continue;
            }

            var originalText = await document.GetTextAsync(TestContext.Current.CancellationToken);
            var updatedText = await updatedDocument.GetTextAsync(TestContext.Current.CancellationToken);
            if (!originalText.ContentEquals(updatedText))
            {
                count++;

                var updatedSource = updatedText.ToString();
                if (!string.IsNullOrWhiteSpace(auditCase.ExpectedChangedText)
                    && updatedSource.Contains(auditCase.ExpectedChangedText, StringComparison.Ordinal))
                {
                    expectedChangeFound = true;
                }

                if (!string.IsNullOrWhiteSpace(auditCase.UnexpectedChangedText)
                    && !updatedSource.Contains(auditCase.UnexpectedChangedText, StringComparison.Ordinal))
                {
                    unexpectedChangeRemoved = true;
                }
            }
        }

        return (count, expectedChangeFound, unexpectedChangeRemoved);
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

    private static bool IsVisible(CodeActionExecutionResult<CodeActionListData> result, BuiltInCodeActionAuditCase auditCase)
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

    private static string? BuildCodeFixDiagnosticMessage(string[] diagnosticIds)
    {
        if (diagnosticIds.Length == 0)
        {
            return "No diagnostics intersected the requested span.";
        }

        return "Diagnostics: " + string.Join(", ", diagnosticIds);
    }

    private static string? BuildMutationFailureMessage(
        int changedDocumentCount,
        bool expectedChangeFound,
        bool unexpectedChangeRemoved)
    {
        if (changedDocumentCount == 0)
        {
            return "The matched action did not change any source documents.";
        }

        if (!expectedChangeFound)
        {
            return "The matched action did not produce the expected source text.";
        }

        if (!unexpectedChangeRemoved)
        {
            return "The matched action retained source text that should have been removed.";
        }

        return null;
    }

    private static string[] GetDiagnosticIds(ImmutableArray<Diagnostic> diagnostics)
    {
        return diagnostics
            .Select(static diagnostic => diagnostic.Id)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static diagnosticId => diagnosticId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] GetCandidateTitles(IReadOnlyList<DiscoveredAuditCodeAction> discovered)
    {
        return discovered
            .Select(static action => action.Title)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static title => title, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetProviderId(object provider)
    {
        var providerType = provider.GetType();
        return providerType.FullName ?? providerType.Name;
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
