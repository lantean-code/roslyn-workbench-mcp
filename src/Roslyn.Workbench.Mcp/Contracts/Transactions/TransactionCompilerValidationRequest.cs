namespace Roslyn.Workbench.Mcp.Contracts.Transactions;

/// <summary>
/// Represents a request to compare compiler errors in the active transaction baseline and staged snapshot.
/// </summary>
internal sealed record TransactionCompilerValidationRequest : WorkspaceMutationRequest;
