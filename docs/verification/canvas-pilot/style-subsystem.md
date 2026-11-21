## Style

### Verification Approach

The `Style` subsystem is verified by integration tests defined in `StyleSubsystemTests.cs`. The tests use the real units of the
subsystem together, on temporary folders, the way the later units and the application use them: a kit is built, checked,
written to a folder with its example files, and read back from that folder. They check the observable result: the kit that is
read is the kit that was written, the examples are found from the files of the folder, and the warnings are the ones that the
kit calls for. The subsystem is built up unit by unit, and the scenarios below are the ones that exist; each unit that is added
extends the subsystem tests with the scenarios that need more than one unit. One test also checks that the application library
refers to no package of a language model or agent library.

### Test Environment

N/A - standard test environment. The tests use temporary folders and the dependency file of the test project and the project
file of the application library from the repository; no network, display, or special permission is needed.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A kit that is written to a folder and read back is the same kit, in the same canonical text, with its examples listed from
  the files of the `examples` folder, each with its caption, and with a warning for a font that not every format draws.
- No assembly that the application library refers to, no package that the test project resolves, and no package that the
  project file of the application library declares is a language model or agent library.

### Test Scenarios

**StyleSubsystem_DefineKits_WrittenKitFolder_IsReadBackWithItsExamplesAndWarnings**: A kit with a heading font that is not
bundled, Mermaid and Vega-Lite parts, advice, a size preset, and one caption is checked, written with the example files of a
folder, and read back from the folder. The check and the read give the one warning about the font and no error; the kit that is
read has the same canonical text as the kit that was written, both example files in name order with the caption on the file that
has one, the colors in file order, and example paths that exist. This scenario is tested by
`StyleSubsystem_DefineKits_WrittenKitFolder_IsReadBackWithItsExamplesAndWarnings`.

**StyleSubsystem_Assembly_ReferencesNoLanguageModelPackage**: The assemblies that the application library refers to, the
packages in the dependency file of the test project, and the package references in the project file of the application library
do not include the SDKs of GitHub Copilot, OllamaSharp, the AI extensions of Microsoft, AgentKit, or the other libraries of
language models that are listed. This scenario is tested by `StyleSubsystem_Assembly_ReferencesNoLanguageModelPackage`.
