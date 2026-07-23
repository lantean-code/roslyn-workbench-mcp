using Microsoft.Extensions.Options;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionInfoFactory : ICodeActionInfoFactory
{
    private readonly ICodeActionTokenService _tokenService;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _tokenLifetime;

    public CodeActionInfoFactory(
        ICodeActionTokenService tokenService,
        TimeProvider timeProvider,
        IOptions<CodeActionExecutionOptions> options)
    {
        _tokenService = tokenService;
        _timeProvider = timeProvider;
        _tokenLifetime = options.Value.TokenLifetime;
    }

    public CodeActionInfo Create(
        DiscoveredCodeAction action,
        ICodeActionExecutionContext context,
        Document document,
        TextSpan span,
        CodeActionDescriptorEntry descriptor)
    {
        var expiresAt = _timeProvider.GetUtcNow().Add(_tokenLifetime);
        var expiresAtText = expiresAt.ToString("O");
        var payload = new CodeActionTokenPayload
        {
            Kind = action.Kind.ToString(),
            ProviderId = action.ProviderId,
            Title = action.Title,
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = action.ActionPath.ToArray(),
            DiagnosticIds = action.DiagnosticIds.ToArray(),
            WorkspaceId = context.WorkspaceIdentity.WorkspaceId,
            WorkspaceEpoch = context.WorkspaceIdentity.WorkspaceEpoch,
            TransactionRevision = context.TransactionRevision,
            ExpiresAt = expiresAtText,
            DocumentPath = context.WorkspaceResolver.NormalizeDocumentPath(document.FilePath ?? document.Name),
            ProjectId = document.Project.Id.Id.ToString(),
            Start = span.Start,
            Length = span.Length,
        };

        return new CodeActionInfo
        {
            ActionId = _tokenService.Encode(payload),
            WorkspaceId = context.WorkspaceIdentity.WorkspaceId,
            Title = action.Title,
            ProviderId = action.ProviderId,
            Kind = action.Kind == DiscoveredActionKind.Refactoring ? "Refactoring" : "CodeFix",
            EquivalenceKey = action.EquivalenceKey,
            ActionPath = action.ActionPath,
            DiagnosticIds = action.DiagnosticIds,
            WorkspaceEpoch = context.WorkspaceIdentity.WorkspaceEpoch,
            TransactionRevision = context.TransactionRevision,
            ExpiresAt = expiresAtText,
            ExecutionMode = descriptor.ExecutionMode,
            ExecutorTool = descriptor.ExecutorTool,
            DescribeTool = descriptor.DescribeTool,
            UnsupportedReasonCode = descriptor.UnsupportedReasonCode,
            Requirements = descriptor.Requirements,
        };
    }
}
