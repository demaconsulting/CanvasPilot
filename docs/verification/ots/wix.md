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
`artifacts/fileassert-installer-windows-latest.trx`, which the requirements enforcement step reads. The assertion proves that
exactly one package file exists and that it is not trivially small, so a build that failed silently, produced no
package, or produced an empty one fails the job. If WiX were non-functional, the build step that precedes the
assertion would fail first. What the installed package does (the Start Menu shortcut, closing a running
application, and replacing an earlier version in place) is checked by hand on a clean machine before each release
and is not part of the automated evidence.

### Test Scenarios

#### WiX_MsiPackage

**Scenario**: After the installer project is built with a numeric version over the published win-x64 output and the
package is copied to the package folder, FileAssert asserts the file pattern `package/CanvasPilot-*-win-x64.msi`.

**Expected**: Exactly one file matches and its size is at least 10,000,000 bytes.

**Requirement coverage**: `CanvasPilot-OTS-WiX-BuildMsiPackage`, and through it `CanvasPilot-System-WindowsInstaller`.
