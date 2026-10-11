# Building from source

Use this route when developing Roslyn Workbench or running a customised build. For released packages, use [dnx or the global tool](getting-started.md).

## Publish the Host

From the repository root:

```bash
dotnet publish src/Roslyn.Workbench.Mcp/Roslyn.Workbench.Mcp.csproj \
  --configuration Release \
  --output artifacts/publish/Roslyn.Workbench.Mcp/release
```

The published executable is placed beneath `artifacts/publish/Roslyn.Workbench.Mcp/release`.

## Connect the source build

For a source build, use the absolute path to the published `Roslyn.Workbench.Mcp` executable instead of the installed command.

The server communicates over standard input and standard output. Protocol data uses stdout; operational logging uses stderr.

Configure operational mode and state storage using the same [startup options](configuration.md) as the packaged Host. Open only fully trusted build inputs. Self-built binaries do not automatically carry official release provenance; see [Supply-chain evidence](supply-chain.md).
