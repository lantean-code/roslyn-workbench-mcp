using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionAnalyzerActivator : ICodeActionAnalyzerActivator
{
    public CodeActionAnalyzerActivationResult Activate(string analyzerTypeName)
    {
        var typeResolution = ResolveAnalyzerType(analyzerTypeName);
        if (typeResolution.HasFailure)
        {
            return typeResolution.Failure;
        }

        return CreateAnalyzer(typeResolution.AnalyzerType);
    }

    public CodeActionAnalyzerActivationResult Activate(Type analyzerType)
    {
        if (!typeof(DiagnosticAnalyzer).IsAssignableFrom(analyzerType))
        {
            return CodeActionAnalyzerActivationResult.IncompatibleType();
        }

        return CreateAnalyzer(analyzerType);
    }

    private static AnalyzerTypeResolution ResolveAnalyzerType(string analyzerTypeName)
    {
        var inspectionFailed = false;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type? analyzerType;
            try
            {
                analyzerType = assembly.GetType(analyzerTypeName, throwOnError: false, ignoreCase: false);
            }
            catch (Exception exception) when (IsExpectedInspectionFailure(exception))
            {
                // Loaded assemblies are external runtime inputs; one unreadable assembly must not disable Code Actions.
                inspectionFailed = true;
                continue;
            }

            if (analyzerType is null)
            {
                continue;
            }

            if (typeof(DiagnosticAnalyzer).IsAssignableFrom(analyzerType))
            {
                return AnalyzerTypeResolution.Available(analyzerType);
            }
            else
            {
                return AnalyzerTypeResolution.Failed(CodeActionAnalyzerActivationResult.IncompatibleType());
            }
        }

        if (inspectionFailed)
        {
            return AnalyzerTypeResolution.Failed(CodeActionAnalyzerActivationResult.InspectionFailed());
        }
        else
        {
            return AnalyzerTypeResolution.Failed(CodeActionAnalyzerActivationResult.TypeNotFound());
        }
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

    private static bool IsExpectedInspectionFailure(Exception exception)
    {
        return exception is ArgumentException
            or BadImageFormatException
            or FileLoadException
            or FileNotFoundException
            or NotSupportedException
            or SecurityException
            or TypeLoadException;
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

    private sealed record AnalyzerTypeResolution
    {
        public Type? AnalyzerType { get; }

        public CodeActionAnalyzerActivationResult? Failure { get; }

        [MemberNotNullWhen(true, nameof(Failure))]
        [MemberNotNullWhen(false, nameof(AnalyzerType))]
        public bool HasFailure => Failure is not null;

        private AnalyzerTypeResolution(
            Type? analyzerType,
            CodeActionAnalyzerActivationResult? failure)
        {
            AnalyzerType = analyzerType;
            Failure = failure;
        }

        public static AnalyzerTypeResolution Available(Type analyzerType)
        {
            return new AnalyzerTypeResolution(analyzerType, failure: null);
        }

        public static AnalyzerTypeResolution Failed(CodeActionAnalyzerActivationResult failure)
        {
            return new AnalyzerTypeResolution(analyzerType: null, failure);
        }
    }
}
