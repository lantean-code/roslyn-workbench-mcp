# Windows MSIX packaging

This Windows Application Packaging Project builds an unsigned x64 development MSIX using Microsoft's Visual Studio and Windows SDK tools. It packages the existing framework-dependent Host without changing the application or using FireGiant's commercial MSIX extension. The project appears under Packaging in the solution; normal solution builds and deployments exclude it.

## Build

Install Visual Studio's **MSIX Packaging Tools** optional component and **Windows 11 SDK (10.0.28000.0)**, alongside PowerShell 7 and the .NET SDK pinned by `global.json`. The .NET desktop development workload alone does not necessarily install the Windows SDK. Restart Visual Studio after adding components if the project remains unavailable.

Run from the repository root in Windows PowerShell 7:

```powershell
./packaging/windows/build-msix.ps1
./packaging/windows/build-msix.ps1 -Version 0.1.0-beta.1
```

The script discovers Visual Studio MSBuild, publishes the Windows Host, and builds the package using the checked-in artwork. It supplies the packaging project's external payload and generated manifest; direct project builds need those inputs too. `-MSBuildPath` can select a particular Visual Studio installation and `-WindowsSdkVersion` can select another installed compatible SDK. The .NET CLI's MSBuild cannot replace Visual Studio MSBuild for this project.

Each run creates a unique directory under `artifacts/msix`, containing the published Host, packaging intermediates, final `.msix` and adjacent SHA-256 checksum. `-OutputDirectory` can select another new directory; an existing directory is rejected, never cleared. Local builds default to `0.0.0-dev` and do not run GitVersion. GitHub workflows do not build or publish MSIX packages yet.

The package includes the Host apphost, DLLs, bundled plugins, satellite assemblies, portable PDBs, notices and MIT licence. Windows generates a Package Resource Index (PRI) for the qualified artwork. The Host content is marked `ExcludeFromResourceIndex` because .NET resolves its own satellite assemblies; this does not exclude those files from the package. The build verifies every published Host file against its packaged copy. It does not install the package, change PATH, create or trust a certificate, or submit anything to Microsoft Store.

## Manifest authoring

The manifest and [Assets](Assets/) are available in the project without first running the packaging script. Open `Package.appxmanifest` in Visual Studio's manifest designer to inspect the visual assets. The XML remains authoritative for declarations that the designer does not expose.

The 57 PNGs are raster derivatives of the locked [`roslyn-workbench-mcp-icon.svg`](../../../assets/roslyn-workbench-mcp-icon.svg), rendered directly at their destination resolution. The 44-pixel application logo, 150-pixel medium tile and 50-pixel Store logo each have 100%, 125%, 150%, 200% and 400% variants. The application logo also has 16, 20, 24, 30, 32, 36, 40, 48, 60, 64, 72, 80, 96 and 256 pixel target-size variants, each in default, unplated and light-unplated forms. These shell variants intentionally retain the same Dark Indigo-backed artwork; the qualifier prevents an extra Windows backplate, not the logo's own background. Regenerate derivatives from the locked SVG when the artwork changes, rather than enlarging a smaller PNG. Normal package builds consume the checked-in files and require no image-rendering dependency.

The manifest sections were assessed against the current full-trust, stdio-only Host:

| Area | Decision |
| --- | --- |
| Package and application descriptions | Both describe the server; the package-level description is not left empty merely because it is optional. |
| Visual assets | Include qualified application, medium-tile and Store logos. Splash screens, wide/large tiles, badges and lock-screen presentation do not apply to this non-interactive server. |
| Capabilities | Declare `runFullTrust`. Ordinary .NET file and network access already runs with the user's authority; do not add AppContainer Internet/library permissions, `broadFileSystemAccess`, elevation or device permissions without a corresponding API requirement. |
| Declarations | Register the console execution alias and permit independent server instances. Do not register file associations, URI handlers, Windows services, startup tasks or background tasks: the MCP client owns server launch. |
| Content URIs | Intentionally absent. These govern hosted web-content access to Windows functionality; they are not documentation, support or outbound-network allowlist URLs. |
| Store website, support and privacy | Configure these in Partner Center when preparing the Store submission, using the public documentation site, support route and reviewed privacy information. They do not belong in Content URIs. |
| Identity and dependencies | Development identity and Windows desktop dependency are explicit. Store identity and .NET runtime prerequisite handling remain outstanding below. |
| State virtualisation | No opt-out capability is added speculatively. Verify recovery/state access and retention in the installed package before deciding whether an explicit policy is needed. |

Microsoft's guidance covers [icon variants](https://learn.microsoft.com/windows/apps/design/style/iconography/app-icon-construction), [full-trust capabilities](https://learn.microsoft.com/windows/uwp/packaging/app-capability-declarations), [Content URI rules](https://learn.microsoft.com/uwp/schemas/appxpackage/uapmanifestschema/element-uap-applicationcontenturirules) and [Store support/privacy metadata](https://learn.microsoft.com/windows/apps/publish/publish-your-app/msix/support-info).

## Identity and runtime

The checked-in identity is deliberately for development: `LanteanCode.RoslynWorkbenchMcp.Development`, publisher `CN=Lantean Code Development`. It is not a reserved Store identity or evidence of signing authority. Before Store distribution, associate the project with the actual Partner Center product and use its exact identity and publisher. NuGet's reserved package prefix does not supply a Store identity.

MSIX uses four numeric version components. The script maps the Host's `Major.Minor.Patch` to `Major.Minor.Patch.0`, preserving the full semantic version in the Host and output filename. Prerelease labels are not ordered by MSIX: `0.1.0-beta.1` and `0.1.0-beta.2` both map to `0.1.0.0`. Do not treat this as MSI's same-version major-upgrade behaviour; use distinct numerical versions for distributed updates. The Store's version and update policy must be settled before publication.

The manifest targets Windows desktop, with a minimum build of 19041, and requests full-trust execution as the current user, not administrator elevation. It declares a console execution alias, `roslyn-workbench-mcp.exe`, and permits multiple server instances. There is no Start menu entry because the server is launched by an MCP client, not as an interactive application.

The Host still requires the .NET 10 x64 runtime. This development MSIX neither bundles nor installs it, and MSIX does not inherit the MSI's custom prerequisite check. Installing the package alone therefore does not establish that the server can run. Runtime prerequisite handling remains a distribution decision before Store publication; SDK/MSBuild availability remains visible through server status.

## Installed compatibility checks

An unsigned MSIX is a build artefact, not an installable public download. Local sideloading needs a matching signature and appropriate certificate trust, or an explicitly chosen development registration route. Signing, trust changes and package registration require separate approval. Store-managed signing is a later distribution step.

Before declaring MSIX support validated, use an isolated Windows account or explicitly approved test setup to check:

- Alias launch with redirected stdin/stdout, MCP initialisation, tool discovery and independent simultaneous clients.
- `server-status`, SDK/MSBuild discovery, opening an existing repository, queries and a preview/rollback workflow.
- Default state-directory and recovery behaviour across restarts, including Windows package data virtualisation and removal semantics. Do not assume MSIX keeps the MSI's state location or retention behaviour.
- Loading bundled plugins from the protected installation directory and optional trusted plugins from an external directory supplied through `--plugin-directory`; never copy plugins into WindowsApps.
- Missing-runtime behaviour, package updates/removal and alias conflicts with an existing MSI or .NET global tool installation.

The alias is managed through Windows app execution aliases, not the MSI's optional PATH feature. Existing MSI/global-tool commands can take precedence on PATH. Confirm which executable the client launches, and do not replace an existing installation or change its configuration as an incidental packaging test.

A successful package build and payload inspection do not validate installed activation, resource access, Store acceptance or upgrade behaviour.
