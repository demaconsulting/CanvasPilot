## WiX Toolset

This document describes the integration and usage design for the `WiX Toolset` OTS software item.

### Purpose

The WiX Toolset (the `WixToolset.Sdk` MSBuild SDK with the `WixToolset.Util.wixext` extension) is the tool chosen to
build the Windows Installer (MSI) package of CanvasPilot. The application is delivered for 64-bit Windows as one
per-machine package, and WiX is the established open source way to describe such a package as source files that are
built and reviewed like code. The SDK and the extension are pinned in the installer project to a release line that
predates the Open Source Maintenance Fee license terms of later WiX releases, so the build carries no license fee; a
move to a later line is a deliberate decision about those terms, not a routine update.

### Features Used

- The `WixToolset.Sdk` MSBuild SDK, which builds a `Package` project (`DemaConsulting.CanvasPilot.Installer.wixproj`)
  into an `.msi` file with `dotnet build`.
- The package definition `Package.wxs`: per-machine, 64-bit scope; a fixed upgrade code with a major upgrade that
  replaces any installed version; an embedded cabinet; the icon and the help and about links shown in Add/Remove
  Programs; a Start Menu shortcut; and the install folder under `Program Files`.
- The `Files` harvesting element, which adds every file of the published win-x64 output folder, including the files in
  its sub-folders (the pattern `**\*`; a pattern that ends in `**` harvests only the top folder), to the install folder
  with the same folder structure, so that the style kits in the `StyleKits` folder beside the executable are installed.
  Each harvested file is its own component with a component code derived from its path, so the same file keeps the
  same component code from one build to the next.
- The `util:CloseApplication` element of `WixToolset.Util.wixext`, which sends a running `CanvasPilot.exe` a close
  message and, if it has not exited after 5 seconds, ends the process. The extension schedules its action
  just before the files are installed, which is after Windows Installer has validated the installation and after the
  major upgrade has removed the installed version.

### Integration Pattern

The installer project is a separate project that is not part of the solution, so the cross-platform restore and build of
the solution never needs WiX. Only the Windows build job uses it, after the application is published self-contained as a
single file for `win-x64` with the debug symbols and documentation files removed. The job computes two versions from the
build version. The numeric MSI version drops any prerelease suffix and any build metadata, because Windows Installer
needs `major.minor.build` with major and minor at most 255 and build at most 65535; the package version drops only the
build metadata, because a `+` is awkward in a file name and download address and the metadata does not tell two
releases apart. The job builds the installer project with the numeric version, the package version, and the publish
folder as properties, straight into the package folder. The numeric version (`Version`) reaches `Package.wxs` as a
preprocessor variable and is the `ProductVersion`, so Add/Remove Programs shows the numeric version (for example
`0.2.0` for `0.2.0-beta.3`). The package version (`MsiPackageVersion`, which defaults to the numeric version in a local
build) only names the package: the project names its output `CanvasPilot-{package version}-win-x64.msi`, so the build
copies or renames nothing.

WiX has no self-validation mode, so the evidence that it works is the FileAssert test `WiX_MsiPackage` (tag
`installer`), run against the built package in the same job; its results file is an input of requirements enforcement.
A step of the same job then writes the tables of the built package that decide how it installs (`Property`,
`Directory`, `Media`, `File`, `Shortcut`, `Upgrade`, `Wix4CloseApplication`, and `InstallExecuteSequence`) and its
summary template to `artifacts/msi-tables.xml`, read through the Windows Installer automation interface, and the
`Installer_*` FileAssert tests of the same tag assert those rows as the evidence of the system installer requirement.
The same job asserts, with other FileAssert tests of the same tag, that the published folder holds the native speech
runtime and the style kits that come with the program (the `StyleKits` folder), and a step reads the file table of the
package to check that the installer took those files, also those of the sub-folders. What the installed package does
when it runs is checked by hand before a release. The package is never built or installed by the tests.

#### Package Decisions

- **Any installed version is replaced.** `MajorUpgrade` with `AllowDowngrades="yes"` gives one `Upgrade` row with no
  maximum version that is not detect-only, so an older, the same, or a newer installed version is removed and there is
  never a second entry in Add/Remove Programs or a refusal to install. `AllowSameVersionUpgrades` is not combined with
  it, because the downgrade setting already covers the same version.
- **ICE61 is suppressed.** ICE61 warns about an `Upgrade` row without a maximum version, which is exactly the row that
  the decision above asks for, so the warning is a known false positive for this package and is suppressed in the
  project file rather than acted on.
- **The installed version is removed first.** The major upgrade keeps the default schedule of WiX, which removes the
  installed version right after the installation is validated and before the new files are installed. This is the
  schedule that does not depend on the component rules holding across versions, which harvested files cannot promise.
  The cost is accepted: when the new installation then fails or is cancelled, the removal is not rolled back, and the
  user installs the package again.
- **A running application is closed without a question.** The close message lets CanvasPilot close as it does when the
  user closes the window; after 5 seconds the process is ended (with exit code 1), so an open application never keeps
  the installation from replacing its files, at the risk of losing work that was not saved within that time. Windows
  Installer allows ending the process only without a reboot prompt, so `RebootPrompt="no"`. `PromptToContinue` is not
  set, so the `Description` of the element is not shown to the user; it is kept as the record of what the action does
  in the package's table. Whether Windows Installer's own files-in-use check, which runs during validation and so
  before this action, asks the user first in an interactive upgrade is open and is to be settled by a check by hand.
- **The shortcut component's key path is a value under `HKCU`.** The Start Menu shortcut is not advertised, and the
  Windows Installer validation rules (ICE38, ICE43, ICE57) require such a component in the Start Menu folder, which is
  a per-user folder unless the package is installed per machine, to have a value under `HKCU` as its key path. The
  value `HKCU\Software\DEMA Consulting\CanvasPilot\Installed` is therefore written to the profile of the user who
  installs and removed from the profile of the user who uninstalls or upgrades. When those are different users, the
  value stays in the profile of the user who installed. This leftover is accepted: it is one number that nothing
  reads, and the shortcut itself is installed and removed for all users.
- **Modify and Repair are hidden.** The package has no WixUI dialog sequence, so `ARPNOMODIFY` and `ARPNOREPAIR` hide
  the Modify and Repair buttons of Add/Remove Programs rather than offer actions with no user interface.
