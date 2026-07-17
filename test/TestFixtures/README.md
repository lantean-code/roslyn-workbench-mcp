# Compiled Test Fixtures

This directory contains projects that are built solely to produce runtime inputs for tests. They are solution build participants, but they are not test assemblies and contain no test runner packages.

This differs from `../TestAssets`: assets there are inert checked-in inputs that must not be built or mutated in place. Projects here are intentionally compiled through normal project references so tests can exercise real assembly discovery, metadata, dependency loading and failure behaviour.

## Plugins

`Plugins` contains small production-shaped plugin assemblies used by Host integration and process acceptance tests. Keep each project limited to the minimum plugin shapes required by its discovery or loading scenario. Tests consume the compiled output; they must not add test methods or general-purpose test support to these assemblies.
