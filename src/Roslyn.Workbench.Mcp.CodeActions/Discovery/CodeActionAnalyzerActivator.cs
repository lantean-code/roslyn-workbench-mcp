using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionAnalyzerActivator : ICodeActionAnalyzerActivator
{
    public CodeActionAnalyzerActivationResult Activate(Type analyzerType)
    {
        if (!typeof(DiagnosticAnalyzer).IsAssignableFrom(analyzerType))
        {
            return CodeActionAnalyzerActivationResult.IncompatibleType();
        }

        return CreateAnalyzer(analyzerType);
    }

    private static CodeActionAnalyzerActivationResult CreateAnalyzer(Type analyzerType)
    {
        try
        {
            if (Activator.CreateInstance(analyzerType, nonPublic: true) is not DiagnosticAnalyzer analyzer)
            {
                return CodeActionAnalyzerActivationResult.ConstructionFailed();
            }
            else
            {
                return CodeActionAnalyzerActivationResult.Available(analyzer);
            }
        }
        catch (Exception exception) when (IsExpectedConstructionFailure(exception))
        {
            // Optional runtime analyzers can have inaccessible or failing constructors; absence is an expected outcome.
            return CodeActionAnalyzerActivationResult.ConstructionFailed();
        }
    }

    private static bool IsExpectedConstructionFailure(Exception exception)
    {
        return exception is ArgumentException
            or MemberAccessException
            or MissingMethodException
            or NotSupportedException
            or SecurityException
            or TargetInvocationException
            or TypeLoadException;
    }
}
