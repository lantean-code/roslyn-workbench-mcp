namespace Roslyn.Workbench.Mcp.Plugins.Core.Inspection;

[RoslynTool(
    name: "get-symbol-members",
    title: "Get Symbol Members",
    description: "Lists declared members and optional inherited or interface members for a resolved symbol.")]
internal sealed class GetSymbolMembersTool : QueryToolHandler<GetSymbolMembersRequest, SymbolMembersData>
{
    protected override async ValueTask<PluginExecutionResult<SymbolMembersData>> ExecuteCoreAsync(GetSymbolMembersRequest request, IQueryContext context, CancellationToken cancellationToken)
    {
        var symbolResolution = await context.ToolExecutionServices.RequestResolver.ResolveSymbolAsync<SymbolMembersData>(request.Symbol, request.ExpectedSnapshot, context, cancellationToken);
        if (symbolResolution.HasRejection)
        {
            return symbolResolution.Rejection;
        }

        if (symbolResolution.Value is not INamedTypeSymbol namedType)
        {
            return PluginExecutionResultFactory.Rejected<SymbolMembersData>("InvalidRequest", "Get symbol members requires a named type symbol.");
        }

        var members = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        foreach (var member in namedType.GetMembers())
        {
            if (!member.IsImplicitlyDeclared)
            {
                members.Add(member);
            }
        }

        if (request.IncludeInherited)
        {
            for (var current = namedType.BaseType; current is not null; current = current.BaseType)
            {
                foreach (var member in current.GetMembers())
                {
                    if (!member.IsImplicitlyDeclared)
                    {
                        members.Add(member);
                    }
                }
            }
        }

        if (request.IncludeExplicitInterface)
        {
            foreach (var interfaceSymbol in namedType.AllInterfaces)
            {
                foreach (var member in interfaceSymbol.GetMembers())
                {
                    members.Add(member);
                }
            }
        }

        var orderedMembers = members
            .Select(member => context.WorkspaceResolver.CreateSymbolReference(member))
            .OrderBy(static member => member.DisplayName, StringComparer.Ordinal)
            .ThenBy(static member => member.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal);

        var projectedMembers = new List<SymbolReference>();
        var hasMore = false;
        foreach (var memberReference in orderedMembers)
        {
            if (projectedMembers.Count == request.EffectiveMembersLimit)
            {
                hasMore = true;
                break;
            }

            projectedMembers.Add(memberReference);
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(namedType);
        var data = new SymbolMembersData
        {
            Symbol = symbolReference,
            Members = BoundedCollection<SymbolReference>.CreatePrebounded(projectedMembers, hasMore),
        };

        return PluginExecutionResult<SymbolMembersData>.Success(data);
    }
}
