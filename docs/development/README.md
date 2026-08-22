# Development documentation

This directory contains the plans, specifications, audits, implementation matrices, inventories, active backlog and dated evidence used to build and validate Roslyn Workbench.

These files are engineering records. They may describe intended, intermediate, historical or aspirational states and are not the release contract for a running server. Release-facing documentation lives directly under [`docs`](../README.md), and MCP `tools/list` remains authoritative for the live tool inventory.

## Active references

- [Product operating model](ProductOperatingModel.md) defines the supported user, agent, concurrency and trust scenarios used for implementation and review decisions.
- [Future tasks](FutureTasks.md) is the prioritised engineering backlog.
- [Testing strategy](TestingStrategy.md) defines the current test architecture and execution policy.
- [Tool test inventory](Tool%20Test%20Inventory.md) records current tool-test ownership and known partial branch coverage.

The remaining files are retained for design rationale, implementation history and validation evidence. Relative cross-links are preserved by keeping related document families together in this directory.
