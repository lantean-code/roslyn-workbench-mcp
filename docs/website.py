#!/usr/bin/env python3
"""Compose the product site, versioned docs and legacy compatibility tree.

The default command only writes an output directory. --update-branch is an
explicit CI operation; it never checks out files or pushes a branch.
"""

from __future__ import annotations

import argparse
import hashlib
import gzip
import html
from html.parser import HTMLParser
import io
import json
import os
from pathlib import Path
import posixpath
import re
import shutil
import subprocess
import tarfile
import tempfile
import time
import urllib.error
import urllib.request
from urllib.parse import unquote, urljoin, urlparse
import xml.etree.ElementTree as ET


DEFAULT_URL = "https://lantean-code.github.io/roslyn-workbench-mcp/"
PACKAGE = "Lantean.Roslyn.Workbench.Mcp"
REPOSITORY = Path(__file__).resolve().parent.parent
SITEMAP_NS = "http://www.sitemaps.org/schemas/sitemap/0.9"


def write_text(path: Path, value: str) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(value, encoding="utf-8", newline="\r\n")


def public_url(value: str) -> str:
    parsed = urlparse(value)
    if parsed.scheme != "https" or not parsed.netloc or parsed.query or parsed.fragment:
        raise ValueError("The public site URL must be an absolute HTTPS URL without a query or fragment.")
    return value.rstrip("/") + "/"


def segment(value: object) -> str:
    if not isinstance(value, str) or re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*", value) is None:
        raise ValueError(f"Invalid documentation path segment: {value!r}.")
    return value


def read_inventory(root: Path) -> list[dict]:
    inventory = json.loads((root / "versions.json").read_text(encoding="utf-8"))
    if not isinstance(inventory, list) or not inventory:
        raise ValueError("The Mike inventory must be a non-empty array.")
    names = set()
    for entry in inventory:
        name = segment(entry["version"])
        if name in names:
            raise ValueError(f"Duplicate version: {name}.")
        names.add(name)
        if not isinstance(entry.get("aliases"), list) or any(alias != "latest" for alias in entry["aliases"]):
            raise ValueError("Only the latest documentation alias is supported.")
    if sum("latest" in entry["aliases"] for entry in inventory) > 1:
        raise ValueError("The latest alias must identify exactly one version.")
    return inventory


def download(url: str) -> bytes:
    request = urllib.request.Request(url, headers={"User-Agent": "Roslyn-Workbench-Website"})
    with urllib.request.urlopen(request, timeout=30) as response:
        data = response.read()
        return gzip.decompress(data) if response.headers.get("Content-Encoding") == "gzip" else data


def stable_version_key(version: str) -> tuple[int, ...] | None:
    if re.fullmatch(r"\d+\.\d+\.\d+(?:\.\d+)?(?:\+[0-9A-Za-z.-]+)?", version) is None:
        return None
    parts = tuple(int(part) for part in version.split("+", 1)[0].split("."))
    return (*parts, 0) if len(parts) == 3 else parts


def resolve_release(expected: str | None = None) -> dict:
    index_url = "https://api.nuget.org/v3/index.json"
    service = json.loads(download(index_url))
    registration = next(item["@id"] for item in service["resources"] if item["@type"] == "RegistrationsBaseUrl/3.6.0")
    content_base = next(item["@id"] for item in service["resources"] if item["@type"] == "PackageBaseAddress/3.0.0")
    index = json.loads(download(f"{registration}{PACKAGE.lower()}/index.json"))
    candidates = []
    for page in index["items"]:
        leaves = page.get("items")
        if leaves is None:
            leaves = json.loads(download(page["@id"]))["items"]
        for leaf in leaves:
            entry = leaf["catalogEntry"]
            version = entry["version"]
            key = stable_version_key(version)
            if entry["id"].lower() == PACKAGE.lower() and entry.get("listed", True) is True and key is not None:
                candidates.append((key, version))
    if not candidates:
        raise ValueError("NuGet has no listed stable Host release.")
    version = max(candidates)[1]
    if expected is not None and (stable_version_key(expected) is None or stable_version_key(version) < stable_version_key(expected)):
        raise ValueError(f"NuGet has not yet exposed the expected stable release {expected}.")
    normalised = version.split("+", 1)[0].lower()
    package_url = f"{content_base}{PACKAGE.lower()}/{normalised}/{PACKAGE.lower()}.{normalised}.nupkg"
    package = download(package_url)
    if not package.startswith(b"PK"):
        raise ValueError("NuGet did not return a downloadable package archive.")
    return {"package": PACKAGE, "version": version, "source": index_url, "packageUrl": package_url,
            "sha256": hashlib.sha256(package).hexdigest()}


def redirect(target: str) -> str:
    return ('<!doctype html><html lang="en"><head><meta charset="utf-8">'
            '<meta name="robots" content="noindex"><title>Documentation moved</title>'
            f'<meta http-equiv="refresh" content="0; url={html.escape(target, quote=True)}">'
            '</head><body><p>Documentation moved. '
            f'<a href="{html.escape(target, quote=True)}">Continue to the documentation</a>.</p>'
            f'<script>location.replace({json.dumps(target)} + location.search + location.hash);</script>'
            '</body></html>\n')


def export_revision(repository: Path, revision: str, output: Path) -> None:
    archive = subprocess.run(["git", "archive", revision], cwd=repository, check=True, capture_output=True).stdout
    with tarfile.open(fileobj=io.BytesIO(archive)) as stream:
        stream.extractall(output, filter="data")


def canonicalise(root: Path, base_url: str, version: str, *, unversioned: bool = False) -> None:
    for path in root.rglob("*.html"):
        source = path.read_text(encoding="utf-8")
        # Change only the canonical tag; never rewrite examples or historical prose.
        url = base_url + "docs/" + version + "/" + path.relative_to(root).as_posix()
        updated = re.sub(r'(<link\b[^>]*\brel=[\"\']canonical[\"\'][^>]*\bhref=[\"\'])[^\"\']+',
                         lambda match: match[1] + html.escape(url, quote=True), source)
        if updated != source:
            write_text(path, updated)
    for sitemap in root.rglob("sitemap.xml"):
        tree = ET.parse(sitemap)
        for loc in tree.findall(f".//{{{SITEMAP_NS}}}loc"):
            old = urlparse(loc.text or "")
            marker = "/" + version + "/"
            if marker not in old.path and unversioned:
                marker = "/docs/"
            if marker not in old.path:
                raise ValueError(f"Unexpected historical sitemap URL: {loc.text}.")
            loc.text = base_url + "docs/" + version + "/" + old.path.split(marker, 1)[1]
        ET.register_namespace("", SITEMAP_NS)
        tree.write(sitemap, encoding="utf-8", xml_declaration=True)
        compressed = sitemap.with_suffix(".xml.gz")
        if compressed.exists():
            compressed.write_bytes(gzip.compress(sitemap.read_bytes(), mtime=0))


def relativise_navigation(root: Path, version_root: Path, versions: set[str], base_url: str) -> None:
    origins = (DEFAULT_URL, base_url)
    prefixes = tuple(urlparse(origin).path for origin in origins)
    for path in version_root.rglob("*.html"):
        source = path.read_text(encoding="utf-8")

        def replace(match: re.Match) -> str:
            url = html.unescape(match[2])
            parsed = urlparse(url)
            for origin, prefix in zip(origins, prefixes):
                if url.startswith(origin):
                    target = parsed.path[len(prefix):]
                    break
                if not parsed.netloc and parsed.path.startswith(prefix):
                    target = parsed.path[len(prefix):]
                    break
            else:
                return match[0]
            if target.split("/", 1)[0] in versions | {"latest"}:
                target = "docs/" + target
            if target.endswith("/"):
                target += "index.html"
            target = posixpath.relpath(target or "index.html", path.parent.relative_to(root).as_posix())
            if parsed.query:
                target += "?" + parsed.query
            if parsed.fragment:
                target += "#" + parsed.fragment
            return match[1] + html.escape(target, quote=True) + match[3]

        # Only navigation/resource attributes: canonical and social metadata stay absolute.
        updated = re.sub(r'(<(?:a|img|script)\b[^>]*?\b(?:href|src)=")([^"]+)(")', replace, source)
        if updated != source:
            write_text(path, updated)


def add_version_selector_assets(root: Path, version_root: Path) -> None:
    for path in version_root.rglob("*.html"):
        source = path.read_text(encoding="utf-8")
        if "data-rw-version-ui" in source or "</head>" not in source:
            continue
        relative = posixpath.relpath("assets/", path.parent.relative_to(root).as_posix())
        includes = (f'<link data-rw-version-ui rel="stylesheet" href="{relative}/docs-version.css">'
                    f'<script data-rw-version-ui src="{relative}/docs-version.js" defer></script>')
        write_text(path, source.replace("</head>", includes + "</head>", 1))


def compose(source: Path, output: Path, release: dict, base_url: str = DEFAULT_URL,
            development: Path | None = None, fresh_versions: set[str] | None = None) -> dict:
    base_url = public_url(base_url)
    if output.exists() and any(output.iterdir()):
        raise ValueError("Use a new, empty output directory for the composed site.")
    if release.get("package") != PACKAGE or stable_version_key(release.get("version", "")) is None:
        raise ValueError("The website requires a verified stable Host version.")
    output.mkdir(parents=True, exist_ok=True)
    prefixed = (source / "docs/versions.json").is_file()
    docs_source = source / "docs" if prefixed else source
    inventory = read_inventory(docs_source)
    docs = output / "docs"
    docs.mkdir()
    latest = next((entry["version"] for entry in inventory if "latest" in entry["aliases"]), None)
    previous_inventory = read_inventory(source) if prefixed and (source / "versions.json").is_file() else inventory
    previous_latest = next((entry["version"] for entry in previous_inventory if "latest" in entry["aliases"]), None)
    if development is not None and not any(entry["version"] == "dev" for entry in inventory):
        inventory.insert(0, {"version": "dev", "title": "dev", "aliases": []})
    # Retain existing compatibility assets, including versions retired by explicit cleanup.
    if prefixed:
        for child in source.iterdir():
            if child.name != "docs":
                if child.is_dir():
                    shutil.copytree(child, output / child.name)
                else:
                    shutil.copy2(child, output / child.name)
        active = {entry["version"] for entry in inventory} | {"latest", "assets"}
        for child in output.iterdir():
            retired_version = re.fullmatch(r"\d+\.\d+\.\d+(?:-(?:alpha|beta|rc)\.\d+)?", child.name) and child.name not in active
            retired_alias = child.name == "latest" and latest is None
            if child.is_dir() and (retired_version or retired_alias) and (child / "index.html").is_file():
                for path in child.rglob("*.html"):
                    target = posixpath.relpath("docs/index.html", path.parent.relative_to(output).as_posix())
                    write_text(path, '<!doctype html><html lang="en"><head><meta charset="utf-8">'
                               '<meta name="robots" content="noindex"><title>Documentation retired</title></head>'
                               '<body><h1>Documentation retired</h1><p>This version was retired when its release tag was removed.</p>'
                               f'<a href="{target}">Browse retained documentation</a></body></html>')
    else:
        for child in source.iterdir():
            if child.name not in {entry["version"] for entry in inventory} | {"latest", "versions.json", "index.html"}:
                if child.is_dir():
                    shutil.copytree(child, output / child.name)
                else:
                    shutil.copy2(child, output / child.name)
    mappings = []
    for entry in inventory:
        version = entry["version"]
        origin = development if version == "dev" and development is not None else docs_source / version
        if not (origin / "index.html").is_file():
            raise ValueError(f"Missing published documentation snapshot: {version}.")
        destination = docs / version
        shutil.copytree(origin, destination)
        canonicalise(destination, base_url, version, unversioned=version == "dev" and development is not None)
        add_version_selector_assets(output, destination)
        if version in (fresh_versions or set()) or version == "dev" and development is not None:
            relativise_navigation(output, destination, {entry["version"] for entry in inventory}, base_url)
        for path in sorted(origin.rglob("*")):
            if not path.is_file():
                continue
            suffix = path.relative_to(origin).as_posix()
            old = f"{version}/{suffix}"
            new = f"docs/{version}/{suffix}"
            legacy = output / old
            legacy.parent.mkdir(parents=True, exist_ok=True)
            if path.suffix == ".html":
                write_text(legacy, redirect(posixpath.relpath(new, posixpath.dirname(old))))
                kind = "redirect"
            else:
                original = source / old
                fresh = version in (fresh_versions or set()) or version == "dev" and development is not None
                shutil.copy2(original if prefixed and original.is_file() and not fresh else path, legacy)
                kind = "asset"
            mappings.append({"old": old, "new": new, "kind": kind})
        if version == latest:
            for path in destination.rglob("*"):
                if not path.is_file():
                    continue
                suffix = path.relative_to(destination).as_posix()
                for alias in ("latest/", "docs/latest/"):
                    old = alias + suffix
                    new = f"docs/{version}/{suffix}"
                    target = output / old
                    if path.suffix == ".html":
                        write_text(target, redirect(posixpath.relpath(new, posixpath.dirname(old))))
                    else:
                        target.parent.mkdir(parents=True, exist_ok=True)
                        original_alias = source / "latest" / suffix
                        original_version = output / version / suffix
                        if alias == "latest/":
                            original = original_alias if previous_latest == latest and original_alias.is_file() else original_version
                        else:
                            original = path
                        shutil.copy2(original, target)
                    mappings.append({"old": old, "new": new, "kind": "redirect" if path.suffix == ".html" else "asset"})
    write_text(docs / "versions.json", json.dumps(inventory, indent=2) + "\n")
    shutil.copy2(docs / "versions.json", output / "versions.json")
    beta_versions = [entry["version"] for entry in inventory if re.fullmatch(r"\d+\.\d+\.\d+-beta\.\d+", entry["version"])]
    beta = max(beta_versions, key=lambda value: tuple(int(part) for part in re.split(r"\.|-beta\.", value))) if beta_versions else None
    default = latest or beta or next((entry["version"] for entry in inventory if entry["version"] == "dev"), inventory[0]["version"])
    write_text(docs / "index.html", redirect(default + "/"))
    render_homepage(output, release, base_url)
    if latest is None:
        page = (output / "index.html").read_text(encoding="utf-8").replace("docs/latest/", f"docs/{default}/")
        write_text(output / "index.html", page)
    sitemap = ET.Element("urlset", xmlns=SITEMAP_NS)
    urls = {base_url}
    for entry in inventory:
        if entry["version"] == "dev":
            continue
        for path in (docs / entry["version"]).rglob("*.html"):
            text = path.read_text(encoding="utf-8")
            if 'rel="canonical"' in text and 'http-equiv="refresh"' not in text:
                urls.add(base_url + path.relative_to(output).as_posix())
    for url in sorted(urls):
        ET.SubElement(ET.SubElement(sitemap, "url"), "loc").text = url
    ET.ElementTree(sitemap).write(output / "sitemap.xml", encoding="utf-8", xml_declaration=True)
    write_text(output / ".nojekyll", "")
    manifest = {"publicBase": base_url, "latestDocs": latest, "release": release, "mappings": mappings}
    write_text(output / "migration-manifest.json", json.dumps(manifest, indent=2) + "\n")
    audit = absolute_urls(output)
    write_text(output / "absolute-urls.json", json.dumps(audit, indent=2) + "\n")
    validate(output)
    return manifest


def render_homepage(output: Path, release: dict, base_url: str) -> None:
    template = REPOSITORY / "docs/website"
    shutil.copytree(template, output, dirs_exist_ok=True)
    assets = output / "assets"
    assets.mkdir(exist_ok=True)
    shutil.copy2(REPOSITORY / "assets/roslyn-workbench-mcp-icon.svg", assets / "roslyn-icon.svg")
    wordmark = (REPOSITORY / "assets/roslyn-workbench-mcp-wordmark.svg").read_text(encoding="utf-8")
    wordmark = wordmark.replace('viewBox="0 0 1200 330"', 'viewBox="0 0 865 330"').replace("fill: #1A1D3D", "fill: #F5F7FA")
    write_text(assets / "roslyn-wordmark-dark.svg", wordmark)
    page = (template / "index.html").read_text(encoding="utf-8")
    page = page.replace("{{PUBLIC_BASE}}", base_url).replace("{{VERSION}}", release["version"])
    write_text(output / "index.html", page)


class Links(HTMLParser):
    def __init__(self) -> None:
        super().__init__(convert_charrefs=True)
        self.urls: list[tuple[str, str]] = []
        self.ids: set[str] = set()
        self.h1 = 0

    def handle_starttag(self, tag: str, attrs: list[tuple[str, str | None]]) -> None:
        values = dict(attrs)
        if values.get("id"):
            self.ids.add(values["id"])
        if tag == "h1":
            self.h1 += 1
        for key in ("href", "src"):
            if values.get(key):
                self.urls.append((tag + "." + key, values[key]))
        if tag == "meta" and values.get("content", "").startswith("https://"):
            self.urls.append(("meta." + values.get("property", values.get("name", "content")), values["content"]))


def absolute_urls(root: Path) -> list[dict]:
    result = []
    for path in sorted(root.rglob("*")):
        if path.suffix not in {".html", ".xml", ".json", ".js", ".css", ".svg"} or not path.is_file():
            continue
        if path.name in {"migration-manifest.json", "absolute-urls.json"}:
            continue
        text = path.read_text(encoding="utf-8")
        for url in sorted(set(re.findall(r'https?://[^\s<>"\x27)]+', text))):
            result.append({"file": path.relative_to(root).as_posix(), "url": html.unescape(url), "kind": "absolute"})
        if path.suffix == ".html":
            parsed = Links()
            parsed.feed(text)
            for context, url in sorted(set(parsed.urls)):
                if url.startswith("/"):
                    result.append({"file": path.relative_to(root).as_posix(), "url": url, "kind": "root-relative", "context": context})
    return result


def validate(root: Path) -> None:
    root = root.resolve()
    inventory = read_inventory(root / "docs")
    files = {Path(os.path.abspath(path)) for path in root.rglob("*") if path.is_file()}
    directories = {parent for path in files for parent in path.parents if parent.is_relative_to(root)}
    parsed = {}
    for path in sorted(files):
        if path.suffix != ".html":
            continue
        page = Links()
        page.feed(path.read_text(encoding="utf-8"))
        parsed[path] = page
    if parsed[(root / "index.html").resolve()].h1 != 1:
        raise ValueError("The product homepage must have one H1.")
    checked = set()
    manifest = root / "migration-manifest.json"
    base_path = urlparse(json.loads(manifest.read_text(encoding="utf-8"))["publicBase"]).path if manifest.exists() else urlparse(DEFAULT_URL).path
    for source, page in parsed.items():
        for context, link in page.urls:
            url = urlparse(link)
            if url.scheme or url.netloc:
                continue
            if url.path.startswith("/"):
                if not url.path.startswith(base_path):
                    raise ValueError(f"Unexpected root-relative URL in {source}: {link}")
                target = root / unquote(url.path[len(base_path):])
            else:
                target = Path(os.path.abspath(source.parent / unquote(url.path))) if url.path else source
            if url.path.endswith("/"):
                target /= "index.html"
            key = (target, url.fragment)
            if key in checked:
                continue
            if target in directories:
                target /= "index.html"
            if not target.is_relative_to(root) or target not in files:
                raise ValueError(f"Broken {context}: {source.relative_to(root)}: {link}")
            if url.fragment and target in parsed and unquote(url.fragment) not in parsed[target].ids:
                # Redirect shells preserve fragments for the final document.
                if 'http-equiv="refresh"' not in target.read_text(encoding="utf-8"):
                    raise ValueError(f"Broken anchor in {source}: {link}")
            checked.add(key)
    for entry in inventory:
        if not (root / "docs" / entry["version"] / "index.html").is_file():
            raise ValueError("Missing version homepage.")
    ET.parse(root / "sitemap.xml")
    home = (root / "index.html").read_text(encoding="utf-8")
    for block in re.findall(r'<script type="application/ld\+json">(.*?)</script>', home, re.S):
        json.loads(block)
    if "{{" in home or 'http-equiv="refresh"' in home:
        raise ValueError("The homepage must be fully rendered content.")


def update_branch(repository: Path, release: dict, base_url: str, fresh_versions: set[str] | None = None) -> None:
    branch = "refs/heads/gh-pages"
    result = subprocess.run(["git", "rev-parse", "--verify", branch], cwd=repository, capture_output=True, text=True)
    baseline = result.stdout.strip() if result.returncode == 0 else None
    revision = baseline or "refs/remotes/origin/gh-pages"
    with tempfile.TemporaryDirectory() as temporary:
        temporary_path = Path(temporary)
        source, output = temporary_path / "source", temporary_path / "output"
        export_revision(repository, revision, source)
        compose(source, output, release, base_url, fresh_versions=fresh_versions)
        git_dir = subprocess.run(["git", "rev-parse", "--absolute-git-dir"], cwd=repository, check=True, capture_output=True, text=True).stdout.strip()
        environment = os.environ.copy()
        environment["GIT_INDEX_FILE"] = str(temporary_path / "index")
        command = ["git", "--git-dir", git_dir, "--work-tree", str(output)]
        subprocess.run([*command, "read-tree", "--empty"], check=True, env=environment)
        subprocess.run([*command, "add", "--force", "--all", "--", "."], cwd=output, check=True, env=environment)
        tree = subprocess.run([*command, "write-tree"], check=True, capture_output=True, text=True, env=environment).stdout.strip()
        parent = baseline or subprocess.run(["git", "rev-parse", revision], cwd=repository, check=True, capture_output=True, text=True).stdout.strip()
        old_tree = subprocess.run(["git", "rev-parse", parent + "^{tree}"], cwd=repository, check=True, capture_output=True, text=True).stdout.strip()
        commit = parent
        if tree != old_tree:
            commit = subprocess.run(["git", "commit-tree", tree, "-p", parent, "-m", "Compose product site and versioned documentation"],
                                    cwd=repository, check=True, capture_output=True, text=True).stdout.strip()
        subprocess.run(["git", "update-ref", branch, commit, baseline or "0" * 40], cwd=repository, check=True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--source", type=Path, help="An exported publication tree; otherwise export --revision read-only.")
    parser.add_argument("--revision", default="origin/gh-pages")
    parser.add_argument("--output", type=Path, default=REPOSITORY / "artifacts/website")
    parser.add_argument("--development", type=Path, help="Fresh MkDocs output to preview as dev.")
    parser.add_argument("--fresh-version", action="append", default=[], help="Relativise navigation in a newly generated version only.")
    parser.add_argument("--public-url", default=os.environ.get("RW_SITE_URL", DEFAULT_URL))
    parser.add_argument("--release-manifest", type=Path, help="Previously verified, deterministic NuGet input for offline preview.")
    parser.add_argument("--expected-release")
    parser.add_argument("--wait-for-release", action="store_true", help="Retry NuGet propagation for up to five minutes.")
    parser.add_argument("--update-branch", action="store_true", help="CI only: commit the assembled tree to the local gh-pages ref; never push.")
    parser.add_argument("--validate", type=Path)
    arguments = parser.parse_args()
    if arguments.validate:
        validate(arguments.validate)
        return 0
    if arguments.release_manifest and arguments.update_branch:
        raise ValueError("Publication must verify NuGet live; offline manifests are for local previews only.")
    if arguments.release_manifest:
        release = json.loads(arguments.release_manifest.read_text(encoding="utf-8"))
    else:
        deadline = time.monotonic() + 300
        for attempt in range(30 if arguments.wait_for_release else 1):
            try:
                release = resolve_release(arguments.expected_release)
                break
            except (ValueError, urllib.error.URLError, TimeoutError):
                if not arguments.wait_for_release or attempt == 29 or time.monotonic() >= deadline:
                    raise
                time.sleep(10)
    base_url = public_url(arguments.public_url)
    if arguments.update_branch:
        update_branch(REPOSITORY, release, base_url, set(arguments.fresh_version))
    elif arguments.source:
        compose(arguments.source, arguments.output, release, base_url, arguments.development)
    else:
        with tempfile.TemporaryDirectory() as temporary:
            source = Path(temporary) / "source"
            export_revision(REPOSITORY, arguments.revision, source)
            compose(source, arguments.output, release, base_url, arguments.development)
    print(f"Website ready; public base {base_url}; stable Host {release['version']}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
