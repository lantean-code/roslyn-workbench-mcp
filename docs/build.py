#!/usr/bin/env python3
"""Build the generated reference and strict MkDocs site from the compiled Host."""

from __future__ import annotations

import argparse
import json
import os
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
    source_revision = subprocess.run(
        ["git", "rev-parse", "HEAD"],
        cwd=repository_root,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    notes_command = [
        sys.executable, str(repository_root / "tools/release/render-release-notes.py"),
        "--output", str(docs_directory / "content/release-notes.md"),
    ]
    if os.environ.get("RoslynWorkbenchReleaseBuild") == "true":
        notes_command.extend(["--version", os.environ["RoslynWorkbenchVersion"]])
    subprocess.run(notes_command, cwd=repository_root, check=True)
    generated_reference = docs_directory / "content" / "reference" / "tools"
    generated_security_reference = docs_directory / "content" / "reference" / "security"
    generated_assets = docs_directory / "content" / "assets" / "generated"
    security_manifest = docs_directory / "security" / "security-invariants.json"

    generator_project = repository_root / "tools" / "Roslyn.Workbench.Mcp.ToolReferenceGenerator"
    restore_projects = get_restore_projects(repository_root, generator_project, security_manifest)
    build_command = [
        "dotnet",
        "build",
        str(generator_project),
        "--configuration",
        arguments.configuration,
        "--no-restore",
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
        "--security-output",
        str(generated_security_reference),
        "--security-baseline",
        str(docs_directory / "security" / "security-surface-v1.json"),
        "--security-manifest",
        str(security_manifest),
        "--repository-root",
        str(repository_root),
        "--source-revision",
        source_revision,
    ]
    artifacts_arguments: list[str] = []
    if "microsoft" in platform.release().lower():
        artifacts_argument = "--artifacts-path=/tmp/artifacts/roslyn-workbench-mcp"
        artifacts_arguments.append(artifacts_argument)
        build_command.append(artifacts_argument)
        generator_command[7:7] = [artifacts_argument]

    for restore_project in restore_projects:
        restore_command = [
            "dotnet",
            "restore",
            str(restore_project),
            "-p:RestoreUseStaticGraphEvaluation=true",
            *artifacts_arguments,
        ]

        subprocess.run(restore_command, cwd=repository_root, check=True)

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


def get_restore_projects(repository_root: Path, generator_project: Path, security_manifest: Path) -> list[Path]:
    manifest = json.loads(security_manifest.read_text(encoding="utf-8"))
    generator_project_file = generator_project / f"{generator_project.name}.csproj"
    evidence_projects = {
        repository_root / evidence["project"]
        for invariant in manifest["entries"]
        for evidence in invariant["evidence"]
    }

    return [generator_project_file, *sorted(evidence_projects)]


if __name__ == "__main__":
    raise SystemExit(main())
