# Contributing

Thank you for helping improve Roslyn Workbench MCP. Issues, Discussions, security reports and other feedback are welcome. Please follow the [support guide](SUPPORT.md) so the report reaches the right place, and follow the [Code of Conduct](CODE_OF_CONDUCT.md) in every project space.

## External pull requests

Check [README](README.md) for current contribution availability before starting a pull request. Discuss the proposed change through an Issue or Discussion first so that its scope and direction can be agreed. When external contributions are closed, unsolicited pull requests are closed with a link to the current policy rather than entering review.

Contribution availability does not prevent anyone from using the repository under the MIT licence, building the source, maintaining a fork, or proposing a change through an Issue or Discussion. Rights already granted for a published MIT-licensed version cannot be withdrawn. Any contributor agreement or sign-off requirements will be stated before accepting contributions.

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
