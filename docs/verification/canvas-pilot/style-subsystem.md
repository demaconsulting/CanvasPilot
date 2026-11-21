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
- Kits that are in the program, the folder of the user, the project, and the shared folders are found with the order of
  precedence, a folder that is not there is reported and does not stop the others, and a shared kit that is copied into the
  project becomes the kit of its id.
- A kit that is exported to a zip file and imported is the same kit; a second import of the same id is refused, and an archive
  with a name that climbs out of the target is refused with nothing created outside the target.
- No assembly that the application library refers to, no package that the test project resolves, and no package that the
  project file of the application library declares is a language model or agent library.

### Test Scenarios

**StyleSubsystem_DefineKits_WrittenKitFolder_IsReadBackWithItsExamplesAndWarnings**: A kit with a heading font that is not
bundled, Mermaid and Vega-Lite parts, advice, a size preset, and one caption is checked, written with the example files of a
folder, and read back from the folder. The check and the read give the one warning about the font and no error; the kit that is
read has the same canonical text as the kit that was written, both example files in name order with the caption on the file that
has one, the colors in file order, and example paths that exist. This scenario is tested by
`StyleSubsystem_DefineKits_WrittenKitFolder_IsReadBackWithItsExamplesAndWarnings`.

**StyleSubsystem_FindKitsInLocations_AllPlaces_ResolveByPrecedenceAndSurviveAnUnreachableFolder**: With the real project
service and settings service, a project is created and kits are written in the folder of the program, the folder of the
user, a shared folder, the other shared folder that is not there at all, and the project. The kit of the user wins over the
shared and program copies of its id; the missing folder is the one place that is not available and the other four places
are read; a shared kit that is copied into the project is the kit of its id, with the shared one overridden; and a second
catalog that starts as after a restart finds all of that again. This scenario is tested by
`StyleSubsystem_FindKitsInLocations_AllPlaces_ResolveByPrecedenceAndSurviveAnUnreachableFolder`.

**StyleSubsystem_ExchangeKitsAsZipFiles_ExportImportAndHostileArchive_MoveKitsAndChangeNothingOutside**: A kit that was read
from a folder is exported to a zip file and imported into another place; the kit that arrives has the same canonical text. A
second import of the same file is refused because the id is taken, and an archive with a name that climbs out of the target
is refused; the target holds only the one kit and nothing was created beside it. This scenario is tested by
`StyleSubsystem_ExchangeKitsAsZipFiles_ExportImportAndHostileArchive_MoveKitsAndChangeNothingOutside`.

**StyleSubsystem_Assembly_ReferencesNoLanguageModelPackage**: The assemblies that the application library refers to, the
packages in the dependency file of the test project, and the package references in the project file of the application library
do not include the SDKs of GitHub Copilot, OllamaSharp, the AI extensions of Microsoft, AgentKit, or the other libraries of
language models that are listed. This scenario is tested by `StyleSubsystem_Assembly_ReferencesNoLanguageModelPackage`.
