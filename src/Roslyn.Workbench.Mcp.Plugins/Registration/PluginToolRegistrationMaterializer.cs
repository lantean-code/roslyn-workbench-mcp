using System.Reflection;

namespace Roslyn.Workbench.Mcp.Plugins.Registration;

internal sealed class PluginToolRegistrationMaterializer : IPluginToolRegistrationMaterializer
{
    private static readonly MethodInfo _createQueryRegistrationMethod = GetMaterializationMethod(nameof(CreateQueryRegistration));
    private static readonly MethodInfo _createMutationRegistrationMethod = GetMaterializationMethod(nameof(CreateMutationRegistration));

    private static readonly ServiceProviderOptions _serviceProviderOptions = new()
    {
        ValidateOnBuild = true,
        ValidateScopes = true,
    };

    public PluginMaterializationResult Materialize(PluginPreparationResult preparation)
    {
        var services = new ServiceCollection();
        foreach (var service in preparation.Services)
        {
            services.AddSingleton(service.ServiceType, service.ImplementationType);
        }

        foreach (var handlerType in preparation.Tools
            .Select(static tool => tool.HandlerType)
            .Distinct())
        {
            services.TryAddSingleton(handlerType);
        }

        var serviceProvider = services.BuildServiceProvider(_serviceProviderOptions);
        var serviceProviderLifetime = new PluginServiceProviderLifetime(serviceProvider);
        try
        {
            var tools = preparation.Tools
                .Select(tool => CreateRegistration(tool, serviceProvider))
                .ToArray();

            return new PluginMaterializationResult
            {
                Tools = tools,
                Diagnostics = preparation.Diagnostics,
                ServiceProviderLifetime = serviceProviderLifetime,
            };
        }
        catch (Exception materializationException)
        {
            try
            {
                serviceProviderLifetime.Dispose();
            }
            catch (Exception disposalException)
            {
                throw new AggregateException(
                    "Plugin materialization and cleanup both failed.",
                    materializationException,
                    disposalException);
            }

            throw;
        }
    }

    private static IRegisteredPluginTool CreateRegistration(
        PreparedPluginTool preparedTool,
        IServiceProvider serviceProvider)
    {
        object handler;
        try
        {
            handler = serviceProvider.GetRequiredService(preparedTool.HandlerType);
        }
        catch (Exception exception)
        {
            throw CreateConstructionException(preparedTool.HandlerType, exception);
        }

        MethodInfo materializationMethod;
        if (preparedTool.Tool.Kind == ToolKind.Query)
        {
            materializationMethod = _createQueryRegistrationMethod.MakeGenericMethod(
                preparedTool.HandlerContract.GenericTypeArguments);
        }
        else
        {
            materializationMethod = _createMutationRegistrationMethod.MakeGenericMethod(
                preparedTool.HandlerContract.GenericTypeArguments);
        }

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
        where TRequest : WorkspaceMutationRequest
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
