### StyleTools

#### Verification Approach

`StyleTools` is verified with unit tests in `StyleToolsTests.cs`. `style_get` is checked with a real catalog over kit
folders in a temporary place and a hand-written stand-in for the active style (`FakeActiveStyle`), against the text that
`StyleGuidance` writes. `style_check` is checked with an NSubstitute render service that answers with a picture, an
error, a cancelled result, or an exception, so that every mode and every error is reached without drawing; the arguments
that the renderer received are asserted, and the thread that it was called on. The containment of the path is checked
with temporary folders: a project folder, a folder outside it with a picture in it, and, where the machine allows it, a
junction (on Windows, made with `mklink /J`, which needs no privilege) or a symbolic link inside the project that leads
outside and one that leads to another place in the project. One test draws real SVG files with the real render service.
A subsystem test runs both tools over the real project service, catalog, selection, and render service.

#### Test Environment

N/A - standard test environment with temporary local folders. The tests of links skip themselves with a message on a
machine that does not let them make a junction or a link.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- The tools are named `style_get` and `style_check`, have descriptions that say when to call them, and have schemas that
  are JSON objects with no other property than `kit` and `section`, or `path` and `tolerance`; the section list of the
  schema is the list of the guidance; a missing collaborator is refused.
- `style_get` with no arguments (in each of seven ways of saying so) gives the block of the active kit; with a kit it
  gives that kit; an unknown kit is answered with the known ids in order; no kit and a missing kit are answered with how
  to choose one; a section is the whole section, in any case; an unknown section lists the sections.
- `style_check` draws the file at the full path inside the project with the project folder, on a thread of the pool, and
  answers with the text of the report; with no path it uses the picture that is shown, and says what to send when there
  is none; a full path inside the project is accepted.
- The tolerance reaches the check; 0 and 100 are accepted; a value below 0, above 100, and a string are refused.
- A relative path that climbs out (with either slash and in the middle of a path), a full path elsewhere, a network
  path, the data folder in either case, a folder, a missing file, and a name with a NUL are each refused with a message
  that says why, and the renderer is never asked; a junction that leads outside and a symbolic link to a file outside
  are refused, and a junction that leads to another place of the project is drawn.
- No project, no kit, a missing kit, a failed drawing (with its code, message, and hint), a cancelled drawing, and a
  renderer that throws are answered with messages and nothing is thrown; arguments that are not JSON, not an object, or
  of the wrong type are answered with a message; a catalog that throws does not make `style_get` throw.
- A real SVG on the palette passes and shows its palette colors; one with another color fails with the share and the
  color; a file that is not a picture is answered with the reason that it could not be drawn.

#### Test Scenarios

**StyleTools_CreateAll_Descriptors_HaveNamesDescriptionsAndSchemas**: Both descriptors have the names, descriptions, and
schemas of the design, and each factory refuses a missing collaborator. This scenario is tested by
`StyleTools_CreateAll_Descriptors_HaveNamesDescriptionsAndSchemas`.

**StyleTools_Get_NoArguments_GivesTheGuidanceOfTheActiveKit**: Nothing, blank, `null`, `{}`, null and empty values, and
an unknown property each give the block of the active kit. This scenario is tested by
`StyleTools_Get_NoArguments_GivesTheGuidanceOfTheActiveKit`.

**StyleTools_Get_NamedKit_GivesThatKitOrListsTheKnownKits**: A named kit is described even when the project uses
another; an id that no kit has is answered with `Known kits: clean, slides-dark.`. This scenario is tested by
`StyleTools_Get_NamedKit_GivesThatKitOrListsTheKnownKits`.

**StyleTools_Get_NoActiveKitOrMissingKit_SaysHowToChoose**: With no style, with a missing kit, and with no kit at all,
each answer says what is wrong and how to get a style, and the last says that no kits are known. This scenario is tested
by `StyleTools_Get_NoActiveKitOrMissingKit_SaysHowToChoose`.

**StyleTools_Get_Section_GivesThatSectionOrListsTheSections**: `Palette` and `vegaLite` (with a kit) give the sections
of the guidance, every name of the list works, and `colors` is answered with the sections. This scenario is tested by
`StyleTools_Get_Section_GivesThatSectionOrListsTheSections`.

**StyleTools_Get_BadArguments_AreAnsweredWithAMessage**: `{`, `[1]`, a string, a number for `kit`, and a boolean for
`section` are each answered with a message that names the tool and says what is wrong. This scenario is tested by
`StyleTools_Get_BadArguments_AreAnsweredWithAMessage`.

**StyleTools_Get_CatalogThrows_IsAnsweredWithAMessage**: A catalog that throws is answered with
`the tool could not answer`. This scenario is tested by `StyleTools_Get_CatalogThrows_IsAnsweredWithAMessage`.

**StyleTools_Check_RelativePath_DrawsTheFileAndAnswersWithTheReport**: A relative path is drawn at its full path with
the project folder, on a thread of the pool, and the answer is the text of the report. This scenario is tested by
`StyleTools_Check_RelativePath_DrawsTheFileAndAnswersWithTheReport`.

**StyleTools_Check_NoPathOrFullPath_UsesTheShownPictureOrTheFullPath**: With no path the picture that is shown is
checked; with neither the answer says what to send; a full path inside the project is accepted. This scenario is tested
by `StyleTools_Check_NoPathOrFullPath_UsesTheShownPictureOrTheFullPath`.

**StyleTools_Check_Tolerance_ReachesTheCheckAndIsBounded**: With a kit of only white, a color just outside the default
tolerance fails, passes with 30, fails with 0, passes with 100; -0.5, 100.5, and a string are refused. This scenario is
tested by `StyleTools_Check_Tolerance_ReachesTheCheckAndIsBounded`.

**StyleTools_Check_PathOutsideTheProject_IsRefusedBeforeDrawing**: Ten paths (three that climb out, a full path
elsewhere, a network path, the data folder in two cases, a folder, a missing file, and a name with a NUL) are each
refused with the reason, and the renderer is not asked. This scenario is tested by
`StyleTools_Check_PathOutsideTheProject_IsRefusedBeforeDrawing`.

**StyleTools_Check_LinkThatLeadsOutOfTheProject_IsRefused**: A file under a junction that leads outside is refused, a
file under a junction that leads to another folder of the project is drawn, and a symbolic link to a file outside is
refused where one can be made. This scenario is tested by `StyleTools_Check_LinkThatLeadsOutOfTheProject_IsRefused`.

**StyleTools_Check_NoProjectNoKitOrDrawingFails_AreAnsweredWithAMessage**: No kit, a missing kit, no project, a failed
drawing with a hint, a cancelled drawing, and a renderer that throws are each answered with the message of the design.
This scenario is tested by `StyleTools_Check_NoProjectNoKitOrDrawingFails_AreAnsweredWithAMessage`.

**StyleTools_Check_BadArguments_AreAnsweredWithAMessage**: Text that is not JSON, an array, and a number for `path` are
answered with a message; a null tolerance is the same as none. This scenario is tested by
`StyleTools_Check_BadArguments_AreAnsweredWithAMessage`.

**StyleTools_Check_RealRender_PassesOnAndFailsOffThePalette**: A real SVG on the palette passes and lists its colors;
one with half in another color fails with 50 percent off the palette and names the color; a file that is not a picture
is answered with a message that it could not be drawn. This scenario is tested by
`StyleTools_Check_RealRender_PassesOnAndFailsOffThePalette`.
