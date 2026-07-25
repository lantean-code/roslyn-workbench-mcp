namespace Roslyn.Workbench.Mcp.Workspace.IO;

internal interface IPhysicalPathContainment
{
    bool TryGetContainedPath(string rootDirectory, string candidatePath, out string containedPath);

    bool TryGetStrictlyContainedPath(string rootDirectory, string candidatePath, out string containedPath);
}
