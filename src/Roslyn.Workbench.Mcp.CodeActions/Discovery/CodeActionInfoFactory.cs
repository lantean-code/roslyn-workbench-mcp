using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionInfoFactory : ICodeActionInfoFactory
{
    private readonly ICodeActionReferenceStore _referenceStore;
    private readonly TimeProvider _timeProvider;
    private readonly int _maximumDiagnosticContextsPerAction;
    private readonly TimeSpan _referenceLifetime;

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

    public bool TryCreate(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        Document document,
        ResolvedLocation location,
        [NotNullWhen(true)] out CodeActionListItem? item)
    {
        if (location.Document is null || location.Span is null)
        {
            item = null;
            return false;
        }

        var documentPath = document.FilePath ?? document.Name;
        if (!context.WorkspacePathService.TryNormalizePath(documentPath, out var normalizedDocumentPath))
        {
            item = null;
            return false;
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
            item = null;
            return false;
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

        item = new CodeActionListItem
        {
            ActionId = reference.ActionId,
            Title = action.Title,
            Kind = kind,
            Location = actionLocation,
            Diagnostics = diagnosticContexts,
            FixAllScopes = fixAllScopes,
        };

        return true;
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
