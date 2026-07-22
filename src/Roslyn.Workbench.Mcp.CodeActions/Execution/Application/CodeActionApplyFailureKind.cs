namespace Roslyn.Workbench.Mcp.CodeActions.Execution.Application;

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
