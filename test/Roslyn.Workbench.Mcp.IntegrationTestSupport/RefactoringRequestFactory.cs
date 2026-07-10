using Roslyn.Workbench.Mcp.Contracts.Refactorings;
using Roslyn.Workbench.Mcp.Contracts.Results;
using Roslyn.Workbench.Mcp.Contracts.Selectors;
using Roslyn.Workbench.Mcp.Contracts.Server;

namespace Roslyn.Workbench.Mcp.IntegrationTestSupport;

public static class RefactoringRequestFactory
{
    public static LocationRefactoringRequest CreateLocationRequest(LocationSelector selection, ToolResult<WorkspaceOpenData> openResult)
    {
        return new LocationRefactoringRequest
        {
            Selection = selection,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        };
    }

    public static ConvertAnonymousTypeToClassRequest CreateAnonymousTypeToClassRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        ConvertAnonymousTypeToClassKind kind)
    {
        return new ConvertAnonymousTypeToClassRequest
        {
            Selection = selection,
            Kind = kind,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        };
    }

    public static AddAwaitRequest CreateAddAwaitRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        AddAwaitKind kind)
    {
        return new AddAwaitRequest
        {
            Selection = selection,
            Kind = kind,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        };
    }

    public static AddImportRequest CreateAddImportRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        bool simplifyAllOccurrences)
    {
        return new AddImportRequest
        {
            Selection = selection,
            SimplifyAllOccurrences = simplifyAllOccurrences,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        };
    }

    public static ConvertIfToSwitchRequest CreateConvertIfToSwitchRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        ConvertIfToSwitchKind kind)
    {
        return new ConvertIfToSwitchRequest
        {
            Selection = selection,
            Kind = kind,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        };
    }

    public static UseNamedArgumentsRequest CreateUseNamedArgumentsRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        bool includeTrailingArguments)
    {
        return new UseNamedArgumentsRequest
        {
            Selection = selection,
            IncludeTrailingArguments = includeTrailingArguments,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        };
    }

    public static MoveTypeToFileRequest CreateMoveTypeToFileRequest(LocationSelector selection, ToolResult<WorkspaceOpenData> openResult)
    {
        return new MoveTypeToFileRequest
        {
            Type = new SymbolSelector
            {
                Location = selection,
            },
            PreserveNamespace = true,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        };
    }

    public static ConvertPropertyRequest CreateConvertPropertyRequest(
        LocationSelector selection,
        ToolResult<WorkspaceOpenData> openResult,
        ConvertPropertyDirection direction)
    {
        return new ConvertPropertyRequest
        {
            Selection = selection,
            Direction = direction,
            ExpectedSnapshot = BundledCoreToolTestHarness.CreateSnapshot(openResult, 0),
        };
    }
}
