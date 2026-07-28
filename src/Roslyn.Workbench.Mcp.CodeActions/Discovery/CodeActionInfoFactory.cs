using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionInfoFactory : ICodeActionInfoFactory
{
    private readonly ICodeActionReferenceStore _referenceStore;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _referenceLifetime;

    public CodeActionInfoFactory(
        ICodeActionReferenceStore referenceStore,
        TimeProvider timeProvider,
        IOptions<CodeActionExecutionOptions> options)
    {
        _referenceStore = referenceStore;
        _timeProvider = timeProvider;
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

        var expiresAt = _timeProvider.GetUtcNow().Add(_referenceLifetime);
        var recipe = new CodeActionReplayRecipe
        {
            Kind = action.Kind,
            ProviderId = action.ProviderId,
            Title = action.Title,
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = action.ActionPath.ToArray(),
            DiagnosticIds = action.DiagnosticIds.ToArray(),
            Diagnostics = action.Diagnostics.ToArray(),
            WorkspaceId = context.WorkspaceIdentity.WorkspaceId,
            WorkspaceEpoch = context.WorkspaceIdentity.WorkspaceEpoch,
            TransactionRevision = context.TransactionRevision,
            DocumentPath = context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name),
            ProjectId = document.Project.Id.Id.ToString(),
            Start = action.TargetSpan.Start,
            Length = action.TargetSpan.Length,
        };

        if (!_referenceStore.TryCreate(recipe, expiresAt, out var reference))
        {
            item = null;
            return false;
        }

        item = new CodeActionListItem
        {
            ActionId = reference.ActionId,
            Title = action.Title,
            Kind = action.Kind == DiscoveredActionKind.Refactoring
                ? CodeActionKind.Refactoring
                : CodeActionKind.CodeFix,
            Location = new CodeActionLocation
            {
                Document = location.Document,
                Span = location.Span,
                Line = location.Line,
                Column = location.Column,
            },
            Diagnostics = action.Kind == DiscoveredActionKind.CodeFix
                ? CreateDiagnosticContexts(action.Diagnostics)
                : null,
            FixAllScopes = action.FixAllScopes.Count == 0
                ? null
                : action.FixAllScopes,
        };

        return true;
    }

    public CodeActionInfo CreateFromReference(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        CodeActionDescriptorEntry descriptor,
        CodeActionReference reference,
        ResolvedLocation location)
    {
        var info = new CodeActionInfo
        {
            ActionId = reference.ActionId,
            WorkspaceId = context.WorkspaceIdentity.WorkspaceId,
            Title = action.Title,
            ProviderId = action.ProviderId,
            Kind = action.Kind == DiscoveredActionKind.Refactoring ? "Refactoring" : "CodeFix",
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = action.ActionPath,
            DiagnosticIds = action.DiagnosticIds,
            Location = location,
            WorkspaceEpoch = context.WorkspaceIdentity.WorkspaceEpoch,
            TransactionRevision = context.TransactionRevision,
            ExpiresAt = reference.ExpiresAt.ToString("O"),
            ExecutionMode = descriptor.ExecutionMode,
            ExecutorTool = descriptor.ExecutorTool,
            DescribeTool = descriptor.DescribeTool,
            UnsupportedReasonCode = descriptor.UnsupportedReasonCode,
            Requirements = descriptor.Requirements,
        };

        return info;
    }

    private static List<CodeActionDiagnosticContext> CreateDiagnosticContexts(
        IReadOnlyList<CodeActionDiagnosticIdentity> diagnostics)
    {
        var contexts = new List<CodeActionDiagnosticContext>(diagnostics.Count);
        foreach (var diagnostic in diagnostics)
        {
            contexts.Add(new CodeActionDiagnosticContext
            {
                Id = diagnostic.Id,
                Message = diagnostic.Message,
            });
        }

        return contexts;
    }
}
