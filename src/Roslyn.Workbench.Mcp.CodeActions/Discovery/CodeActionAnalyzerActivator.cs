using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Security;

namespace Roslyn.Workbench.Mcp.CodeActions.Discovery;

internal sealed class CodeActionAnalyzerActivator : ICodeActionAnalyzerActivator
{
    public CodeActionAnalyzerActivationResult Activate(string analyzerTypeName)
    {
        var typeResolution = ResolveAnalyzerType(analyzerTypeName);
        if (!typeResolution.IsAvailable)
        {
            return Unavailable(typeResolution.Status);
        }

        return CreateAnalyzer(typeResolution.AnalyzerType);
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

            return typeof(DiagnosticAnalyzer).IsAssignableFrom(analyzerType)
                ? AnalyzerTypeResolution.Available(analyzerType)
                : AnalyzerTypeResolution.Unavailable(CodeActionAnalyzerActivationStatus.IncompatibleType);
        }

        return AnalyzerTypeResolution.Unavailable(
            inspectionFailed
                ? CodeActionAnalyzerActivationStatus.InspectionFailed
                : CodeActionAnalyzerActivationStatus.TypeNotFound);
    }

    private static CodeActionAnalyzerActivationResult CreateAnalyzer(Type analyzerType)
    {
        try
        {
            var analyzer = Activator.CreateInstance(analyzerType, nonPublic: true) as DiagnosticAnalyzer;
            return analyzer is null
                ? Unavailable(CodeActionAnalyzerActivationStatus.ConstructionFailed)
                : new CodeActionAnalyzerActivationResult
                {
                    Status = CodeActionAnalyzerActivationStatus.Available,
                    Analyzer = analyzer,
                };
        }
        catch (Exception exception) when (IsExpectedConstructionFailure(exception))
        {
            // Optional runtime analyzers can have inaccessible or failing constructors; absence is an expected outcome.
            return Unavailable(CodeActionAnalyzerActivationStatus.ConstructionFailed);
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

    private static CodeActionAnalyzerActivationResult Unavailable(CodeActionAnalyzerActivationStatus status)
    {
        return new CodeActionAnalyzerActivationResult
        {
            Status = status,
        };
    }

    private sealed record AnalyzerTypeResolution
    {
        public required CodeActionAnalyzerActivationStatus Status { get; init; }

        public Type? AnalyzerType { get; init; }

        [MemberNotNullWhen(true, nameof(AnalyzerType))]
        public bool IsAvailable => AnalyzerType is not null;

        public static AnalyzerTypeResolution Available(Type analyzerType)
        {
            return new AnalyzerTypeResolution
            {
                Status = CodeActionAnalyzerActivationStatus.Available,
                AnalyzerType = analyzerType,
            };
        }

        public static AnalyzerTypeResolution Unavailable(CodeActionAnalyzerActivationStatus status)
        {
            return new AnalyzerTypeResolution
            {
                Status = status,
            };
        }
    }
}
