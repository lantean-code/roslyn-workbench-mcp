using System.Diagnostics.CodeAnalysis;
using ContractDiagnosticSeverity = Roslyn.Workbench.Mcp.Workspace.Contracts.Results.DiagnosticSeverity;

namespace Roslyn.Workbench.Mcp.Plugins.Validation;

internal sealed class PluginHandlerContractResolver : IPluginHandlerContractResolver
{
    private static readonly Type _queryHandlerDefinition = typeof(IQueryToolHandler<,>);
    private static readonly Type _mutationHandlerDefinition = typeof(IMutationToolHandler<>);

    public bool TryResolve(
        ConfiguredToolDefinition definition,
        PluginContractAccessibility contractAccessibility,
        [NotNullWhen(true)] out Type? contract,
        [NotNullWhen(false)] out DiagnosticInfo? diagnostic)
    {
        var matchingDefinition = definition.Kind == ToolKind.Query
            ? _queryHandlerDefinition
            : _mutationHandlerDefinition;

        var otherDefinition = definition.Kind == ToolKind.Query
            ? _mutationHandlerDefinition
            : _queryHandlerDefinition;

        var interfaces = definition.HandlerType.GetInterfaces();
        var matchingContracts = interfaces
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == matchingDefinition)
            .ToArray();

        var hasMismatchedContract = interfaces.Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == otherDefinition);

        if (matchingContracts.Length != 1 || hasMismatchedContract)
        {
            var handlerFamily = definition.Kind == ToolKind.Query ? "query" : "mutation";
            contract = null;
            diagnostic = CreateDiagnostic(
                $"Plugin handler '{definition.HandlerType.FullName}' must implement exactly one {handlerFamily} handler contract and no handler contract from the other family.");

            return false;
        }

        contract = matchingContracts[0];
        if (contractAccessibility == PluginContractAccessibility.PublicOnly)
        {
            foreach (var contractType in contract.GenericTypeArguments)
            {
                if (!IsPublicContractType(contractType))
                {
                    contract = null;
                    diagnostic = CreateDiagnostic($"Tool contract type '{contractType.FullName}' must be public.");
                    return false;
                }
            }
        }

        diagnostic = null;
        return true;
    }

    private static DiagnosticInfo CreateDiagnostic(string message)
    {
        return new DiagnosticInfo
        {
            Id = PluginDiagnosticIds.HandlerContract,
            Severity = ContractDiagnosticSeverity.Error,
            Message = message,
        };
    }

    private static bool IsPublicContractType(Type contractType)
    {
        var elementType = contractType.GetElementType();
        if (elementType is not null)
        {
            return IsPublicContractType(elementType);
        }

        if (contractType.IsGenericType
            && contractType.GenericTypeArguments.Any(static type => !IsPublicContractType(type)))
        {
            return false;
        }

        var typeDefinition = contractType.IsGenericType
            ? contractType.GetGenericTypeDefinition()
            : contractType;

        if (!typeDefinition.IsNested)
        {
            return typeDefinition.IsPublic;
        }

        var declaringType = typeDefinition.DeclaringType;
        return typeDefinition.IsNestedPublic
            && declaringType is not null
            && IsPublicContractType(declaringType);
    }
}
