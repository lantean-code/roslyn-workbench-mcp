namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionAnalyzerActivator
{
    CodeActionAnalyzerActivationResult Activate(string analyzerTypeName);

    CodeActionAnalyzerActivationResult Activate(Type analyzerType);
}
