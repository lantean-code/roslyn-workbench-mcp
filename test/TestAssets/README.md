# Test Assets

This directory contains checked-in inputs for integration and acceptance tests. Assets may include complete project and solution files, source code, configuration, imported build files and binary fixtures.

## Project scope

A `.csproj`, `.sln` or `.slnx` file in this directory is test data. Its presence does not make it part of `Roslyn.Workbench.Mcp.slnx` and does not cause it to be built by the repository build.

Asset files are included by test-support projects as `Content` and copied to their output directories. Tests must then materialise a unique temporary copy before explicitly loading a project or solution through Roslyn and MSBuild. Any design-time build output must be created under that temporary copy, never under this directory.

Do not add an asset project to the repository solution or reference it with `ProjectReference`. Intentionally compiled fixture projects belong under `../TestFixtures` instead.

## Authoring rules

- Treat every checked-in asset as immutable test input.
- Never open, build, restore or mutate an asset project in place.
- Materialise assets through the shared test asset materialiser.
- Do not check in `bin`, `obj`, `.vs`, recovery state or other generated output.
- Do not include machine-specific absolute paths, credentials or secrets.
- Preserve intentional encodings, byte-order marks, line endings and binary contents.
- Keep intentionally malformed inputs explicit and give their directories descriptive names.
- Use tokens only for values that genuinely must vary, such as an absolute path or unique assembly name.

Profile directories may overlay files from a `Base` template. A profile-local `.asset-delete` file lists relative paths that the materialiser removes after applying the overlay; the manifest itself is never copied into the workspace.

## Template boundaries

Reuse an existing template when scenarios have the same material project shape. Mutable, external-change, recovery and locking scenarios obtain isolation from unique materialised copies rather than duplicated templates.

Create a separate template or explicit profile when a scenario depends on a materially different project graph, SDK or project format, document set, build configuration, encoding, malformed input, or other structure that should remain readable without consulting fixture-generation code.
