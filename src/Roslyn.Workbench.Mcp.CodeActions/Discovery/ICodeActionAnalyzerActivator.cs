namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionAnalyzerActivator
{
    CodeActionAnalyzerActivationResult Activate(Type analyzerType);
}
