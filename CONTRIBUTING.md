# Contributing

Thank you for helping improve Roslyn Workbench MCP. Issues, Discussions, security reports and other feedback are welcome. Please follow the [support guide](SUPPORT.md) so the report reaches the right place, and follow the [Code of Conduct](CODE_OF_CONDUCT.md) in every project space.

## External pull requests

External pull requests are accepted. Discuss the proposed change through an Issue or Discussion first so that its scope and direction can be agreed. Every contributed commit must carry a cryptographic signature that GitHub can verify. A separate Developer Certificate of Origin sign-off or contributor licence agreement is not required.

The MIT licence permits using the repository, building the source and maintaining a fork. Rights already granted for a published MIT-licensed version cannot be withdrawn.

## Choose the right route

- Ask setup, usage and troubleshooting questions in [Q&A](https://github.com/lantean-code/roslyn-workbench-mcp/discussions/categories/q-a).
- Discuss an early or exploratory proposal in [Ideas](https://github.com/lantean-code/roslyn-workbench-mcp/discussions/categories/ideas).
- Use the structured issue forms for reproducible defects, documentation problems and focused feature proposals that are ready for engineering assessment.
- Report vulnerabilities privately as described in [SECURITY.md](SECURITY.md).
- Send private Code of Conduct reports to `lanteancode@gmail.com` rather than a public Issue or Discussion.

Do not include source code, credentials, personal data, private paths or other secrets unless they are necessary and the selected private route explicitly asks for them. Prefer the minimum suitably redacted evidence needed to understand the problem.

## Building from source

The repository requires the .NET 10 SDK selected by `global.json`.

```bash
dotnet restore
dotnet build
```

Repository-specific engineering instructions are kept in `AGENTS.md`, `src/AGENTS.md` and `test/AGENTS.md`. Acceptance tests exercise a published Host and are intentionally outside the normal edit loop.

Plugin developers can consult the [source authoring reference](docs/PluginAuthoring.md). Check the selected release's notes for package availability and the supported authoring surface. Source-built plugins run as trusted in-process code; their code and dependencies must be fully trusted.
