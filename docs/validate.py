#!/usr/bin/env python3
"""Validate generated documentation content and the rendered site."""

from __future__ import annotations

import argparse
import json
import os
import re
import time
import urllib.error
import urllib.request
from html.parser import HTMLParser
from pathlib import Path
from urllib.parse import unquote, urljoin, urlparse


_external_resource_pattern = re.compile(r"(?i)(?:https?:)?//[^\s'\"()<>]+")
_style_import_pattern = re.compile(r"(?is)@import\s+(['\"])(.*?)\1")
_style_comment_pattern = re.compile(r"/\*.*?\*/", re.DOTALL)
_style_url_pattern = re.compile(r"(?is)url\(\s*(['\"]?)(.*?)\1\s*\)")


class PageParser(HTMLParser):
    """Collect page metadata and navigable links from rendered HTML."""

    def __init__(self) -> None:
        super().__init__()
        self.in_title = False
        self.title = ""
        self.h1_count = 0
        self.documentation_version: str | None = None
        self.visible_documentation_version: str | None = None
        self.links: list[str] = []
        self.ids: set[str] = set()
        self.images_without_alt: list[str] = []
        self.external_resources: list[str] = []
        self.in_style = False

    def handle_starttag(self, tag: str, attributes: list[tuple[str, str | None]]) -> None:
        values = dict(attributes)
        if values.get("id"):
            self.ids.add(values["id"] or "")
        if tag == "title":
            self.in_title = True
        elif tag == "style":
            self.in_style = True
        elif tag == "h1":
            self.h1_count += 1
        elif tag == "meta" and values.get("name") == "roslyn-workbench-version":
            self.documentation_version = values.get("content")
        elif values.get("data-rw-documentation-version"):
            self.visible_documentation_version = values["data-rw-documentation-version"]
        elif tag == "a" and values.get("href"):
            self.links.append(values["href"] or "")
        elif tag == "img":
            source = values.get("src") or ""
            if not values.get("alt"):
                self.images_without_alt.append(source)

        inline_style = values.get("style")
        if inline_style:
            self.external_resources.extend(find_external_style_resources(inline_style))

        source_set = values.get("srcset")
        if source_set:
            self.external_resources.extend(find_external_resources(source_set))

        resource_attributes = {
            "audio": ("src",),
            "embed": ("src",),
            "iframe": ("src",),
            "image": ("href", "xlink:href"),
            "img": ("src",),
            "link": ("href",),
            "object": ("data",),
            "script": ("src", "href", "xlink:href"),
            "source": ("src",),
            "track": ("src",),
            "use": ("href", "xlink:href"),
            "video": ("src", "poster"),
        }.get(tag, ())
        if tag == "input" and (values.get("type") or "").lower() == "image":
            resource_attributes = ("src",)
        if tag == "link":
            relationships = set((values.get("rel") or "").lower().split())
            loading_relationships = {"dns-prefetch", "icon", "manifest", "modulepreload", "preconnect", "prefetch", "preload", "stylesheet"}
            if relationships.isdisjoint(loading_relationships):
                resource_attributes = ()
        for resource_attribute in resource_attributes:
            resource = values.get(resource_attribute)
            if resource and is_external_resource(resource):
                self.external_resources.append(resource)

    def handle_endtag(self, tag: str) -> None:
        if tag == "title":
            self.in_title = False
        elif tag == "style":
            self.in_style = False

    def handle_data(self, data: str) -> None:
        if self.in_title:
            self.title += data
        if self.in_style:
            self.external_resources.extend(find_external_style_resources(data))


def is_external_resource(value: str) -> bool:
    normalized_value = value.strip()
    parsed = urlparse(normalized_value)
    return parsed.scheme in {"http", "https"} or bool(parsed.netloc)


def find_external_resources(value: str) -> list[str]:
    return _external_resource_pattern.findall(value)


def find_external_style_resources(value: str) -> list[str]:
    searchable_value = _style_comment_pattern.sub("", value)
    candidates = [match.group(2).strip() for match in _style_url_pattern.finditer(searchable_value)]
    candidates.extend(match.group(2).strip() for match in _style_import_pattern.finditer(searchable_value))
    return [candidate for candidate in candidates if is_external_resource(candidate)]


def parse_deployment_version(value: str) -> str:
    normalized_value = value.strip("/")
    if (
        not normalized_value
        or normalized_value in {".", ".."}
        or "/" in normalized_value
        or "\\" in normalized_value
    ):
        raise argparse.ArgumentTypeError("deployment version must be one non-empty URL path segment")
    return normalized_value


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check-external", action="store_true")
    parser.add_argument("--allow-unpublished-project-links", action="store_true")
    parser.add_argument("--deployment-version", type=parse_deployment_version)
    arguments = parser.parse_args()

    docs_directory = Path(__file__).resolve().parent
    reference_directory = docs_directory / "content" / "reference" / "tools"
    site_directory = docs_directory / "site"
    validate_reference(reference_directory)
    external_links = validate_site(site_directory, arguments.deployment_version)
    if arguments.check_external:
        validate_external_links(external_links, arguments.allow_unpublished_project_links)
    return 0


def validate_reference(reference_directory: Path) -> None:
    catalog = read_json(reference_directory / "catalog.json")
    validate_json_reference(reference_directory / "catalog.json", catalog.get("$schema"), reference_directory)
    validate_schema_identity(reference_directory / "schemas" / "tool-catalog.schema.json")
    validate_schema_identity(reference_directory / "schemas" / "tool-detail.schema.json")
    tools = catalog.get("tools")
    if not isinstance(tools, list) or not tools:
        raise ValueError("The generated tool catalog is empty.")

    names = [tool.get("name") for tool in tools]
    if names != sorted(names) or len(names) != len(set(names)):
        raise ValueError("Generated tools must be unique and ordered by protocol name.")

    for tool in tools:
        name = tool["name"]
        detail = read_json(reference_directory / "data" / f"{name}.json")
        validate_json_reference(
            reference_directory / "data" / f"{name}.json",
            detail.get("$schema"),
            reference_directory,
        )
        if detail.get("name") != name or detail.get("tool", {}).get("name") != name:
            raise ValueError(f"Generated detail for '{name}' does not match the catalog.")
        if "inputSchema" not in detail["tool"] or "outputSchema" not in detail["tool"]:
            raise ValueError(f"Generated detail for '{name}' is missing a production schema.")
        if not (reference_directory / f"{name}.md").is_file():
            raise ValueError(f"Generated Markdown page for '{name}' is missing.")

    expected_details = {f"{name}.json" for name in names}
    actual_details = {path.name for path in (reference_directory / "data").glob("*.json")}
    if actual_details != expected_details:
        raise ValueError("Generated detail files do not exactly match the tool catalog.")


def validate_schema_identity(schema_file: Path) -> None:
    schema = read_json(schema_file)
    schema_id = schema.get("$id")
    if not isinstance(schema_id, str) or urlparse(schema_id).scheme:
        raise ValueError(f"'{schema_file}' must use a relative schema identifier.")
    validate_json_reference(schema_file, schema_id, schema_file.parent)


def validate_json_reference(source_file: Path, reference: object, allowed_root: Path) -> None:
    if not isinstance(reference, str) or urlparse(reference).scheme:
        raise ValueError(f"'{source_file}' must use a relative JSON schema reference.")

    target = Path(os.path.normpath(source_file.parent / unquote(urlparse(reference).path)))
    if not target.is_relative_to(allowed_root) or not target.is_file():
        raise ValueError(f"'{source_file}' contains unresolved JSON schema reference '{reference}'.")


def validate_site(site_directory: Path, deployment_version: str | None) -> set[str]:
    if not site_directory.is_dir():
        raise ValueError("The rendered documentation site does not exist.")

    html_files = sorted(site_directory.rglob("*.html"))
    if not html_files:
        raise ValueError("The rendered documentation site contains no HTML pages.")

    parsed_pages: dict[Path, PageParser] = {}
    validated_internal_links: set[tuple[Path, str]] = set()
    external_links: set[str] = set()
    forbidden_text = ("google-analytics", "googletagmanager", "plausible.io", "cookieconsent")
    for style_file in sorted(site_directory.rglob("*.css")):
        content = style_file.read_text(encoding="utf-8")
        external_resources = find_external_style_resources(content)
        if external_resources:
            raise ValueError(f"'{style_file}' contains unexpected external resources: {external_resources}.")

    for html_file in html_files:
        content = html_file.read_text(encoding="utf-8")
        lowered = content.lower()
        if any(value in lowered for value in forbidden_text):
            raise ValueError(f"Tracking or cookie integration found in '{html_file}'.")

        page = PageParser()
        page.feed(content)
        parsed_pages[html_file] = page
        if not page.title.strip() or page.h1_count != 1:
            raise ValueError(f"'{html_file}' must contain a title and exactly one h1.")
        if not page.documentation_version or page.visible_documentation_version != page.documentation_version:
            raise ValueError(f"'{html_file}' must identify its documentation version visibly and in metadata.")
        if page.images_without_alt:
            raise ValueError(f"'{html_file}' contains images without alternative text.")
        if page.external_resources:
            raise ValueError(f"'{html_file}' contains unexpected external resources: {page.external_resources}.")

    for html_file, page in parsed_pages.items():
        for link in page.links:
            parsed = urlparse(link)
            if parsed.scheme in {"http", "https"}:
                external_links.add(link)
            elif parsed.scheme or link.startswith("mailto:"):
                continue
            else:
                validate_internal_link(
                    site_directory,
                    html_file,
                    link,
                    deployment_version,
                    parsed_pages,
                    validated_internal_links,
                )

    return external_links


def validate_internal_link(
    site_directory: Path,
    source_file: Path,
    link: str,
    deployment_version: str | None,
    parsed_pages: dict[Path, PageParser],
    validated_links: set[tuple[Path, str]],
) -> None:
    parsed_link = urlparse(link)
    path_text = unquote(parsed_link.path)
    if not path_text:
        candidate = source_file
    elif path_text.startswith("/"):
        site_path_prefix = "/roslyn-workbench-mcp/"
        if path_text.startswith(site_path_prefix):
            path_text = path_text[len(site_path_prefix) :]
        if deployment_version is not None:
            deployment_prefix = f"{deployment_version}/"
            if path_text == deployment_version:
                path_text = ""
            elif path_text.startswith(deployment_prefix):
                path_text = path_text[len(deployment_prefix) :]
        candidate = site_directory / path_text.lstrip("/")
    else:
        candidate = source_file.parent / path_text

    if not candidate.suffix:
        candidate = candidate / "index.html"

    candidate = Path(os.path.normpath(candidate))
    fragment = unquote(parsed_link.fragment)
    validation_key = (candidate, fragment)
    if validation_key in validated_links:
        return

    if not candidate.is_relative_to(site_directory):
        raise ValueError(f"Broken internal link '{link}' in '{source_file}'.")

    if candidate.suffix == ".html" and candidate not in parsed_pages:
        raise ValueError(f"Broken internal link '{link}' in '{source_file}'.")
    if candidate.suffix != ".html" and not candidate.exists():
        raise ValueError(f"Broken internal link '{link}' in '{source_file}'.")

    if parsed_link.fragment and candidate.suffix == ".html":
        if fragment not in parsed_pages[candidate].ids:
            raise ValueError(f"Broken anchor '{link}' in '{source_file}'.")

    validated_links.add(validation_key)


def validate_external_links(links: set[str], allow_unpublished_project_links: bool) -> None:
    excluded_hosts = {"localhost", "127.0.0.1"}
    unpublished_project_prefixes = (
        "https://github.com/lantean-code/roslyn-workbench-mcp",
        "https://lantean-code.github.io/roslyn-workbench-mcp",
    )
    for link in sorted(links):
        parsed = urlparse(link)
        if parsed.hostname in excluded_hosts:
            continue
        if allow_unpublished_project_links and link.startswith(unpublished_project_prefixes):
            continue

        target = urljoin(link, parsed.path)
        last_error: Exception | None = None
        for attempt in range(3):
            try:
                request = urllib.request.Request(target, method="HEAD", headers={"User-Agent": "Roslyn-Workbench-docs-validator"})
                with urllib.request.urlopen(request, timeout=15) as response:
                    if response.status >= 400:
                        raise ValueError(f"External link '{link}' returned HTTP {response.status}.")
                last_error = None
                break
            except (urllib.error.URLError, TimeoutError, ValueError) as error:
                last_error = error
                time.sleep(attempt + 1)
        if last_error is not None:
            raise ValueError(f"External link '{link}' could not be validated: {last_error}")


def read_json(path: Path) -> dict:
    with path.open(encoding="utf-8") as stream:
        value = json.load(stream)
    if not isinstance(value, dict):
        raise ValueError(f"'{path}' must contain a JSON object.")
    return value


if __name__ == "__main__":
    raise SystemExit(main())
