## WiX Toolset

This document describes the integration and usage design for the `WiX Toolset` OTS software item.

### Purpose

The WiX Toolset (v5, as the `WixToolset.Sdk` MSBuild SDK with the `WixToolset.Util.wixext` extension) is the tool
chosen to build the Windows Installer (MSI) package of CanvasPilot. The application is delivered for 64-bit Windows
as one per-machine package, and WiX is the established open source way to describe such a package as source files
that are built and reviewed like code. The 5.0.x line is pinned on purpose: it predates the Open Source Maintenance
Fee license terms that started with WiX v6.

### Features Used

- The `WixToolset.Sdk` MSBuild SDK, which builds a `Package` project (`DemaConsulting.CanvasPilot.Installer.wixproj`)
  into an `.msi` file with `dotnet build`.
- The package definition `Package.wxs`: per-machine, 64-bit scope; a fixed upgrade code with a major upgrade that
  replaces any installed version; an embedded cabinet; the icon and the help and about links shown in Add/Remove
  Programs; a Start Menu shortcut; and the install folder under `Program Files`.
- The `Files` harvesting element, which adds every file of the published win-x64 output folder, including the files in
  its sub-folders (the pattern `**\*`; a pattern that ends in `**` harvests only the top folder), to the install folder
  with the same folder structure, so that the style kits in the `StyleKits` folder beside the executable are installed.
- The `util:CloseApplication` element of `WixToolset.Util.wixext`, which asks a running `CanvasPilot.exe` to close
  and ends it after a short time, so that files are replaced on upgrade.

### Integration Pattern

The installer project is a separate project that is not part of the solution, so the cross-platform restore and build
of the solution never needs WiX. Only the Windows build job uses it, after the application is published self-contained
as a single file for `win-x64` with the debug symbols and documentation files removed: it computes the numeric MSI
version from the build version (a prerelease suffix cannot be represented and is removed), builds the installer
project with that version and the publish folder as properties, and copies the package to
`CanvasPilot-{version}-win-x64.msi`. The properties reach `Package.wxs` as preprocessor variables. WiX has no
self-validation mode, so the evidence that it works is the FileAssert test `WiX_MsiPackage` (tag `installer`), run
against the copied package in the same job; its results file is an input of requirements enforcement. The same job
asserts, with other FileAssert tests of the same tag, that the published folder holds the native speech runtime and the
style kits that come with the program (the `StyleKits` folder), and a
step reads the file table of the package to check that the installer took those files, also those of the sub-folders. What the
installed package does is checked by hand before a release. The package is never built or installed by the tests.
