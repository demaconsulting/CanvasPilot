## WiX Toolset Verification

This document provides the verification evidence for the WiX Toolset OTS software item. Requirements for this OTS
item are defined in the WiX Toolset OTS Software Requirements document.

### Required Functionality

The WiX Toolset builds the published self-contained win-x64 application output and the package definition of the
installer project into one Windows Installer (MSI) package file, which is how the application is delivered.

### Verification Approach

WiX has no self-validation mode and is not exercised by a unit test, because building a package needs the published
application and takes a minute or more. The Windows build job builds the package and then runs FileAssert with the
`installer` tag against `package/CanvasPilot-*-win-x64.msi`, recording the results in
`artifacts/fileassert-installer-windows-latest.trx`, which the requirements enforcement step reads. The assertion
proves that exactly one package file exists and that it is not trivially small, so a build that failed silently,
produced no package, or produced an empty one fails the job. If WiX were non-functional, the build step that precedes
the assertion would fail first. The tables of the built package that decide how it installs (per-machine scope, the
Start Menu shortcut, the closing of a running application, and the upgrade that replaces any installed version) are
asserted in the same run by the `Installer_*` FileAssert tests, which are the evidence of the system installer
requirement and are described in the system verification document. What the installed package does when it runs is
checked by hand on a clean machine before each release and is not part of the automated evidence.

### Test Scenarios

#### WiX_MsiPackage

**Scenario**: After the installer project is built over the published win-x64 output, with the numeric version and the
package version, straight into the package folder, FileAssert asserts the file pattern
`package/CanvasPilot-*-win-x64.msi`, the name that the project gives the package.

**Expected**: Exactly one file matches and its size is at least 10,000,000 bytes.

**Requirement coverage**: `CanvasPilot-OTS-WiX-BuildMsiPackage` (Windows results only). The test proves that WiX built a
package of plausible size; it is not evidence of how the package installs.
