using Microsoft.CodeAnalysis;

namespace Roslyn.Workbench.Mcp.Test.ToolExecution;

internal static class ToolExecutionContextMockHelper
{
    public static void ConfigurePluginContext<TContext>(Mock<TContext> context, Solution solution)
        where TContext : class, IToolExecutionContext
    {
        context.SetupGet(item => item.CurrentSolution).Returns(solution);
        context.SetupGet(item => item.WorkspaceIdentity).Returns(CreateWorkspaceIdentity());
        context.SetupGet(item => item.TransactionRevision).Returns(2);
    }

    public static void ConfigureCodeActionContext<TContext>(Mock<TContext> context, Solution solution)
        where TContext : class, ICodeActionExecutionContext
    {
        context.SetupGet(item => item.CurrentSolution).Returns(solution);
        context.SetupGet(item => item.WorkspaceIdentity).Returns(CreateWorkspaceIdentity());
        context.SetupGet(item => item.TransactionRevision).Returns(2);
    }

    private static WorkspaceIdentity CreateWorkspaceIdentity()
    {
        return new WorkspaceIdentity
        {
            WorkspaceId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            WorkspaceEpoch = 3,
            LoadedPath = "C:\\Workspace\\Solution.sln",
            WorkspaceRoot = "C:\\Workspace",
        };
    }
}
