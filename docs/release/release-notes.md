# Roslyn Workbench MCP — {{VERSION}}

Roslyn Workbench MCP is a local stdio server for Roslyn-powered C# inspection and transactional refactoring. The 0.1.0 beta follows successful evaluation across several solutions, including a large enterprise solution, and is intended for wider feedback on trusted repositories. Tool contracts, configuration and persisted state may change before v1; review release notes before upgrading and finish or discard active transactions before replacing the server.

## What is included

- Multiple long-lived Workspace sessions and semantic inspection of symbols, references, diagnostics, code structure and change impact.
- Bounded results with snapshot-aware selectors and a versioned tool reference for deeper guidance.
- Mutation staging, previews, undo/redo, rollback, explicit commit, conflict detection and durable transaction recovery.
- Roslyn Code Action discovery, Fix All preparation and staging through the same transaction boundary.
- Local correlated diagnostics and an explicit, consent-controlled error-reporting workflow.

## Requirements and support

A supported .NET 10 SDK and an MCP client able to launch a local stdio process are required. Install the SDKs/build tooling needed by the projects being analysed as well. Windows x64, Linux x64 and WSL2 x64 are supported. macOS x64 and ARM64 are best effort; Windows ARM64 and Linux ARM64 are not supported release targets.

Only open fully trusted repositories. MSBuild evaluation, project analysers and plugins can execute with the Host's permissions; Roslyn Workbench is not a sandbox. Stop other tools from changing affected source or directory structure during commit application and startup recovery.

## Install from NuGet.org

The 0.1.0 beta is distributed through NuGet.org; GitHub Packages credentials are not required.

```bash
dotnet tool install --global Lantean.Roslyn.Workbench.Mcp --version {{VERSION}}
roslyn-workbench-mcp --version
```

For an existing installation, use `dotnet tool update --global Lantean.Roslyn.Workbench.Mcp --version {{VERSION}}`. The reported version must match the release.

Earlier engineering prereleases used the package ID `Roslyn.Workbench.Mcp`. NuGet treats the publisher-qualified ID as a different tool, so uninstall that earlier package before installing `Lantean.Roslyn.Workbench.Mcp`. The `roslyn-workbench-mcp` command remains unchanged.

## Connect and evaluate

Configure the MCP client to launch `roslyn-workbench-mcp`. See the release's [getting-started guidance](https://lantean-code.github.io/roslyn-workbench-mcp/{{DOCS_VERSION}}/getting-started.html) for state-directory configuration, Workspace loading and a first transaction. The running server's `tools/list` is authoritative for its enabled tool set.

Client support and approval policy determine whether optional elicitation prompts appear. If reporting is blocked, no error report is sent. Review prepared payloads carefully: exception messages may contain source text, paths, identifiers or secrets. Choose the option without exception messages when needed.

## Not included in this release

- The Plugins NuGet package, supported plugin-authoring documentation and curated plugin repository are not included. Trusted source-built plugins still work in the existing runtime.
- Native installation packages are not included; install this beta as a .NET tool.

## Feedback

Use [Issues](https://github.com/lantean-code/roslyn-workbench-mcp/issues) for reproducible defects and documentation problems, [Q&A](https://github.com/lantean-code/roslyn-workbench-mcp/discussions/categories/q-a) for help, and [Ideas](https://github.com/lantean-code/roslyn-workbench-mcp/discussions/categories/ideas) for exploratory proposals. Follow the [security policy](https://github.com/lantean-code/roslyn-workbench-mcp/security/policy) for private vulnerability reports. Include version, platform and redacted reproduction details, not private source or credentials.

External pull requests are not accepted before v1 preparation. Issues, Discussions, private security reports and other feedback remain welcome.

## Uninstall

Remove the client's server entry, stop the process and run `dotnet tool uninstall --global Lantean.Roslyn.Workbench.Mcp`. Keep unresolved recovery state until recovery is understood; see [troubleshooting and removal](https://lantean-code.github.io/roslyn-workbench-mcp/{{DOCS_VERSION}}/troubleshooting.html).

## Licence and release evidence

The package is MIT-licensed. Release assets include symbols, checksums, source identity and a coverage snapshot.
