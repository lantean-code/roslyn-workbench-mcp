#!/usr/bin/env python3
"""Build the generated reference and strict MkDocs site from the compiled Host."""

from __future__ import annotations

import argparse
import platform
import shutil
import subprocess
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--configuration", default="Debug")
    parser.add_argument("--skip-mkdocs", action="store_true")
    arguments = parser.parse_args()

    docs_directory = Path(__file__).resolve().parent
    repository_root = docs_directory.parent
    generated_reference = docs_directory / "content" / "reference" / "tools"
    generated_assets = docs_directory / "content" / "assets" / "generated"

    generator_project = repository_root / "tools" / "Roslyn.Workbench.Mcp.ToolReferenceGenerator"
    build_command = [
        "dotnet",
        "build",
        str(generator_project),
        "--configuration",
        arguments.configuration,
        "-m:1",
    ]
    generator_command = [
        "dotnet",
        "run",
        "--project",
        str(generator_project),
        "--configuration",
        arguments.configuration,
        "--no-build",
        "--",
        "--output",
        str(generated_reference),
        "--examples",
        str(docs_directory / "examples" / "tool-reference-examples.json"),
    ]
    if "microsoft" in platform.release().lower():
        artifacts_argument = "--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp"
        build_command.append(artifacts_argument)
        generator_command[7:7] = [artifacts_argument]

    subprocess.run(build_command, cwd=repository_root, check=True)
    subprocess.run(generator_command, cwd=repository_root, check=True)

    generated_assets.mkdir(parents=True, exist_ok=True)
    shutil.copy2(repository_root / "assets" / "roslyn-workbench-mcp-icon.svg", generated_assets)
    shutil.copy2(repository_root / "assets" / "roslyn-workbench-mcp-wordmark.svg", generated_assets)

    if not arguments.skip_mkdocs:
        subprocess.run(
            [sys.executable, "-m", "mkdocs", "build", "--strict", "--config-file", str(docs_directory / "mkdocs.yml")],
            cwd=repository_root,
            check=True,
        )

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
