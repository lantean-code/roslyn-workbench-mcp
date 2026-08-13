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
        var composition = CodeActionCompositionFactory.Create(new CodeActionCompositionOptions
        {
            IncludeBuiltInAssemblies = true,
        });

        await using var coordinator = ComponentWorkspace.Create(
            new ComponentWorkspaceOptions
            {
                Boundary = ComponentWorkspaceBoundary.CodeActions,
                IncludeBuiltInCodeActions = true,
            },
            composition);

        var session = new CodeActionComponentTestSession(coordinator);
        var open = await coordinator.OpenAsync(fixture.ProjectPath, TestContext.Current.CancellationToken);
        var expectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open);
        var location = auditCase.LocationFactory(fixture);
        await using var queryLease = coordinator.CodeActionContextFactory.CreateQueryContext(
            CreateListRequest(location, auditCase.Kind, expectedSnapshot),
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
            var provider = composition.CodeFixProviders.Single(
                candidate => string.Equals(CodeActionProviderIdentity.GetId(candidate), auditCase.ProviderId, StringComparison.Ordinal));

            IReadOnlyList<string> requestedDiagnosticIds = provider.FixableDiagnosticIds;
            if (auditCase.ExpectedDiagnosticId is not null)
            {
                requestedDiagnosticIds = [auditCase.ExpectedDiagnosticId];
            }

            var diagnosticService = coordinator.GetRequiredService<ICodeActionDiagnosticService>();
            codeFixDiagnostics = (await diagnosticService.GetDocumentDiagnosticsAsync(
                document,
                resolution.Value.SourceSpan,
                requestedDiagnosticIds,
                TestContext.Current.CancellationToken)).ToImmutableArray();

            discovered = await DiscoverCodeFixesAsync(
                provider,
                document,
                codeFixDiagnostics,
                TestContext.Current.CancellationToken);
        }
        else
        {
            var provider = composition.RefactoringProviders.Single(
                candidate => string.Equals(CodeActionProviderIdentity.GetId(candidate), auditCase.ProviderId, StringComparison.Ordinal));

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
            CreateListRequest(location, auditCase.Kind, expectedSnapshot),
            TestContext.Current.CancellationToken);

        if (matching.Length == 0)
        {
            string? failureMessage = null;
            if (auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix)
            {
                failureMessage = BuildCodeFixDiagnosticMessage(diagnosticIds);
            }

            return new BuiltInCodeActionAuditProbe
            {
                LocationStatus = resolution.Status,
                VisibilityOutcome = visibilityResult.Outcome,
                RuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.NotOffered,
                MatchingActionCount = 0,
                IsVisibleInList = IsVisible(visibilityResult, auditCase),
                CandidateTitles = candidateTitles,
                DiagnosticIds = diagnosticIds,
                FailureMessage = failureMessage,
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
                var (changedDocumentCount, expectedChangeFound, unexpectedChangeRemoved, changedSource) = await InspectChangedSourceDocumentsAsync(
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
                        unexpectedChangeRemoved,
                        changedSource),
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

        return Flatten(rootActions, CodeActionProviderIdentity.GetId(provider));
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
            .SelectMany(entry => Flatten([entry.Action], CodeActionProviderIdentity.GetId(provider)))
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

    private static async Task<(int ChangedDocumentCount, bool ExpectedChangeFound, bool UnexpectedChangeRemoved, string? ChangedSource)> InspectChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        BuiltInCodeActionAuditCase auditCase)
    {
        var count = 0;
        var expectedChangedText = auditCase.ExpectedChangedText?.ReplaceLineEndings("\n");
        var unexpectedChangedText = auditCase.UnexpectedChangedText?.ReplaceLineEndings("\n");
        var expectedChangeFound = string.IsNullOrWhiteSpace(expectedChangedText);
        var unexpectedChangeRemoved = string.IsNullOrWhiteSpace(unexpectedChangedText);
        string? changedSource = null;

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
                var normalizedUpdatedSource = updatedSource.ReplaceLineEndings("\n");
                changedSource ??= updatedSource;

                if (!string.IsNullOrWhiteSpace(expectedChangedText)
                    && normalizedUpdatedSource.Contains(expectedChangedText, StringComparison.Ordinal))
                {
                    expectedChangeFound = true;
                }

                if (!string.IsNullOrWhiteSpace(unexpectedChangedText)
                    && !normalizedUpdatedSource.Contains(unexpectedChangedText, StringComparison.Ordinal))
                {
                    unexpectedChangeRemoved = true;
                }
            }
        }

        return (count, expectedChangeFound, unexpectedChangeRemoved, changedSource);
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
        return result.Data?.Actions.Items.Any(action =>
            action.Kind == (auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix
                ? CodeActionKind.CodeFix
                : CodeActionKind.Refactoring)
            && MatchesTitle(auditCase, action.Title)) == true;
    }

    private static ListCodeActionsRequest CreateListRequest(
        LocationSelector location,
        BuiltInCodeActionAuditKind kind,
        SnapshotPrecondition expectedSnapshot)
    {
        var span = location.Span
            ?? throw new InvalidOperationException("The audit location must be span-backed.");

        var document = span.Document
            ?? throw new InvalidOperationException("The audit location must identify a document.");

        var range = new TextSpanRange
        {
            Start = span.Start,
            Length = span.Length,
        };

        return new ListCodeActionsRequest
        {
            Document = document,
            Range = range,
            ExpectedSnapshot = expectedSnapshot,
            Kinds = kind == BuiltInCodeActionAuditKind.CodeFix
                ? CodeActionKindSelection.CodeFixes
                : CodeActionKindSelection.Refactorings,
        };
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
        bool unexpectedChangeRemoved,
        string? changedSource)
    {
        if (changedDocumentCount == 0)
        {
            return "The matched action did not change any source documents.";
        }

        if (!expectedChangeFound)
        {
            return $"The matched action did not produce the expected source text. Changed source:{Environment.NewLine}{changedSource}";
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

    private sealed record DiscoveredAuditCodeAction
    {
        public required CodeAction Action { get; init; }

        public required string ProviderId { get; init; }

        public required string Title { get; init; }

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<int> ActionPath { get; init; } = [];
    }
}
