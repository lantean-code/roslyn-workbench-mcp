using Microsoft.Extensions.Primitives;

namespace Roslyn.Workbench.Mcp.Workspace.Caching;

internal interface IQueryCacheInvalidationTokenSource
{
    IChangeToken GetInvalidationToken(string workspaceId);
}
