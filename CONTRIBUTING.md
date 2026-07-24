# Contributing

Thank you for helping improve Roslyn Workbench MCP.

## Before opening an issue

- Use GitHub issues for reproducible defects, documentation problems and focused feature proposals.
- Check the live MCP `tools/list` result before reporting a missing tool; the configured plugin set and Code Action catalogue affect the published surface.
- Do not report vulnerabilities publicly. Follow [the security policy](SECURITY.md).

## Development setup

The repository requires the .NET 10 SDK selected by `global.json`.

```bash
dotnet restore
dotnet build
```

Follow the repository conventions in `AGENTS.md`, `src/AGENTS.md` and `test/AGENTS.md`. Keep changes focused, update public documentation when behaviour changes and add coverage at the owning unit, contract, integration or acceptance boundary.

Acceptance tests exercise a published Host and are intentionally not part of the normal local edit loop. Run them when changing acceptance behaviour or when requested during release validation.

## Pull requests

Explain the user-visible or architectural intent, the implementation, the validation performed and any compatibility or migration risk. A pull request should build with the pinned SDK, pass the affected tests and avoid unrelated formatting or generated-output changes.

Plugin API changes require particular care because external packages compile against the published Plugins and Workspace contracts. Describe any source or binary compatibility impact explicitly.
