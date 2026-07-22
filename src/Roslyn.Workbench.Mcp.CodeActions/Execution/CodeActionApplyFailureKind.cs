namespace Roslyn.Workbench.Mcp.CodeActions.Execution;

internal enum CodeActionApplyFailureKind
{
    UnsupportedActionOperation,
    FixAllUnavailable,
    ActionExpired,
    InvalidRequest,
    DocumentNotFound,
    ProjectNotFound,
    CodeFixUnavailable,
    ActionAmbiguous,
}
