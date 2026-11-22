## Style

### Verification Approach

The `Style` subsystem is verified by integration tests defined in `StyleSubsystemTests.cs`. The tests use the real units
of the subsystem together, on temporary folders, the way the later units and the application use them: a kit is built,
checked, written to a folder with its example files, and read back from that folder. They check the observable result:
the kit that is read is the kit that was written, the examples are found from the files of the folder, and the warnings
are the ones that the kit calls for. The subsystem is built up unit by unit, and the scenarios below are the ones that
exist; each unit that is added extends the subsystem tests with the scenarios that need more than one unit. One test
also checks that the subsystem refers to no type or source file of a language model or agent library.

### Test Environment

N/A - standard test environment. The tests use temporary folders and the dependency file of the test project and the
project file of the application library from the repository; no network, display, or special permission is needed.

### Acceptance Criteria

- All subsystem tests pass with zero failures.
- A kit that is written to a folder and read back is the same kit, in the same canonical text, with its examples listed
  from the files of the `examples` folder, each with its caption, and with a warning for a font that not every format
  draws.
- Kits that are in the program, the folder of the user, the project, and the shared folders are found with the order of
  precedence, a folder that is not there is reported and does not stop the others, and a shared kit that is copied into
  the project becomes the kit of its id.
- A kit that is exported to a zip file and imported is the same kit; a second import of the same id is refused, and an
  archive with a name that climbs out of the target is refused with nothing created outside the target.
- The default kit of the user becomes the style of a new project and is kept in the project file; another set of
  services that opens the file finds the same kit; a kit that cannot be found is reported as missing.
- The export sizes that the style supplies are offered by the exporter of the `Preview` subsystem, the sizes of the kit
  first, and an export at the size of the kit is a PNG of exactly that size over the background of the kit.
- A picture that is drawn from a file is checked against the kit that the catalog finds: a picture of the colors of the
  kit passes, and a picture with another color fails and names it.
- The tools of an assistant over the real units give the style of the project (within the budget), check a picture of
  the project and name the color that is off the palette, and refuse a file outside the project.
- No type of the subsystem uses a type of a language model or agent library in its signatures, and no source file of
  the subsystem has a using directive for such a library.

### Test Scenarios

**StyleSubsystem_DefineKits_WrittenKitFolder_IsReadBackWithItsExamplesAndWarnings**: A kit with a heading font that is
not bundled, Mermaid and Vega-Lite parts, advice, a size preset, and one caption is checked, written with the example
files of a folder, and read back from the folder. The check and the read give the one warning about the font and no
error; the kit that is read has the same canonical text as the kit that was written, both example files in name order
with the caption on the file that has one, the colors in file order, and example paths that exist. This scenario is
tested by `StyleSubsystem_DefineKits_WrittenKitFolder_IsReadBackWithItsExamplesAndWarnings`.

**StyleSubsystem_FindKitsInLocations_AllPlaces_ResolveByPrecedenceAndSurviveAnUnreachableFolder**: With the real project
service and settings service, a project is created and kits are written in the folder of the program, the folder of the
user, a shared folder, the other shared folder that is not there at all, and the project. The kit of the user wins over
the shared and program copies of its id; the missing folder is the one place that is not available and the other four
places are read; a shared kit that is copied into the project is the kit of its id, with the shared one overridden; and
a second catalog that starts as after a restart finds all of that again. This scenario is tested by
`StyleSubsystem_FindKitsInLocations_AllPlaces_ResolveByPrecedenceAndSurviveAnUnreachableFolder`.

**StyleSubsystem_ExchangeKitsAsZipFiles_ExportImportAndHostileArchive_MoveKitsAndChangeNothingOutside**: A kit that was
read from a folder is exported to a zip file and imported into another place; the kit that arrives has the same
canonical text. A second import of the same file is refused because the id is taken, and an archive with a name that
climbs out of the target is refused; the target holds only the one kit and nothing was created beside it. This scenario
is tested by `StyleSubsystem_ExchangeKitsAsZipFiles_ExportImportAndHostileArchive_MoveKitsAndChangeNothingOutside`.

**StyleSubsystem_ChooseActiveKit_DefaultAndChoice_AreKeptInTheProjectFileAndSurviveReopening**: With a kit of the user
as the default kit in the real settings and a new project, applying the default writes `activeStyleKit` into the project
file. A second project service, catalog, and selection that open the file find the same kit as the active kit; naming a
kit that does not exist then reports it as missing, with its id and no kit. This scenario is tested by
`StyleSubsystem_ChooseActiveKit_DefaultAndChoice_AreKeptInTheProjectFileAndSurviveReopening`.

**StyleSubsystem_OfferExportPresets_KitSizes_AreOfferedByTheExporterAndWriteAPngOfTheSize**: A project uses a kit of the
user with a dark background and one size, `Square` at 320 by 320. With a vector picture shown, the exporter that gets
the selection as its source of sizes offers the picture as it is shown, `Square`, and the three sizes of the
application, in that order; exporting at `Square` writes a PNG of 320 by 320 whose corner has the background of the kit,
with no message. This scenario is tested by
`StyleSubsystem_OfferExportPresets_KitSizes_AreOfferedByTheExporterAndWriteAPngOfTheSize`.

**StyleSubsystem_CheckPictures_RealRenderedPictures_PassOnAndFailOffThePalette**: Two SVG files are drawn by the real
render service and checked against a kit that the catalog finds. The one that uses white and the blue of the kit passes
with the roles `background` and `primary`; the one with half of it in `#D32F2F` fails, lists that color, has half of the
picture off the palette, and its notes all have codes that start with `style-`. This scenario is tested by
`StyleSubsystem_CheckPictures_RealRenderedPictures_PassOnAndFailOffThePalette`.

**StyleSubsystem_GuideTheAssistant_ToolsOverRealUnits_GiveTheStyleAndCheckAPictureOfTheProject**: A real project uses a
kit of the user through the real selection. `style_get` gives exactly the guidance of the kit, within the budget, and
the line that ends a cut block names the tool; `style_check` with no arguments checks the picture that is shown, fails
it with half of the pixels off the palette and names the color, and a path that climbs out of the project is refused.
This scenario is tested by
`StyleSubsystem_GuideTheAssistant_ToolsOverRealUnits_GiveTheStyleAndCheckAPictureOfTheProject`.

**StyleSubsystem_Types_ReferenceNoLanguageModelPackage**: The types of the subsystem, in their base types, interfaces,
fields, properties, events, constructors, and methods, use no type of the SDKs of GitHub Copilot, OllamaSharp, the AI
extensions of Microsoft, the Microsoft Agent Framework, AgentKit, or the other libraries of language models that are
listed, and no source file in the folder of the subsystem has a using directive for them. The application library as a
whole refers to the libraries of the AI copilot, so the rule is checked on the subsystem and not on the assembly. This
scenario is tested by `StyleSubsystem_Types_ReferenceNoLanguageModelPackage`.
