#!/usr/bin/env python3
"""Inspect and smoke-test an RPM; opt into installation only on a disposable Fedora system."""

import argparse
import os
import platform
import re
import subprocess
import tempfile
from pathlib import Path

from package_test_support import APPLICATION, COMMAND, PACKAGE, capture, digest, smoke


def rpm_version(version: str) -> str:
    """Map the Host semantic version to its RPM package version."""
    release, separator, prerelease = version.partition("-")
    if separator and "-" in prerelease:
        raise ValueError("RPM package versions do not support hyphens within prerelease identifiers.")
    return release + ("~" + prerelease if separator else "")


def inspect(package: Path, version: str, root: Path, publish: Path | None) -> None:
    expected = {
        "NAME": PACKAGE, "VERSION": rpm_version(version), "RELEASE": "1",
        "ARCH": "x86_64", "LICENSE": "MIT", "VENDOR": "Lantean Code",
    }
    for field, value in expected.items():
        if capture(["rpm", "-qp", "--queryformat", f"%{{{field}}}", str(package)]) != value:
            raise ValueError(f"Incorrect package {field.lower()}.")
    requirements = capture(["rpm", "-qp", "--requires", str(package)]).splitlines()
    if "dotnet-runtime-10.0 >= 10.0.0" not in requirements:
        raise ValueError("Package does not declare the required .NET 10 runtime.")
    if capture(["rpm", "-qp", "--scripts", str(package)]):
        raise ValueError("The package must not contain installation or removal scripts.")
    expected_checksum = f"{digest(package)}  {package.name}\n"
    if package.with_suffix(".rpm.sha256").read_text(encoding="ascii") != expected_checksum:
        raise ValueError("Package checksum does not match its adjacent checksum file.")

    metadata = capture([
        "rpm", "-qp", "--queryformat",
        "[%{FILENAMES}|%{FILEMODES:perms}|%{FILEUSERNAME}|%{FILEGROUPNAME}\n]", str(package),
    ])
    for entry in metadata.splitlines():
        name, mode, owner, group = entry.split("|", 3)
        if owner != "root" or group != "root":
            raise ValueError(f"Package entry is not root-owned: {name}")
        if name == f"/{APPLICATION}/{PACKAGE}":
            if mode != "-rwxr-xr-x":
                raise ValueError("Host executable permissions must be 0755.")
        elif mode.startswith("-") and mode != "-rw-r--r--":
            raise ValueError(f"Unexpected payload permissions: {name}")
        elif mode.startswith("d") and mode != "drwxr-xr-x":
            raise ValueError(f"Unexpected directory permissions: {name}")

    first = subprocess.Popen(["rpm2cpio", str(package)], stdout=subprocess.PIPE)
    try:
        subprocess.run(["cpio", "--extract", "--make-directories", "--quiet"], stdin=first.stdout, cwd=root, check=True)
    finally:
        first.stdout.close()
    if first.wait() != 0:
        raise subprocess.CalledProcessError(first.returncode, first.args)
    executable = root / APPLICATION / PACKAGE
    if executable.stat().st_mode & 0o777 != 0o755:
        raise ValueError("Extracted Host executable permissions must be 0755.")
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
        raise ValueError("--install requires root on a disposable Fedora system.")
    if subprocess.run(["rpm", "--quiet", "-q", PACKAGE]).returncode == 0 or os.path.lexists("/" + str(COMMAND)) or os.path.lexists("/" + str(APPLICATION)):
        raise ValueError("Refusing to modify an existing package, command or application directory.")
    if previous:
        if capture(["rpm", "-qp", "--queryformat", "%{NAME}", str(previous)]) != PACKAGE:
            raise ValueError("Upgrade baseline must be a Roslyn Workbench package.")
        previous_evr = capture(["rpm", "-qp", "--queryformat", "%{EVR}", str(previous)])
        selected_evr = capture(["rpm", "-qp", "--queryformat", "%{EVR}", str(package)])
        if not re.fullmatch(r"[0-9A-Za-z.+_~^-]+", previous_evr) or not re.fullmatch(r"[0-9A-Za-z.+_~^-]+", selected_evr):
            raise ValueError("Package version contains unsupported characters.")
        comparison = capture(["rpm", "--eval", f"%{{lua:print(rpm.vercmp('{previous_evr}', '{selected_evr}'))}}"])
        if comparison != "-1":
            raise ValueError("Upgrade baseline must have a lower RPM version.")
    with (logs / "dnf.log").open("w") as output:
        def dnf(*arguments: str) -> None:
            subprocess.run(["dnf", "--assumeyes", *arguments], stdout=output, stderr=subprocess.STDOUT, check=True)

        try:
            if previous:
                dnf("install", str(previous))
            dnf("install", str(package))
            verification = capture(["rpm", "--verify", PACKAGE])
            if verification:
                raise ValueError(f"Installed files differ from the package inventory:\n{verification}")
            smoke(Path("/") / COMMAND, version, logs, "installed", "rpm-packaging-check")
            dnf("reinstall", str(package))
            verification = capture(["rpm", "--verify", PACKAGE])
            if verification:
                raise ValueError(f"Reinstalled files differ from the package inventory:\n{verification}")
            smoke(Path("/") / COMMAND, version, logs, "reinstalled", "rpm-packaging-check")
        finally:
            if subprocess.run(["rpm", "--quiet", "-q", PACKAGE]).returncode == 0:
                dnf("remove", "--no-autoremove", PACKAGE)
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
    with tempfile.TemporaryDirectory(prefix="rwmcp-rpm-check-", dir="/tmp") as temporary:
        Path(temporary).chmod(0o755)
        root = Path(temporary) / "root"
        root.mkdir()
        inspect(package, args.expected_version, root, args.publish_directory)
        if args.install:
            previous = args.upgrade_from.resolve(strict=True) if args.upgrade_from else None
            installed_check(package, args.expected_version, logs, previous)
        else:
            smoke(root / COMMAND, args.expected_version, logs, "extracted", "rpm-packaging-check")
    print(f"PASS: RPM metadata, checksum, permissions, payload and MCP launch. Logs: {logs}")
    if args.install:
        print("PASS: DNF installation, reinstallation and removal" + (", including upgrade." if args.upgrade_from else "."))


if __name__ == "__main__":
    main()
