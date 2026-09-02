# Maintainer guide

These documents describe lasting engineering decisions and maintenance procedures, not implementation worklists or dated review evidence. External contributions remain subject to [the contribution policy](../../CONTRIBUTING.md).

- [Architecture](architecture.md): project boundaries, execution, snapshots and durable changes.
- [Operating model](operating-model.md): supported actors, trust and concurrency assumptions.
- [Testing](testing.md): test ownership, execution and coverage policy.
- [Coverage exceptions](coverage-exceptions.md): narrowly approved defensive branches and real integration boundaries.
- [Analysers](analysers.md): extended analysis and suppression policy.
- [Releasing](releasing.md): versioning, evidence, publication and repository activation.
- [Roadmap](roadmap.md): intentionally deferred capabilities and conditions for revisiting them.

User-facing instructions and the generated tool reference belong in the [documentation site](https://lantean-code.github.io/roslyn-workbench-mcp/). MCP `tools/list` remains authoritative for the running server.
