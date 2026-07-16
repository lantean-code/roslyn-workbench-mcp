namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal interface ICodeActionAnalyzerActivator
{
    CodeActionAnalyzerActivationResult Activate(string analyzerTypeName);
}
