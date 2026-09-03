#!/usr/bin/env python3
"""Inspect and smoke-test a built DEB; opt into installation only on a disposable Linux system."""

import argparse
import os
import platform
import subprocess
import tarfile
import tempfile
from pathlib import Path

from package_test_support import APPLICATION, COMMAND, PACKAGE, capture, digest, smoke


def inspect(package: Path, version: str, root: Path, publish: Path | None) -> None:
    expected = {
        "Package": PACKAGE, "Version": version.replace("-", "~", 1) + "-1",
        "Architecture": "amd64", "Depends": "dotnet-runtime-10.0 (>= 10.0.0)",
    }
    for field, value in expected.items():
        if capture(["dpkg-deb", "--field", str(package), field]) != value:
            raise ValueError(f"Incorrect package {field}.")
    expected_checksum = f"{digest(package)}  {package.name}\n"
    if package.with_suffix(".deb.sha256").read_text(encoding="ascii") != expected_checksum:
        raise ValueError("Package checksum does not match its adjacent checksum file.")
    archive_path = root.parent / "payload.tar"
    with archive_path.open("wb") as stream:
        subprocess.run(["dpkg-deb", "--fsys-tarfile", str(package)], stdout=stream, check=True)
    with tarfile.open(archive_path) as archive:
        for member in archive:
            if member.uid != 0 or member.gid != 0:
                raise ValueError(f"Package entry is not root-owned: {member.name}")
            path = Path(member.name)
            if member.isfile() and path != APPLICATION / PACKAGE and member.mode != 0o644:
                raise ValueError(f"Unexpected payload permissions: {member.name}")
            if member.isdir() and member.mode != 0o755:
                raise ValueError(f"Unexpected directory permissions: {member.name}")
    subprocess.run(["dpkg-deb", "--raw-extract", str(package), str(root)], check=True)
    if {path.name for path in (root / "DEBIAN").iterdir()} != {"control", "md5sums"}:
        raise ValueError("The package must not contain maintainer scripts or configuration hooks.")
    subprocess.run(["md5sum", "--check", "--status", "DEBIAN/md5sums"], cwd=root, check=True)
    executable = root / APPLICATION / PACKAGE
    if executable.stat().st_mode & 0o777 != 0o755:
        raise ValueError("Host executable permissions must be 0755.")
    if os.readlink(root / COMMAND) != "../lib/roslyn-workbench-mcp/roslyn-workbench-mcp":
        raise ValueError("Incorrect command symlink.")
    for name in ("Roslyn.Workbench.Mcp.dll", "Roslyn.Workbench.Mcp.pdb", "Roslyn.Workbench.Mcp.runtimeconfig.json", "Roslyn.Workbench.Mcp.Plugins.Core.dll", "LICENSE", "THIRD-PARTY-NOTICES.md"):
        if not (root / APPLICATION / name).is_file():
            raise ValueError(f"Missing published Host file: {name}")
    if publish:
        originals = {path.relative_to(publish) for path in publish.rglob("*") if path.is_file()}
        packaged = {path.relative_to(root / APPLICATION) for path in (root / APPLICATION).rglob("*") if path.is_file()}
        if originals != packaged:
            raise ValueError("Packaged file inventory differs from the published Host.")
        for relative in originals:
            if digest(publish / relative) != digest(root / APPLICATION / relative):
                raise ValueError(f"Packaged Host file differs: {relative}")


def installed_check(package: Path, version: str, logs: Path, previous: Path | None) -> None:
    if os.geteuid() != 0:
        raise ValueError("--install requires root on a disposable Linux system.")
    status = subprocess.run(["dpkg-query", "--show", "--showformat=${db:Status-Abbrev}", PACKAGE], capture_output=True, text=True)
    if status.returncode == 0 or os.path.lexists("/" + str(COMMAND)) or os.path.lexists("/" + str(APPLICATION)):
        raise ValueError("Refusing to modify an existing package, command or application directory.")
    if previous:
        if capture(["dpkg-deb", "--field", str(previous), "Package"]) != PACKAGE:
            raise ValueError("Upgrade baseline must be a Roslyn Workbench package.")
        subprocess.run(["dpkg", "--compare-versions", capture(["dpkg-deb", "--field", str(previous), "Version"]), "lt", capture(["dpkg-deb", "--field", str(package), "Version"])], check=True)
    with (logs / "apt.log").open("w") as output:
        def apt(*arguments: str) -> None:
            subprocess.run(["apt-get", "--yes", "--no-install-recommends", *arguments], stdout=output, stderr=subprocess.STDOUT, check=True)

        try:
            if previous:
                apt("install", str(previous))
            apt("install", str(package))
            verification = capture(["dpkg", "--verify", PACKAGE])
            if verification:
                raise ValueError(f"Installed files differ from the package inventory:\n{verification}")
            smoke(Path("/") / COMMAND, version, logs, "installed", "deb-packaging-check")
            apt("install", "--reinstall", str(package))
            verification = capture(["dpkg", "--verify", PACKAGE])
            if verification:
                raise ValueError(f"Reinstalled files differ from the package inventory:\n{verification}")
            smoke(Path("/") / COMMAND, version, logs, "reinstalled", "deb-packaging-check")
        finally:
            status = subprocess.run(["dpkg-query", "--show", "--showformat=${db:Status-Abbrev}", PACKAGE], capture_output=True, text=True)
            if status.returncode == 0:
                apt("remove", PACKAGE)
    if os.path.lexists("/" + str(COMMAND)) or os.path.exists("/" + str(APPLICATION)):
        raise ValueError("Removal left installer-owned paths behind.")


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", required=True, type=Path)
    parser.add_argument("--expected-version", required=True)
    parser.add_argument("--publish-directory", type=Path)
    parser.add_argument("--install", action="store_true")
    parser.add_argument("--upgrade-from", type=Path)
    args = parser.parse_args()
    if platform.system() != "Linux" or platform.machine() != "x86_64":
        raise ValueError("Run package validation on Linux x64.")
    if args.upgrade_from and not args.install:
        raise ValueError("--upgrade-from requires --install.")
    package = args.package.resolve(strict=True)
    logs = Path(tempfile.mkdtemp(prefix="validation-", dir=package.parent))
    with tempfile.TemporaryDirectory(prefix="rwmcp-deb-check-", dir="/tmp") as temporary:
        # Permit the unprivileged smoke-test child to traverse the extracted files.
        Path(temporary).chmod(0o755)
        root = Path(temporary) / "root"
        inspect(package, args.expected_version, root, args.publish_directory)
        if args.install:
            previous = args.upgrade_from.resolve(strict=True) if args.upgrade_from else None
            installed_check(package, args.expected_version, logs, previous)
        else:
            smoke(root / COMMAND, args.expected_version, logs, "extracted", "deb-packaging-check")
    print(f"PASS: DEB metadata, checksum, permissions, payload and MCP launch. Logs: {logs}")
    if args.install:
        print("PASS: APT installation, reinstallation and removal" + (", including upgrade." if args.upgrade_from else "."))


if __name__ == "__main__":
    main()
