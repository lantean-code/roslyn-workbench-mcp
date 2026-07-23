using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Roslyn.Workbench.Mcp.CodeActions.Composition;

internal sealed class MefHostExportProviderCompatibilityAdapter : IMefHostExportProviderCompatibilityAdapter
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Enumerating Roslyn MEF exports activates external providers; any provider-defined activation failure must make only the Code Action catalogue unavailable.")]
    public MefHostExportReadResult<T> ReadExports<T>(MefHostServices hostServices)
    {
        var methods = typeof(MefHostServices)
            .GetMethods(BindingFlags.Instance | BindingFlags.NonPublic)
            .Where(IsExportMethod)
            .ToArray();

        if (methods.Length != 1)
        {
            return Failure<T>($"Expected one Roslyn MEF export method but found {methods.Length}.");
        }

        MethodInfo closedMethod;
        try
        {
            closedMethod = methods[0].MakeGenericMethod(typeof(T));
        }
        catch (Exception exception) when (IsExpectedReflectionFailure(exception))
        {
            return ReflectionFailure<T>("closing the Roslyn MEF export method", exception);
        }

        IEnumerable exports;
        try
        {
            var invocationResult = closedMethod.Invoke(hostServices, parameters: null);
            if (invocationResult is not IEnumerable enumerable)
            {
                return Failure<T>("The Roslyn MEF export method returned an unsupported result.");
            }

            exports = enumerable;
        }
        catch (Exception exception) when (IsExpectedReflectionFailure(exception))
        {
            return ReflectionFailure<T>("invoking the Roslyn MEF export method", exception);
        }

        try
        {
            return ReadExportValues<T>(exports);
        }
        catch (Exception exception)
        {
            // Enumeration can activate external Roslyn exports; any activation failure makes this catalogue unavailable.
            return ReflectionFailure<T>("enumerating Roslyn MEF exports", exception);
        }
    }

    private static MefHostExportReadResult<T> ReadExportValues<T>(IEnumerable exports)
    {
        var values = new List<T>();
        foreach (var export in exports)
        {
            if (export is null)
            {
                return Failure<T>("Roslyn MEF returned a null export entry.");
            }

            var valueProperties = export.GetType()
                .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                .Where(static property => property.Name == "Value")
                .ToArray();

            if (valueProperties.Length != 1)
            {
                return Failure<T>($"A Roslyn MEF export entry exposed {valueProperties.Length} public Value properties instead of one.");
            }

            object? value;
            try
            {
                value = valueProperties[0].GetValue(export);
            }
            catch (Exception exception) when (IsExpectedReflectionFailure(exception))
            {
                return ReflectionFailure<T>("activating a Roslyn MEF export value", exception);
            }

            if (value is not T typedValue)
            {
                return Failure<T>($"A Roslyn MEF export value was not assignable to {typeof(T).FullName}.");
            }

            values.Add(typedValue);
        }

        return MefHostExportReadResult<T>.Success(values);
    }

    private static bool IsExportMethod(MethodInfo method)
    {
        return method.Name.Contains("IMefHostExportProvider.GetExports", StringComparison.Ordinal)
            && method.IsGenericMethodDefinition
            && method.GetGenericArguments().Length == 1;
    }

    private static bool IsExpectedReflectionFailure(Exception exception)
    {
        return exception is ArgumentException
            or MemberAccessException
            or NotSupportedException
            or TargetInvocationException
            or TargetParameterCountException;
    }

    private static MefHostExportReadResult<T> ReflectionFailure<T>(string stage, Exception exception)
    {
        var underlyingException = exception is TargetInvocationException invocationException
            ? invocationException.InnerException
            : null;

        var failureException = underlyingException ?? exception;
        var failureType = failureException.GetType().Name;

        return Failure<T>($"Failed while {stage} ({failureType}).");
    }

    private static MefHostExportReadResult<T> Failure<T>(string error)
    {
        return MefHostExportReadResult<T>.Failure(error);
    }
}
