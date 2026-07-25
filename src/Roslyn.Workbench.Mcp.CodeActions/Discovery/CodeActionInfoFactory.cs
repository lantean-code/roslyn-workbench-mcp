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
        TextSpan span,
        CodeActionDescriptorEntry descriptor,
        [NotNullWhen(true)] out CodeActionInfo? info)
    {
        var expiresAt = _timeProvider.GetUtcNow().Add(_referenceLifetime);
        var recipe = new CodeActionReplayRecipe
        {
            Kind = action.Kind,
            ProviderId = action.ProviderId,
            Title = action.Title,
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = action.ActionPath.ToArray(),
            DiagnosticIds = action.DiagnosticIds.ToArray(),
            WorkspaceId = context.WorkspaceIdentity.WorkspaceId,
            WorkspaceEpoch = context.WorkspaceIdentity.WorkspaceEpoch,
            TransactionRevision = context.TransactionRevision,
            DocumentPath = context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name),
            ProjectId = document.Project.Id.Id.ToString(),
            Start = span.Start,
            Length = span.Length,
        };

        if (!_referenceStore.TryCreate(recipe, expiresAt, out var reference))
        {
            info = null;
            return false;
        }

        info = CreateFromReference(action, context, descriptor, reference);
        return true;
    }

    public CodeActionInfo CreateFromReference(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        CodeActionDescriptorEntry descriptor,
        CodeActionReference reference)
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
}
