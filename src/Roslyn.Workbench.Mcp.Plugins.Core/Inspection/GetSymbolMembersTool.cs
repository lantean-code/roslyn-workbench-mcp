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
            return PluginExecutionResult.Rejected<SymbolMembersData>("InvalidRequest", "Get symbol members requires a named type symbol.");
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
                    if (IsInheritedMember(member))
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

        var memberReferences = new List<SymbolReference>();
        foreach (var member in members)
        {
            memberReferences.Add(context.WorkspaceResolver.CreateSymbolReference(member));
        }

        var orderedMembers = memberReferences
            .OrderBy(static member => member.DisplayName, StringComparer.Ordinal)
            .ThenBy(static member => member.Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal);

        var projectedMembers = new List<SymbolReference>();
        foreach (var memberReference in orderedMembers)
        {
            if (projectedMembers.Count == request.EffectiveMembersLimit)
            {
                break;
            }

            projectedMembers.Add(memberReference);
        }

        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(namedType);
        var data = new SymbolMembersData
        {
            Symbol = symbolReference,
            Members = BoundedCollection.CreatePrebounded(projectedMembers, memberReferences.Count),
        };

        return PluginExecutionResult.Success(data);
    }

    private static bool IsInheritedMember(ISymbol member)
    {
        if (member.IsImplicitlyDeclared || member.DeclaredAccessibility == Accessibility.Private)
        {
            return false;
        }

        return member is not IMethodSymbol
        {
            MethodKind: MethodKind.Constructor
                or MethodKind.StaticConstructor
                or MethodKind.Destructor,
        };
    }
}
