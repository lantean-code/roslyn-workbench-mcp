using System.Reflection;
using System.Security;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

/// <summary>
/// Validates and constructs diagnostic analyzer types used for Code Fix discovery.
/// </summary>
internal sealed class CodeActionAnalyzerActivator : ICodeActionAnalyzerActivator
{
    /// <summary>
    /// Creates a diagnostic analyzer from a compatible runtime analyzer type.
    /// </summary>
    /// <param name="analyzerType">The runtime type to validate and instantiate as a diagnostic analyzer.</param>
    /// <returns>The available analyzer, or a result describing incompatibility or construction failure.</returns>
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
