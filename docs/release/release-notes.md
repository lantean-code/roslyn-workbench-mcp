# Roslyn Workbench MCP — {{VERSION}}

Roslyn Workbench MCP 1.0 is the first stable release of the local stdio server for Roslyn-powered C# inspection and transactional refactoring. It includes the Host .NET tool and a separate plugin-authoring package, with a [1.x compatibility policy](https://lantean-code.github.io/roslyn-workbench-mcp/{{DOCS_VERSION}}/compatibility.html) for the published surfaces.

## What is included

- Multiple long-lived Workspace sessions and semantic inspection of symbols, references, diagnostics, code structure and change impact.
- Bounded, snapshot-aware results, a versioned tool reference and structured continuation guidance.
- Transaction staging, preview or receipt-based review, history, undo/redo, rollback, conflict detection and durable recovery.
- Roslyn Code Action discovery, Fix All preparation and staging through the same transaction boundary.
- Optional Workspace admission roots, effective-root capping and external-document policy for limiting accidental agent scope.
- Optional compiler-impact validation that rejects commits introducing new compiler errors. It does not replace analyser validation or tests.
- Opt-in mutation-provider provenance, local correlated diagnostics and an explicit, consent-controlled error-reporting workflow.
- The `Lantean.Roslyn.Workbench.Mcp.Plugins` authoring package, containing Plugins and Abstractions contracts and the authoring analyser. Users install trusted plugins manually; no curated plugin repository is included.

## Requirements and operating defaults

A supported .NET 10 SDK and an MCP client able to launch a local stdio process are required. Install the SDKs and build tooling needed by the projects being analysed. Windows x64, Linux x64 and WSL2 x64 are supported. macOS x64 and ARM64 are best effort; Windows ARM64 and Linux ARM64 are unsupported release targets. Supported workspaces contain SDK-style C# projects; unsupported languages and project systems are skipped with diagnostics.

The default mode is `inspection-only`, which exposes no transaction or source-mutation tools. External plugin loading is independently disabled by default. Choose an operational mode deliberately:

| Mode | Commit workflow |
| --- | --- |
| `inspection-only` | Inspection and Workspace lifecycle only. |
| `transactional` | Preview, then client-mediated confirmation for one commit or the Host session. |
| `approval-required` | Review an exact transaction receipt, then confirm the receipt-bound commit. |
| `autonomous-trusted` | Preview and commit without Host confirmation. |

The two confirmation modes require the client to advertise and permit MCP elicitation. A decline, cancellation or unavailable prompt does not commit; inspect the structured result before retrying. Compiler-impact validation is separately opt-in. See [Configuration](https://lantean-code.github.io/roslyn-workbench-mcp/{{DOCS_VERSION}}/configuration.html) for switches, environment variables, defaults and precedence.

## Install and connect

Both products are distributed through NuGet.org. Run the exact Host package without a permanent installation:

```bash
dnx Lantean.Roslyn.Workbench.Mcp@{{VERSION}} --yes -- --version
```

Alternatively, install the same package as a global tool:

```bash
dotnet tool install --global Lantean.Roslyn.Workbench.Mcp --version {{VERSION}}
roslyn-workbench-mcp --version
```

The reported version must match the release. Configure the client to launch `roslyn-workbench-mcp`, or `dnx` with the exact package version and Host arguments after `--`. See [Getting started](https://lantean-code.github.io/roslyn-workbench-mcp/{{DOCS_VERSION}}/getting-started.html). The running server's `tools/list` is authoritative for the enabled tools and their mode-specific contracts. Native installers are not provided.

Plugin projects reference `Lantean.Roslyn.Workbench.Mcp.Plugins` at the matching release version. See [Plugin authoring](https://lantean-code.github.io/roslyn-workbench-mcp/{{DOCS_VERSION}}/plugin-authoring.html) for the package boundary, deployment and compatibility requirements.

## Compatibility and upgrade

### MCP tools

The stable built-in catalogue follows an additive 1.x policy. Existing tool names and input/output meanings are preserved; clients should ignore unknown output fields and rediscover `tools/list` after a restart or upgrade. Canonical baselines cover both output-schema publication modes and each supported operational configuration.

### Errors and continuations

Stable error codes retain their meanings. `RequiredAction` values and continuation kinds and targets are stable contracts; human-readable messages may improve. Surface unknown codes safely and do not guess a recovery action from message prose.

### Recovery formats

Recovery format V1 remains readable throughout 1.x. Unsupported formats are reported and retained without reinterpretation or cleanup; use the same or a newer compatible Host to recover them. Finish or roll back active transactions and stop the Host before upgrading. Retain unresolved state and follow [Recovery guidance](https://lantean-code.github.io/roslyn-workbench-mcp/{{DOCS_VERSION}}/troubleshooting.html).

### Plugin APIs

Plugin API V1 and the shipped Plugins and Abstractions public APIs are stable for 1.x. Plugin authors own their tool contracts and should use the supported package rather than Host or Workspace implementation assemblies.

For an existing global installation, run `dotnet tool update --global Lantean.Roslyn.Workbench.Mcp --version {{VERSION}}`. For `dnx`, update the pinned package version in client configuration. The published 0.1.0 beta did not carry the stable guarantee; review the operating defaults and plugin opt-in before reconnecting. If an older installation uses the former package ID `Roslyn.Workbench.Mcp`, uninstall it before installing `Lantean.Roslyn.Workbench.Mcp`; the command name remains unchanged.

## Trust boundary and known limitations

Open only fully trusted repositories. MSBuild evaluation, project analysers and explicitly enabled plugins execute with the Host's permissions; Roslyn Workbench is not an operating-system sandbox. Workspace roots and transactional controls protect against accidental agent scope and source-change errors, not untrusted executable build inputs. Avoid concurrent writes to commit-owned paths or structural repository changes during commit application and startup recovery.

Error reporting requires explicit consent. Review prepared payloads because exception messages can contain source text, paths, identifiers or secrets. Choose the option without exception messages when necessary. A declined or unavailable prompt sends no report.

Platform limitations, client-dependent elicitation, trusted executable inputs and the limits of compiler-impact validation are explicit parts of the supported [security model](https://lantean-code.github.io/roslyn-workbench-mcp/{{DOCS_VERSION}}/reference/security/index.html).

## Feedback and contributions

Use [Issues](https://github.com/lantean-code/roslyn-workbench-mcp/issues) for reproducible defects, [Q&A](https://github.com/lantean-code/roslyn-workbench-mcp/discussions/categories/q-a) for help and [Ideas](https://github.com/lantean-code/roslyn-workbench-mcp/discussions/categories/ideas) for proposals. Follow the [security policy](https://github.com/lantean-code/roslyn-workbench-mcp/security/policy) for private vulnerability reports. Include redacted reproduction details rather than private source or credentials.

External pull requests are accepted. Agree the scope first and sign every contributed commit with a cryptographic signature that GitHub can verify. No separate DCO sign-off or contributor licence agreement is required; see [Contributing](https://github.com/lantean-code/roslyn-workbench-mcp/blob/{{SOURCE_REF}}/CONTRIBUTING.md).

## Uninstall and release evidence

Remove the client's server entry, stop the Host and run `dotnet tool uninstall --global Lantean.Roslyn.Workbench.Mcp`. A `dnx` configuration has no global tool installation to uninstall. Keep unresolved recovery evidence until recovery is understood.

Both packages are MIT-licensed. Release assets include both `.nupkg` and `.snupkg` pairs, SHA-256 checksums, source and release identity, coverage evidence, SPDX SBOMs and validated dependency-vulnerability evidence. Published package hashes receive GitHub build-provenance and SBOM attestations. See [Supply-chain evidence](https://lantean-code.github.io/roslyn-workbench-mcp/{{DOCS_VERSION}}/supply-chain.html) for verification and its trust boundaries.
