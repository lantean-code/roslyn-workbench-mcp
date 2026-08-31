using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Converts discovered actions into published items backed by short-lived replay recipes.
/// </summary>
internal sealed class CodeActionInfoFactory : ICodeActionInfoFactory
{
    private readonly ICodeActionReferenceStore _referenceStore;
    private readonly TimeProvider _timeProvider;
    private readonly int _maximumDiagnosticContextsPerAction;
    private readonly TimeSpan _referenceLifetime;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeActionInfoFactory"/> class.
    /// </summary>
    /// <param name="referenceStore">The store that retains short-lived action recipes.</param>
    /// <param name="timeProvider">The time source used for expiry and timestamp calculations.</param>
    /// <param name="options">The reference lifetime and diagnostic context limits.</param>
    public CodeActionInfoFactory(
        ICodeActionReferenceStore referenceStore,
        TimeProvider timeProvider,
        IOptions<CodeActionExecutionOptions> options)
    {
        _referenceStore = referenceStore;
        _timeProvider = timeProvider;
        _maximumDiagnosticContextsPerAction = Math.Max(0, options.Value.MaximumDiagnosticContextsPerAction);
        _referenceLifetime = options.Value.ReferenceLifetime;
    }

    /// <summary>
    /// Creates a published action item and retains the recipe needed to rediscover it.
    /// </summary>
    /// <param name="action">The discovered leaf action to publish.</param>
    /// <param name="context">The current Code Action execution context.</param>
    /// <param name="document">The document in which the action was discovered.</param>
    /// <param name="location">The canonical source location of the action.</param>
    /// <returns>The published item or a categorized reason it could not be created.</returns>
    public CodeActionInfoCreationResult Create(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        Document document,
        ResolvedLocation location)
    {
        if (location.Document is null || location.Span is null)
        {
            return CodeActionInfoCreationResult.LocationUnavailable();
        }

        var documentPath = document.FilePath ?? document.Name;
        if (!context.WorkspacePathService.TryNormalizePath(documentPath, out var normalizedDocumentPath))
        {
            return CodeActionInfoCreationResult.DocumentPathUnavailable();
        }

        var recipe = new CodeActionReplayRecipe
        {
            Kind = action.Kind,
            ProviderId = action.ProviderId,
            Title = action.Title,
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = action.ActionPath.ToArray(),
            DiagnosticIds = action.DiagnosticIds.ToArray(),
            Diagnostics = action.Diagnostics.ToArray(),
            SnapshotIdentity = context.SnapshotIdentity,
            DocumentPath = normalizedDocumentPath,
            ProjectId = document.Project.Id.Id.ToString(),
            Start = action.TargetSpan.Start,
            Length = action.TargetSpan.Length,
        };

        var expiresAt = _timeProvider.GetUtcNow().Add(_referenceLifetime);
        if (!_referenceStore.TryCreate(recipe, expiresAt, out var reference))
        {
            return CodeActionInfoCreationResult.ReferenceCapacityExceeded();
        }

        var kind = CodeActionKind.CodeFix;
        if (action.Kind == DiscoveredActionKind.Refactoring)
        {
            kind = CodeActionKind.Refactoring;
        }

        var actionLocation = new CodeActionLocation
        {
            Document = location.Document,
            Span = location.Span,
            Line = location.Line,
            Column = location.Column,
        };

        BoundedCollection<CodeActionDiagnosticContext>? diagnosticContexts = null;
        if (action.Kind == DiscoveredActionKind.CodeFix)
        {
            diagnosticContexts = CreateDiagnosticContexts(
                action.Diagnostics,
                _maximumDiagnosticContextsPerAction);
        }

        IReadOnlyList<CodeActionFixAllScope>? fixAllScopes = null;
        if (action.FixAllScopes.Count > 0)
        {
            fixAllScopes = action.FixAllScopes;
        }

        var item = new CodeActionListItem
        {
            ActionId = reference.ActionId,
            Title = action.Title,
            Kind = kind,
            Location = actionLocation,
            Diagnostics = diagnosticContexts,
            FixAllScopes = fixAllScopes,
        };

        return CodeActionInfoCreationResult.Success(item);
    }

    private static BoundedCollection<CodeActionDiagnosticContext> CreateDiagnosticContexts(
        IReadOnlyList<CodeActionDiagnosticIdentity> diagnostics,
        int maximumDiagnosticContexts)
    {
        var returnedCount = Math.Min(diagnostics.Count, maximumDiagnosticContexts);
        var contexts = new List<CodeActionDiagnosticContext>(returnedCount);
        foreach (var diagnostic in diagnostics)
        {
            if (contexts.Count == maximumDiagnosticContexts)
            {
                break;
            }

            contexts.Add(new CodeActionDiagnosticContext
            {
                Id = diagnostic.Id,
                Message = diagnostic.Message,
            });
        }

        return BoundedCollection.CreatePrebounded(contexts, diagnostics.Count);
    }
}
