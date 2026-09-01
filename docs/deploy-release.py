#!/usr/bin/env python3
"""Publish one immutable release documentation version and move the latest alias."""

from __future__ import annotations

import argparse
import json
import os
import shutil
import subprocess
import sys
from pathlib import Path


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("source_tag")
    parser.add_argument("--commit")
    parser.add_argument("--full-semver")
    parser.add_argument("--source-distance", default="0")
    parser.add_argument("--dry-run", action="store_true")
    arguments = parser.parse_args()

    docs_directory = Path(__file__).resolve().parent
    repository_root = docs_directory.parent
    config_file = docs_directory / "mkdocs.yml"
    commit = arguments.commit or run_output(["git", "rev-parse", "HEAD"], repository_root)
    full_semver = arguments.full_semver or arguments.source_tag
    build_environment = create_release_environment(
        arguments.source_tag,
        full_semver,
        commit,
        arguments.source_distance,
    )

    ensure_unpublished(arguments.source_tag, load_published_versions(repository_root))

    subprocess.run(
        [sys.executable, str(docs_directory / "build.py"), "--configuration", "Release", "--skip-mkdocs"],
        cwd=repository_root,
        check=True,
        env=build_environment,
    )
    validate_generated_identity(
        docs_directory / "content" / "reference" / "tools" / "catalog.json",
        arguments.source_tag,
        commit,
    )

    publish_rendered_documentation(
        repository_root,
        config_file,
        arguments.source_tag,
        build_environment,
        [
            sys.executable,
            str(docs_directory / "validate.py"),
            "--deployment-version",
            arguments.source_tag,
        ],
        push=not arguments.dry_run,
    )

    return 0


def publish_rendered_documentation(
    repository_root: Path,
    config_file: Path,
    source_tag: str,
    environment: dict[str, str],
    validation_command: list[str],
    *,
    push: bool,
) -> None:
    mike = resolve_mike()
    command_environment = environment.copy()
    mike_directory = str(Path(mike).parent)
    existing_path = command_environment.get("PATH")
    command_environment["PATH"] = os.pathsep.join([mike_directory, existing_path]) if existing_path else mike_directory
    starting_commit = try_run_output(["git", "rev-parse", "--verify", "refs/heads/gh-pages"], repository_root)
    try:
        subprocess.run(
            [
                mike,
                "deploy",
                "--config-file",
                str(config_file),
                "--branch",
                "gh-pages",
                "--update-aliases",
                "--alias-type",
                "redirect",
                source_tag,
                "latest",
            ],
            cwd=repository_root,
            check=True,
            env=command_environment,
        )
        subprocess.run(
            validation_command,
            cwd=repository_root,
            check=True,
            env=command_environment,
        )
        subprocess.run(
            [mike, "set-default", "--config-file", str(config_file), "--branch", "gh-pages", "latest"],
            cwd=repository_root,
            check=True,
            env=command_environment,
        )
    except Exception:
        restore_publication_branch(repository_root, starting_commit)
        raise

    if push:
        subprocess.run(["git", "push", "origin", "gh-pages"], cwd=repository_root, check=True)
    else:
        restore_publication_branch(repository_root, starting_commit)


def create_release_environment(
    source_tag: str,
    full_semver: str,
    commit: str,
    source_distance: str,
) -> dict[str, str]:
    environment = os.environ.copy()
    environment.update(
        {
            "RoslynWorkbenchReleaseBuild": "true",
            "RoslynWorkbenchVersion": source_tag,
            "RoslynWorkbenchFullSemVer": full_semver,
            "RoslynWorkbenchCommitSha": commit,
            "RoslynWorkbenchVersionSourceDistance": source_distance,
            "RoslynWorkbenchSourceTag": source_tag,
        }
    )
    return environment


def load_published_versions(repository_root: Path) -> set[str]:
    remote_branch = subprocess.run(
        ["git", "ls-remote", "--heads", "origin", "refs/heads/gh-pages"],
        cwd=repository_root,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()
    if not remote_branch:
        return set()

    subprocess.run(
        ["git", "fetch", "--no-tags", "origin", "+refs/heads/gh-pages:refs/remotes/origin/gh-pages"],
        cwd=repository_root,
        check=True,
    )
    inventory_text = run_output(
        ["git", "show", "refs/remotes/origin/gh-pages:versions.json"],
        repository_root,
    )
    inventory = json.loads(inventory_text)
    if not isinstance(inventory, list):
        raise ValueError("The published Mike version inventory must be a JSON array.")

    versions: set[str] = set()
    for entry in inventory:
        if not isinstance(entry, dict) or not isinstance(entry.get("version"), str):
            raise ValueError("Every published Mike version must contain a string version field.")
        versions.add(entry["version"])
    return versions


def ensure_unpublished(source_tag: str, published_versions: set[str]) -> None:
    if source_tag in published_versions:
        raise ValueError(f"Documentation version '{source_tag}' already exists and will not be overwritten.")


def validate_generated_identity(
    catalog_file: Path,
    source_tag: str,
    commit: str,
) -> None:
    with catalog_file.open(encoding="utf-8") as stream:
        catalog = json.load(stream)

    expected = {
        "sourceTag": source_tag,
        "productVersion": source_tag,
        "commit": commit,
    }
    actual = {name: catalog.get(name) for name in expected}
    if actual != expected:
        raise ValueError(f"Generated documentation identity {actual!r} does not match release identity {expected!r}.")


def resolve_mike() -> str:
    beside_python = Path(sys.executable).with_name("mike")
    if beside_python.is_file():
        return str(beside_python)

    mike = shutil.which("mike")
    if mike is None:
        raise FileNotFoundError("The Mike CLI is not installed or available on PATH.")
    return mike


def run_output(command: list[str], repository_root: Path) -> str:
    return subprocess.run(
        command,
        cwd=repository_root,
        check=True,
        capture_output=True,
        text=True,
    ).stdout.strip()


def try_run_output(command: list[str], repository_root: Path) -> str | None:
    result = subprocess.run(
        command,
        cwd=repository_root,
        check=False,
        capture_output=True,
        text=True,
    )
    return result.stdout.strip() if result.returncode == 0 else None


def restore_publication_branch(repository_root: Path, starting_commit: str | None) -> None:
    command = ["git", "update-ref", "refs/heads/gh-pages"]
    if starting_commit is None:
        command.insert(2, "-d")
    else:
        command.append(starting_commit)
    subprocess.run(command, cwd=repository_root, check=True)


if __name__ == "__main__":
    raise SystemExit(main())
