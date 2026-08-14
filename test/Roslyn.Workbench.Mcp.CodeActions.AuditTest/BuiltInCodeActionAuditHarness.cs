using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Roslyn.Workbench.Mcp.CodeActions.Execution.Application;
using Roslyn.Workbench.Mcp.CodeActions.References;
using Roslyn.Workbench.Mcp.Workspace.Results;

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
        await coordinator.StartTransactionAsync(TestContext.Current.CancellationToken);
        var expectedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(open, transactionRevision: 0);
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
            var originalSolution = queryContext.CurrentSolution;
            var referenceStore = coordinator.GetRequiredService<ICodeActionReferenceStore>();
            var (actionId, selectionFailure) = ResolvePublishedAction(
                visibilityResult,
                matching[0],
                auditCase,
                referenceStore);

            if (actionId is null)
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
                    FailureMessage = selectionFailure,
                };
            }

            await queryLease.DisposeAsync();

            var stageRequest = new StageCodeActionRequest
            {
                ActionId = actionId.Value,
                ExpectedSnapshot = expectedSnapshot,
            };

            var staged = await session.StageCodeActionAsync(
                stageRequest,
                TestContext.Current.CancellationToken);

            if (!staged.IsSucceeded || staged.Data.Transaction is null)
            {
                return CreateReplayFailure(
                    resolution.Status,
                    visibilityResult,
                    matching.Length,
                    auditCase,
                    candidateTitles,
                    diagnosticIds,
                    BuildStagingFailureMessage(staged));
            }

            if (referenceStore.TryGet(actionId.Value, out _))
            {
                return CreateReplayFailure(
                    resolution.Status,
                    visibilityResult,
                    matching.Length,
                    auditCase,
                    candidateTitles,
                    diagnosticIds,
                    "Successful production staging did not consume the replay reference.");
            }

            var stagedSnapshot = BundledComponentWorkspaceFactory.CreateSnapshot(
                open,
                staged.Data.Transaction.Revision);

            await using var stagedQueryLease = coordinator.CodeActionContextFactory.CreateQueryContext(
                CreateListRequest(location, auditCase.Kind, stagedSnapshot),
                TestContext.Current.CancellationToken);

            if (stagedQueryLease.HasFailure)
            {
                return CreateReplayFailure(
                    resolution.Status,
                    visibilityResult,
                    matching.Length,
                    auditCase,
                    candidateTitles,
                    diagnosticIds,
                    "The staged transaction revision could not be reacquired for source inspection.");
            }

            var stagedSolution = stagedQueryLease.Context.CurrentSolution;
            var solutionChangeCounter = coordinator.GetRequiredService<ICodeActionSolutionChangeCounter>();
            var (changedDocumentCount, expectedChangeFound, unexpectedChangeRemoved, changedSource) = await InspectChangedSourceDocumentsAsync(
                originalSolution,
                stagedSolution,
                auditCase,
                solutionChangeCounter);

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

        return Flatten(rootActions, CodeActionProviderIdentity.GetId(provider), span);
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

        var discovered = new List<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics, TextSpan TargetSpan)>();
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
            .SelectMany(entry => Flatten([entry.Action], CodeActionProviderIdentity.GetId(provider), entry.TargetSpan))
            .ToArray();
    }

    private static async Task RegisterCodeFixesAsync(
        CodeFixProvider provider,
        Document document,
        TextSpan requestedSpan,
        ImmutableArray<Diagnostic> diagnostics,
        List<(CodeAction Action, ImmutableArray<Diagnostic> Diagnostics, TextSpan TargetSpan)> discovered,
        CancellationToken cancellationToken)
    {
        var context = new CodeFixContext(
            document,
            requestedSpan,
            diagnostics,
            (action, actionDiagnostics) => discovered.Add((action, actionDiagnostics, requestedSpan)),
            cancellationToken);

        await provider.RegisterCodeFixesAsync(context);
    }

    private static List<DiscoveredAuditCodeAction> Flatten(
        List<CodeAction> rootActions,
        string providerId,
        TextSpan targetSpan)
    {
        var discovered = new List<DiscoveredAuditCodeAction>();

        for (var index = 0; index < rootActions.Count; index++)
        {
            FlattenCore(rootActions[index], providerId, targetSpan, [index], discovered);
        }

        return discovered;
    }

    private static void FlattenCore(
        CodeAction action,
        string providerId,
        TextSpan targetSpan,
        IReadOnlyList<int> path,
        ICollection<DiscoveredAuditCodeAction> discovered)
    {
        var nested = action.NestedActions;
        if (!nested.IsDefaultOrEmpty)
        {
            for (var index = 0; index < nested.Length; index++)
            {
                FlattenCore(nested[index], providerId, targetSpan, path.Concat([index]).ToArray(), discovered);
            }

            return;
        }

        discovered.Add(new DiscoveredAuditCodeAction
        {
            Action = action,
            ProviderId = providerId,
            Title = action.Title,
            TargetSpan = targetSpan,
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = path.ToArray(),
        });
    }

    private static async Task<(int ChangedDocumentCount, bool ExpectedChangeFound, bool UnexpectedChangeRemoved, string? ChangedSource)> InspectChangedSourceDocumentsAsync(
        Solution before,
        Solution after,
        BuiltInCodeActionAuditCase auditCase,
        ICodeActionSolutionChangeCounter solutionChangeCounter)
    {
        var expectedChangedText = auditCase.ExpectedChangedText?.ReplaceLineEndings("\n");
        var unexpectedChangedText = auditCase.UnexpectedChangedText?.ReplaceLineEndings("\n");
        var expectedChangeFound = string.IsNullOrWhiteSpace(expectedChangedText);
        var unexpectedChangeRemoved = string.IsNullOrWhiteSpace(unexpectedChangedText);
        string? changedSource = null;
        var changedDocuments = await solutionChangeCounter.GetChangedSourceDocumentsAsync(
            before,
            after,
            TestContext.Current.CancellationToken);

        foreach (var documentId in changedDocuments.Select(static document => document.Id))
        {
            var originalDocument = before.GetDocument(documentId);
            var updatedDocument = after.GetDocument(documentId);
            string? normalizedOriginalSource = null;
            string? normalizedUpdatedSource = null;

            if (originalDocument is not null)
            {
                var originalText = await originalDocument.GetTextAsync(TestContext.Current.CancellationToken);
                normalizedOriginalSource = originalText.ToString().ReplaceLineEndings("\n");
            }

            if (updatedDocument is not null)
            {
                var updatedText = await updatedDocument.GetTextAsync(TestContext.Current.CancellationToken);
                var updatedSource = updatedText.ToString();
                normalizedUpdatedSource = updatedSource.ReplaceLineEndings("\n");
                changedSource ??= updatedSource;

                if (!string.IsNullOrWhiteSpace(expectedChangedText)
                    && normalizedUpdatedSource.Contains(expectedChangedText, StringComparison.Ordinal))
                {
                    expectedChangeFound = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(unexpectedChangedText)
                && normalizedOriginalSource?.Contains(unexpectedChangedText, StringComparison.Ordinal) == true
                && normalizedUpdatedSource?.Contains(unexpectedChangedText, StringComparison.Ordinal) != true)
            {
                unexpectedChangeRemoved = true;
            }
        }

        return (changedDocuments.Count, expectedChangeFound, unexpectedChangeRemoved, changedSource);
    }

    private static (Guid? ActionId, string? FailureMessage) ResolvePublishedAction(
        CodeActionExecutionResult<CodeActionListData> result,
        DiscoveredAuditCodeAction selectedAction,
        BuiltInCodeActionAuditCase auditCase,
        ICodeActionReferenceStore referenceStore)
    {
        var expectedKind = auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix
            ? CodeActionKind.CodeFix
            : CodeActionKind.Refactoring;

        var matchingItems = new List<CodeActionListItem>();
        foreach (var item in result.Data?.Actions.Items ?? [])
        {
            if (item.Kind != expectedKind
                || !string.Equals(item.Title, selectedAction.Title, StringComparison.Ordinal)
                || item.Location.Span.Start != selectedAction.TargetSpan.Start
                || item.Location.Span.Length != selectedAction.TargetSpan.Length)
            {
                continue;
            }

            if (auditCase.ExpectedDiagnosticId is not null
                && item.Diagnostics?.Items.Any(diagnostic => string.Equals(
                    diagnostic.Id,
                    auditCase.ExpectedDiagnosticId,
                    StringComparison.Ordinal)) != true)
            {
                continue;
            }

            matchingItems.Add(item);
        }

        var expectedDiscoveredKind = auditCase.Kind == BuiltInCodeActionAuditKind.CodeFix
            ? DiscoveredActionKind.CodeFix
            : DiscoveredActionKind.Refactoring;

        var matchingReferences = new List<CodeActionListItem>();
        foreach (var item in matchingItems)
        {
            if (!referenceStore.TryGet(item.ActionId, out var reference))
            {
                return (null, "A matching production list item did not retain a replay reference.");
            }

            var recipe = reference.Recipe;
            if (recipe.Kind == expectedDiscoveredKind
                && string.Equals(recipe.ProviderId, auditCase.ProviderId, StringComparison.Ordinal)
                && string.Equals(recipe.Title, selectedAction.Title, StringComparison.Ordinal)
                && string.Equals(recipe.EquivalenceKey, selectedAction.EquivalenceKey, StringComparison.Ordinal)
                && recipe.ActionPath.SequenceEqual(selectedAction.ActionPath)
                && recipe.Start == selectedAction.TargetSpan.Start
                && recipe.Length == selectedAction.TargetSpan.Length
                && (auditCase.ExpectedDiagnosticId is null
                    || recipe.DiagnosticIds.Contains(auditCase.ExpectedDiagnosticId, StringComparer.Ordinal)))
            {
                matchingReferences.Add(item);
            }
        }

        if (matchingReferences.Count != 1)
        {
            return (null, $"Production listing retained {matchingReferences.Count} replay references for the matched provider leaf; exactly one was required.");
        }

        return (matchingReferences[0].ActionId, null);
    }

    private static BuiltInCodeActionAuditProbe CreateReplayFailure(
        SelectorResolveStatus locationStatus,
        CodeActionExecutionResult<CodeActionListData> visibilityResult,
        int matchingActionCount,
        BuiltInCodeActionAuditCase auditCase,
        IReadOnlyList<string> candidateTitles,
        IReadOnlyList<string> diagnosticIds,
        string failureMessage)
    {
        return new BuiltInCodeActionAuditProbe
        {
            LocationStatus = locationStatus,
            VisibilityOutcome = visibilityResult.Outcome,
            RuntimeOutcome = BuiltInCodeActionRuntimeAuditOutcome.OfferedButNotReplayable,
            MatchingActionCount = matchingActionCount,
            IsVisibleInList = IsVisible(visibilityResult, auditCase),
            CandidateTitles = candidateTitles,
            DiagnosticIds = diagnosticIds,
            FailureMessage = failureMessage,
        };
    }

    private static string BuildStagingFailureMessage(CodeActionExecutionResult<MutationData> staged)
    {
        if (staged.Error is not null)
        {
            return $"Production staging returned '{staged.Error.Code}': {staged.Error.Message}";
        }

        return $"Production staging returned '{staged.Outcome}'.";
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

        public required TextSpan TargetSpan { get; init; }

        public string? EquivalenceKey { get; init; }

        public IReadOnlyList<int> ActionPath { get; init; } = [];
    }
}
