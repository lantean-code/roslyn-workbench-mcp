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
            return ToolExecutionHelpers.Rejected<SymbolMembersData>("InvalidRequest", "Get symbol members requires a named type symbol.");
        }

        var members = new List<ISymbol>();
        members.AddRange(namedType.GetMembers().Where(static member => !member.IsImplicitlyDeclared));

        if (request.IncludeInherited)
        {
            for (var current = namedType.BaseType; current is not null; current = current.BaseType)
            {
                members.AddRange(current.GetMembers().Where(static member => !member.IsImplicitlyDeclared));
            }
        }

        if (request.IncludeExplicitInterface)
        {
            members.AddRange(namedType.AllInterfaces.SelectMany(static item => item.GetMembers()));
        }

        var orderedMembers = members
            .Distinct(SymbolEqualityComparer.Default)
            .OrderBy(member => context.WorkspaceResolver.CreateSymbolReference(member).DisplayName, StringComparer.Ordinal)
            .ThenBy(member => context.WorkspaceResolver.CreateSymbolReference(member).Location?.Document?.Path ?? string.Empty, StringComparer.Ordinal)
            .Select(context.WorkspaceResolver.CreateSymbolReference)
            .ToArray();
        var symbolReference = context.WorkspaceResolver.CreateSymbolReference(namedType);

        return PluginExecutionResult<SymbolMembersData>.Success(new SymbolMembersData
        {
            Symbol = symbolReference,
            Members = ToolExecutionHelpers.CreateBoundedCollection(
                orderedMembers,
                ToolExecutionHelpers.GetMaxResults(context, request.MembersLimit)),
        });
    }
}
