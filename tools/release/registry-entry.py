#!/usr/bin/env python3
"""Prepare and anonymously verify the public Host's MCP Registry manifest."""

from __future__ import annotations

import argparse
import hashlib
import io
import json
import re
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path


SERVER_NAME = "io.github.lantean-code/roslyn-workbench-mcp"
PACKAGE_ID = "Lantean.Roslyn.Workbench.Mcp"
REGISTRY_URL = "https://registry.modelcontextprotocol.io"
REPOSITORY_URL = "https://github.com/lantean-code/roslyn-workbench-mcp"
RELEASE_VERSION_PATTERN = (
    r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)"
    r"(?:-[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*)?"
)


def validate_version(version: str) -> None:
    if not re.fullmatch(RELEASE_VERSION_PATTERN, version):
        raise ValueError("Expected a release package version, without a tag prefix or build metadata.")


def download(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "roslyn-workbench-registry-publication"})

    with urllib.request.urlopen(request, timeout=60) as response:
        return response.read()


def read_public_entry(version: str) -> dict | None:
    name = urllib.parse.quote(SERVER_NAME, safe="")
    url = f"{REGISTRY_URL}/v0.1/servers/{name}/versions/{version}"

    try:
        return json.loads(download(url))
    except urllib.error.HTTPError as error:
        if error.code == 404:
            return None

        raise


def validate_manifest(manifest: dict, version: str) -> None:
    expected_package = {
        "registryType": "nuget",
        "registryBaseUrl": "https://api.nuget.org/v3/index.json",
        "identifier": PACKAGE_ID,
        "version": version,
        "runtimeHint": "dnx",
        "transport": {"type": "stdio"},
        "packageArguments": [],
    }

    expected_icon = {
        "src": f"https://raw.githubusercontent.com/lantean-code/roslyn-workbench-mcp/{version}/assets/icons/roslyn-workbench-mcp-128.png",
        "mimeType": "image/png",
        "sizes": ["128x128"],
    }

    expected = {
        "name": SERVER_NAME,
        "version": version,
        "websiteUrl": f"https://lantean-code.github.io/roslyn-workbench-mcp/{version}/",
        "repository": {"url": REPOSITORY_URL, "source": "github"},
        "packages": [expected_package],
        "icons": [expected_icon],
    }

    for key, value in expected.items():
        if manifest.get(key) != value:
            raise ValueError(f"Packaged manifest has an unexpected {key}.")

    if manifest.get("remotes"):
        raise ValueError("The Host listing must not contain remote transports.")


def assert_matching_entry(entry: dict, manifest: dict) -> None:
    actual = entry["server"]

    for key, value in manifest.items():
        if key == "$schema":
            continue

        actual_value = actual.get(key)
        if key == "packages":
            actual_value = [dict(package) for package in actual_value or []]

            for package in actual_value:
                package.setdefault("packageArguments", [])

        if actual_value != value:
            raise ValueError(f"Published Registry entry conflicts with the packaged manifest: {key}.")

    if actual.get("remotes"):
        raise ValueError("Published Registry entry unexpectedly contains remote transports.")

    metadata = entry["_meta"]["io.modelcontextprotocol.registry/official"]
    if metadata["status"] != "active":
        raise ValueError("Published Registry entry is not active.")


def prepare(version: str, output: Path, github_output: Path | None) -> None:
    validate_version(version)
    package_base = f"https://api.nuget.org/v3-flatcontainer/{PACKAGE_ID.lower()}/{version}"
    package_bytes = download(f"{package_base}/{PACKAGE_ID.lower()}.{version}.nupkg")

    with zipfile.ZipFile(io.BytesIO(package_bytes)) as package:
        if package.namelist().count(".mcp/server.json") != 1:
            raise ValueError("Expected exactly one packaged .mcp/server.json.")

        manifest_bytes = package.read(".mcp/server.json")

    manifest = json.loads(manifest_bytes)
    validate_manifest(manifest, version)
    readme = download(f"{package_base}/readme").decode("utf-8-sig")
    if f"<!-- mcp-name: {SERVER_NAME} -->" not in readme:
        raise ValueError("The public exact-version README lacks the Registry ownership marker.")

    download(manifest["websiteUrl"])
    download(manifest["icons"][0]["src"])
    entry = read_public_entry(version)
    if entry is not None:
        assert_matching_entry(entry, manifest)

    output.mkdir(parents=True, exist_ok=True)
    (output / "server.json").write_bytes(manifest_bytes)
    evidence = {
        "version": version,
        "packageSha256": hashlib.sha256(package_bytes).hexdigest(),
        "manifestSha256": hashlib.sha256(manifest_bytes).hexdigest(),
        "alreadyPublished": entry is not None,
    }

    (output / "preflight.json").write_text(json.dumps(evidence, indent=2) + "\n", encoding="utf-8")
    if github_output is not None:
        with github_output.open("a", encoding="utf-8") as stream:
            stream.write(f"already-published={str(entry is not None).lower()}\n")

    print(f"Public Host {version} verified; matching Registry entry present: {entry is not None}.")


def verify(version: str, output: Path) -> None:
    validate_version(version)
    manifest = json.loads((output / "server.json").read_bytes())
    validate_manifest(manifest, version)
    entry = read_public_entry(version)
    if entry is None:
        raise ValueError("The exact-version Registry entry is not anonymously available.")

    assert_matching_entry(entry, manifest)
    (output / "registry-response.json").write_text(json.dumps(entry, indent=2) + "\n", encoding="utf-8")
    print(f"Verified active anonymous Registry entry: {SERVER_NAME} {version}.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("operation", choices=("prepare", "verify"))
    parser.add_argument("--version", required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--github-output", type=Path)
    args = parser.parse_args()

    if args.operation == "prepare":
        prepare(args.version, args.output, args.github_output)
    else:
        verify(args.version, args.output)


if __name__ == "__main__":
    main()
