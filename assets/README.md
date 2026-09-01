# Roslyn Workbench MCP Brand Guidelines

## Brand character

Roslyn Workbench MCP is precise, compiler-aware, dependable and developer-focused. The visual identity should communicate safe tooling and semantic understanding rather than generic “AI magic”.

## Logo

The primary icon combines a spanner, Roslyn-style semantic graph, code braces and angle brackets. Use the locked artwork without redrawing, stretching, rotating or rearranging its elements. The icon background is always solid **Dark Indigo**.

In the wordmark, **Roslyn Workbench** uses Dark Indigo text with a thin Soft White outline for visibility on dark backgrounds; **MCP** is Emerald with no outline.

The locked source artwork is:

- [`roslyn-workbench-mcp-icon.svg`](roslyn-workbench-mcp-icon.svg) for square icon usage; and
- [`roslyn-workbench-mcp-wordmark.svg`](roslyn-workbench-mcp-wordmark.svg) for product-name usage.

Raster derivatives required by a package registry or operating system must be generated from the locked icon without altering its proportions, composition or palette.

## Generated icon assets

The deterministic derivatives under [`icons`](icons/) use the locked icon artwork without redrawing or size-specific simplification.

| Asset | Intended use |
| --- | --- |
| Exact-size PNGs from 16×16 through 96×96 | Windows, installer, shortcut and high-DPI surfaces where a PNG is required |
| `roslyn-workbench-mcp-128.png` | Embedded NuGet `PackageIcon` and a universally supported MCP icon |
| `roslyn-workbench-mcp-256.png` | Higher-resolution MCP, documentation and installer surfaces |
| `roslyn-workbench-mcp-512.png` | High-resolution source for future packaging formats that require a large PNG |
| `roslyn-workbench-mcp.ico` | Windows executable, shortcut and MSI `ARPPRODUCTICON` use |

The Windows ICO contains 16×16, 20×20, 24×24, 30×30, 32×32, 36×36, 40×40, 48×48, 60×60, 64×64, 72×72, 80×80, 96×96, 128×128 and 256×256 frames. Use the 128×128 PNG for NuGet rather than the ICO or SVG. MCP metadata should prefer the SVG with `sizes: ["any"]` when the consumer supports it and provide the 128×128 or 256×256 PNG as the safe, universally supported alternative.

## Colour palette

| Colour | Hex | Primary use |
| --- | --- | --- |
| Dark Indigo | `#1A1D3D` | Icon background, primary text and dark surfaces |
| Steel Gray | `#5B6472` | Secondary text, borders and supporting UI |
| Soft White | `#F5F7FA` | Braces, outlines and light backgrounds |
| Emerald | `#10B981` | MCP, success states and safe or transactional cues |
| Aqua | `#22D3EE` | Spanner, chevrons, graph accents and highlights |

## Typography

Use a clean modern sans-serif such as **Inter** or **Segoe UI**. Headings should be bold and direct; supporting text should remain restrained and highly readable.

## Imagery and tone

Prefer syntax trees, semantic graphs, code structure, refactoring previews and transactional workflows. Avoid robots, chat bubbles, excessive sparkles and generic futuristic AI imagery.

## Usage

Preserve generous clear space around the logo, maintain its original proportions and use only the approved palette. Avoid placing the icon or wordmark over visually busy backgrounds.
