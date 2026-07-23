using System.Reflection;

namespace Roslyn.Workbench.Mcp.Plugins.Registration;

internal sealed class PluginToolRegistrationMaterializer : IPluginToolRegistrationMaterializer
{
    private static readonly MethodInfo _createQueryRegistrationMethod = GetMaterializationMethod(nameof(CreateQueryRegistration));
    private static readonly MethodInfo _createMutationRegistrationMethod = GetMaterializationMethod(nameof(CreateMutationRegistration));

    public PluginMaterializationResult Materialize(PluginPreparationResult preparation)
    {
        return new PluginMaterializationResult
        {
            Tools = preparation.Tools.Select(CreateRegistration).ToArray(),
            Diagnostics = preparation.Diagnostics,
        };
    }

    private static IRegisteredPluginTool CreateRegistration(PreparedPluginTool preparedTool)
    {
        object handler;
        try
        {
            handler = preparedTool.HandlerFactory();
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw CreateConstructionException(preparedTool.HandlerType, exception.InnerException);
        }
        catch (Exception exception)
        {
            throw CreateConstructionException(preparedTool.HandlerType, exception);
        }

        var materializationMethod = preparedTool.Tool.Kind == ToolKind.Query
            ? _createQueryRegistrationMethod.MakeGenericMethod(preparedTool.HandlerContract.GenericTypeArguments)
            : _createMutationRegistrationMethod.MakeGenericMethod(preparedTool.HandlerContract.GenericTypeArguments);
        var materialize = materializationMethod.CreateDelegate<Func<RegisteredTool, object, IRegisteredPluginTool>>();
        return materialize(preparedTool.Tool, handler);
    }

    private static InvalidOperationException CreateConstructionException(Type handlerType, Exception exception)
    {
        return new InvalidOperationException(
            $"Plugin handler '{handlerType.FullName}' could not be constructed: {exception.Message}",
            exception);
    }

#pragma warning disable CA1859 // Reflection binds both factories to one interface-returning delegate signature.
    private static IRegisteredPluginTool CreateQueryRegistration<TRequest, TResponse>(RegisteredTool tool, object handler)
        where TRequest : WorkspaceBoundRequest
    {
        var queryHandler = (IQueryToolHandler<TRequest, TResponse>)handler;
        return new PluginQueryRegistration<TRequest, TResponse>(tool, queryHandler);
    }

    private static IRegisteredPluginTool CreateMutationRegistration<TRequest>(RegisteredTool tool, object handler)
        where TRequest : WorkspaceBoundRequest
    {
        var mutationHandler = (IMutationToolHandler<TRequest>)handler;
        return new PluginMutationRegistration<TRequest>(tool, mutationHandler);
    }
#pragma warning restore CA1859

    private static MethodInfo GetMaterializationMethod(string name)
    {
        return typeof(PluginToolRegistrationMaterializer)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .Single(method => string.Equals(method.Name, name, StringComparison.Ordinal));
    }
}
