# OTS Verification

This section provides verification evidence for all Off-The-Shelf (OTS) software items
used by CanvasPilot: the runtime libraries Avalonia, CommunityToolkit.Mvvm, CanvasNet, and Speech; the WiX Toolset
that builds the installer package; the xUnit test framework; and the build, documentation, and analysis tools
BuildMark, FileAssert, Pandoc, ReqStream, ReviewMark, SarifMark, SonarMark, SysML2Tools, VersionMark, and WeasyPrint.
The per-item files under `docs/verification/ots/` cover the verification evidence for each individual OTS item.

## Verification Strategy

Each OTS item is verified by the strongest evidence it can supply: the vendor's self-validation report where the tool
has one, integration tests that call a runtime library the way the application does, and pipeline output assertions
that exercise the specific features this project uses. A successful CI run, in which every OTS tool produces its
expected outputs and the downstream steps consume them, is itself evidence that each tool performs its required
function.

## Qualification Evidence

The evidence collected for each OTS item is the TRX test results produced by the tool's self-validation suite or by
the OTS tests in `test/OtsSoftwareTests`, and the FileAssert results for the documents and packages the build
generates. These results are linked to the OTS requirements in `docs/reqstream/ots/` and reported in the trace matrix,
so every feature listed in an item's integration design is covered by named test evidence.

## Regression Approach

When an OTS version is upgraded, the pipeline reruns every self-validation suite, OTS test, and FileAssert assertion
against the new version. The vendor's release notes are reviewed, and any integration point affected by a change is
re-qualified before the upgrade is merged. A failing self-validation test, OTS test, or assertion blocks the upgrade.

## Scope

Each OTS item is verified by a combination of:

- The OTS tool's own self-validation suite (for the tools that provide one)
- Pipeline output assertions performed by FileAssert on documents and packages produced by the item
- For runtime libraries without a self-validation mode, integration tests in `test/OtsSoftwareTests` that call
  the library the way the application does

Internal OTS design is out of scope; only integration and usage evidence is documented.

## OTS Items

The following OTS items have verification evidence in this section:

- Avalonia (*avalonia.md*) — runtime user interface framework
- BuildMark (*buildmark.md*) — build-notes documentation tool
- CanvasNet (*canvasnet.md*) — runtime library that draws image source files
- CommunityToolkit.Mvvm (*communitytoolkit-mvvm.md*) — runtime view model source generators
- FileAssert (*fileassert.md*) — document assertion tool
- Pandoc (*pandoc.md*) — Markdown-to-HTML conversion tool
- ReqStream (*reqstream.md*) — requirements traceability tool
- ReviewMark (*reviewmark.md*) — file review enforcement tool
- SarifMark (*sarifmark.md*) — SARIF report conversion tool
- SonarMark (*sonarmark.md*) — SonarCloud quality report tool
- Speech (*speech.md*) — runtime library for audio devices, speech models, and their parameters
- SysML2Tools (*sysml2tools.md*) — architecture model validation and diagram rendering tool
- VersionMark (*versionmark.md*) — tool-version documentation tool
- WeasyPrint (*weasyprint.md*) — HTML-to-PDF conversion tool
- WiX Toolset (*wix.md*) — Windows installer package build tool
- xUnit (*xunit.md*) — unit-testing framework
