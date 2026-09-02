# .NET analyser policy

Normal builds enforce the repository's configured warnings and code style. After changing C# source or tests, also build affected projects with the SDK's extended rules:

```bash
dotnet build <affected-project> --no-restore \
  -p:AnalysisLevel=latest-all -p:EnforceCodeStyleInBuild=true \
  -p:CodeAnalysisTreatWarningsAsErrors=false
```

Under WSL, append `--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp`. For a complete solution assessment, add `--no-incremental` so previously built projects are not omitted. Use the SDK pinned in `global.json`; an SDK upgrade can change the available rules. `latest-all` does not enable every legacy or IDE-only diagnostic.

Review diagnostics in changed files and fix applicable findings. Do not turn a scoped change into unrelated repository-wide cleanup. Retained diagnostics need a narrow, concrete rationale; historical zero-warning reports are not a promise that a newer SDK will report none.

## Deliberate conventions

- `CA2007`: the console-hosted application has no synchronisation context; repository code does not use `ConfigureAwait(false)`.
- `CA1014`: the project does not promise CLS compliance.
- `CA1707`: Given/When/Then test names deliberately contain underscores.
- `CA1002`, `CA1034` and `CA1819`: selected test/fixture contracts deliberately expose mutable collections, nested types or arrays to exercise contract validation. These are not production design precedents.
- Reflection, dependency injection, deserialisation, schema generation and deliberately invalid fixtures can justify narrowly scoped activation/accessibility suppressions. Explain the actual consumer rather than applying a blanket exclusion.
- Durable filesystem flushes, recovery boundaries, top-level protocol isolation and mandatory cleanup failures can justify specific rules being retained or suppressed. Do not replace intentional durability or failure visibility merely to silence a warning.

`.editorconfig`, project properties and source-local suppression comments are the executable authority. Review them when changing the underlying behaviour; a documented exception is not a licence to copy the pattern elsewhere.
