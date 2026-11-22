### ProjectTools

#### Verification Approach

`ProjectTools` is verified with unit tests in three files. `ProjectToolsTests.cs` covers the file tools (`project_list`,
`project_read`, `project_write`), the wiring through the tool registry, and the log. `RenderImageToolTests.cs` covers
`render_image`. `ProjectToolsSupportTests.cs` covers the supporting types: the path guard, the picture scaler, and the
file kinds. `ProjectToolsTestDoubles.cs` holds the common ground: a base class that makes a temporary folder with a
project (a project file, the data folder, and a secret beside it), builds the tools the way the application does (the
confining path policy of `AgentToolRegistry` and the packs of the supplier), calls them, and takes a snapshot of every
file with its content hash; a render service that answers as a test says and records what it was asked; the maker of the
links; and the reader of the answers of a tool.

The tools are called through the functions of the AI library, as the model's calls arrive, so the schema, the argument
binding, and the limits of the library are part of what is tested. The real path policy, the real file system, and (for
the picture tool) the real render service are used where the subject is the behavior; the scripted render service is
used where the test needs a picture of a certain size, notes, a failure, or a PNG of a certain size.

The confinement tests are the center. Each class of path that could leave the project or reach a file of the application
is a case of one data set that is used for reading and for writing: `..` and `sub/../..`, a backslash form, the data
folder at the top and below a folder and in another letter case, the project file at the top and below a folder and
another project file, short names, a stream name, device names, an empty path, and (on Windows) a network path, a path
relative to a drive, and a device path. Full paths outside the project, and a folder beside the project that starts with
its name, are tested on their own. Links are tested with real links: a folder junction (made with the command line of
Windows, which needs no privilege), a hard link, and symbolic links to a folder and to a file (skipped, with the reason,
where the user may not make them). Each link test checks that the target is unchanged and that nothing was created
behind the link, and the junction checks were confirmed to fail when the link check is removed.

The write tests take a snapshot of every file and folder under the temporary folder (without following links) before and
after, so that "nothing else was written" is a comparison and not a guess; they also look for any entry with the word
backup in its name, for a temporary file, and for a data folder that did not exist.

#### Test Environment

Each test class makes a temporary folder and deletes it; the links of a test are removed as links first. The junction
and hard link tests run on Windows only (they use `cmd.exe` and `mklink`) and are skipped elsewhere; the symbolic link
tests run where the user may make them and are skipped with the reason otherwise; the test of a file that cannot be
replaced runs on Windows only (it opens the file without sharing). No network, display, model, or Copilot CLI is used.

#### Acceptance Criteria

- All unit tests pass with zero failures; the skipped tests are those of a platform or privilege that is not there.
- The supplier of the project tools offers `project_list`, `project_read`, `project_write`, and `render_image` after the
  drawing knowledge and the style tools, in packs with the prefixes `project` and `render`, each with words; a missing
  renderer or context is refused.
- `project_list` shows relative paths, sizes, and the image source flag in a fixed order, hides the data folder, the
  project file, dot folders, hidden files, and links, lists a given folder, refuses a folder that is outside or missing,
  and stops at 200 files with a note.
- `project_read` returns the exact text of a text image source and of a data file, without a byte order mark, says that
  an empty file is empty, and refuses with the reason a binary picture (pointing to `render_image`), another kind of
  file, a missing file, a folder, a file over 64 KiB, a file with more characters than an answer can hold, and bytes
  that are not UTF-8.
- No path of the data set that leaves the project or means something else is read, written, listed, or drawn, and
  nothing is created or changed anywhere under the temporary folder by a refused write.
- No file is read, written, listed, or drawn through a junction, a symbolic link, or a file symbolic link; a file with a
  second name outside the project is replaced as a name and the other name keeps its content.
- `project_write` creates a new file with its folders and says it was created; replaces an existing file at once and
  says it was replaced with both sizes; the project afterwards holds the same entries as before and nothing else (no
  backup, no temporary file, no data folder); a missing content, a folder in the place of the file, and a file that
  cannot be replaced are answers and leave the old file.
- Only the extensions of the text image sources and `.csv`, `.json`, `.txt` are written, in any letter case; binary
  pictures, programs, scripts, and names that only end like an allowed one are refused. A file of exactly 256 KiB is
  written, one byte more is refused, and the size is counted in bytes.
- The data folder, the project files, and every file below the `styles` folder of the project, in any letter case and at
  any depth, are never written, and the answer for a style kit says why.
- `FileWritten` is raised once per successful write with the relative path and whether the file is new, and never for a
  refused write; a listener that throws does not fail the write.
- `render_image` returns a PNG part and a text with the size, the format, and every note, draws with the project folder
  as the root, and writes no file; a picture over 1,280 pixels is scaled with its proportions (a wide one and a tall
  one), a smaller one is not enlarged, a PNG over the ceiling is made smaller in steps or refused; a real SVG is drawn
  by the real service into a real PNG of the scaled size.
- A render error is an answer with the code, the message, and the hint; a renderer that throws is an answer with the
  type of the exception only; a canceled token throws the cancellation and nothing is drawn.
- An example image of the active style kit is drawn by its full path with the kit folder as the root, only while that
  kit is active, only an image source inside the kit folder, not through a link or `..`, and not by a relative path; a
  kit inside the project is drawn as a file of the project.
- The log has the tool, the relative path, the size, and the outcome of each call and neither the content of a file nor
  the path of a refused request; a log that cannot be written does not fail a call.
- The guard names a link below the project (also a junction whose target is gone), judges containment by whole folder
  names, and refuses the disguised names; the scaler keeps a fitting picture, scales by the longer side with at least
  one pixel, and averages pixels weighted by opacity; the file kinds tell text sources and data files from binary
  pictures.

#### Test Scenarios

**ProjectTools_ArgumentsAreChecked_NullRendererAndContext_AreRefused**: A missing renderer and a missing context are
refused. This scenario is tested by `ProjectTools_ArgumentsAreChecked_NullRendererAndContext_AreRefused`.

**ProjectTools_CreatePacks_AsTheRegistrySupplier_OffersTheFourToolsAfterTheBuiltInTools**: A registry over the real
drawing knowledge and a stand-in style tool, supplied by `CreatePacks`, offers the four tools after the others, in two
packs with the prefixes `project` and `render`. This scenario is tested by
`ProjectTools_CreatePacks_AsTheRegistrySupplier_OffersTheFourToolsAfterTheBuiltInTools`.

**ProjectTools_List_Project_ShowsRelativePathsSizesAndImageSourceFlagAndHidesApplicationFiles**: A project with
pictures, data, a nested file, the data folder, the project file, a `.git` folder, and a hidden file is listed with
relative paths, sizes, and the flag, the top of the project first, and none of the hidden things. This scenario is
tested by `ProjectTools_List_Project_ShowsRelativePathsSizesAndImageSourceFlagAndHidesApplicationFiles`.

**ProjectTools_List_Folder_ShowsOnlyThatFolderAndRefusesAMissingOne**: A folder, a missing folder, and the folder `.`
are listed as expected. This scenario is tested by `ProjectTools_List_Folder_ShowsOnlyThatFolderAndRefusesAMissingOne`.

**ProjectTools_List_MoreFilesThanTheCeiling_StopsAndSaysItIsNotComplete**: With 210 files the first 200 are listed and
the answer says it is not complete. This scenario is tested by
`ProjectTools_List_MoreFilesThanTheCeiling_StopsAndSaysItIsNotComplete`.

**ProjectTools_List_EscapingFolderAndLinks_AreRefusedAndNotFollowed**: With a junction to a folder with a secret, the
link is not listed, nothing is shown through it, and the folders `..` and `.canvaspilot` are refused. This scenario is
tested by `ProjectTools_List_EscapingFolderAndLinks_AreRefusedAndNotFollowed`.

**ProjectTools_Read_TextFiles_ReturnTheirText**: A picture source with non-ASCII text, a data file, a full path inside
the project, an empty file, and a file with a byte order mark are read as expected. This scenario is tested by
`ProjectTools_Read_TextFiles_ReturnTheirText`.

**ProjectTools_Read_FilesThatAreNotSmallTextSources_AreRefusedWithTheReason**: A PNG, a program, a missing file, a
folder, a file over 64 KiB, bytes that are not UTF-8, and a file with more characters than an answer can hold are each
refused with the reason, and a file with exactly as many characters as an answer can hold is read. This scenario is
tested by `ProjectTools_Read_FilesThatAreNotSmallTextSources_AreRefusedWithTheReason`.

**ProjectTools_Read_EscapingPaths_AreRefusedForEveryClass**: Every path of the data set is read with no content of the
secret, the project file, or the data folder in the answer. This scenario is tested by
`ProjectTools_Read_EscapingPaths_AreRefusedForEveryClass`.

**ProjectTools_Read_AbsolutePathOutsideTheProject_IsRefused**: The full path of the secret beside the project is
refused. This scenario is tested by `ProjectTools_Read_AbsolutePathOutsideTheProject_IsRefused`.

**ProjectTools_Read_ThroughJunction_IsRefused**: A file behind a junction is not read, by a relative or a full path.
This scenario is tested by `ProjectTools_Read_ThroughJunction_IsRefused`.

**ProjectTools_Read_ThroughSymbolicLinks_IsRefused**: A file behind a symbolic link to a folder and a symbolic link to a
file are not read. This test is extra evidence that is not traced to a requirement, because it is skipped, with the
reason, where the user may not make a symbolic link. This scenario is tested by
`ProjectTools_Read_ThroughSymbolicLinks_IsRefused`.

**ProjectTools_Write_NewFile_IsCreatedWithItsFoldersAndTheShellIsTold**: A new file in two new folders is created with
the exact text and no byte order mark, the answer says created, and `FileWritten` carries the relative path and the new
flag. This scenario is tested by `ProjectTools_Write_NewFile_IsCreatedWithItsFoldersAndTheShellIsTold`.

**ProjectTools_Write_ExistingFile_IsReplacedDirectlyAndLeavesNothingElseInTheProject**: An existing file is replaced,
the answer says replaced with both sizes, the set of entries of the project is the same as before, there is no backup
folder or file anywhere, no temporary file, and `FileWritten` says the file is not new. This scenario is tested by
`ProjectTools_Write_ExistingFile_IsReplacedDirectlyAndLeavesNothingElseInTheProject`.

**ProjectTools_Write_Extension_IsAllowedOnlyForTextImageSourcesAndSmallDataFiles**: Every text image source extension
and the three data extensions are written, and binary pictures, programs, scripts, and names that only end like an
allowed one are refused with the list of what is allowed. This scenario is tested by
`ProjectTools_Write_Extension_IsAllowedOnlyForTextImageSourcesAndSmallDataFiles`.

**ProjectTools_Write_SizeCeiling_Is256KiBInBytesAndARefusedWriteLeavesTheOldFile**: 256 KiB is written, one byte more
and 130,000 two-byte characters are refused, and the old file is as it was. This scenario is tested by
`ProjectTools_Write_SizeCeiling_Is256KiBInBytesAndARefusedWriteLeavesTheOldFile`.

**ProjectTools_Write_ApplicationFilesAndStyleKits_AreNeverWritten**: The data folder, a project file, and files below
`styles` in several letter cases are refused, the project is unchanged, and the shell is not told. This scenario is
tested by `ProjectTools_Write_ApplicationFilesAndStyleKits_AreNeverWritten`.

**ProjectTools_Write_ExistingStyleKitFile_IsNotReplacedAndTheAnswerSaysWhy**: A style kit file that exists keeps its
content and the answer says style kits cannot be written. This scenario is tested by
`ProjectTools_Write_ExistingStyleKitFile_IsNotReplacedAndTheAnswerSaysWhy`.

**ProjectTools_Write_EscapingPaths_AreRefusedForEveryClassAndNothingIsWritten**: Every path of the data set is refused
and the snapshot of everything under the temporary folder, the secret included, is unchanged. This scenario is tested by
`ProjectTools_Write_EscapingPaths_AreRefusedForEveryClassAndNothingIsWritten`.

**ProjectTools_Write_AbsolutePathsOutsideTheProject_AreRefused**: The full path of the secret, of a new file beside the
project, and of a file in a folder that starts like the project are refused. This scenario is tested by
`ProjectTools_Write_AbsolutePathsOutsideTheProject_AreRefused`.

**ProjectTools_Write_ThroughJunction_IsRefusedAndNothingIsWrittenOutside**: Through a junction, a new file, an existing
file, and a file below a folder that does not exist are refused and the target folder is unchanged. This scenario is
tested by `ProjectTools_Write_ThroughJunction_IsRefusedAndNothingIsWrittenOutside`.

**ProjectTools_Write_ThroughSymbolicLinks_IsRefusedAndNothingIsWrittenOutside**: Through a symbolic link to a folder and
to a file, nothing is written. This test is extra evidence that is not traced to a requirement: it is skipped, with the
reason, where the user may not make a symbolic link (on Windows that needs a privilege or developer mode), and a skipped
test would keep a requirement from being satisfied. This scenario is tested by
`ProjectTools_Write_ThroughSymbolicLinks_IsRefusedAndNothingIsWrittenOutside`.

**ProjectTools_Write_HardLinkToAFileOutside_IsNotWrittenThrough**: A file inside the project that is a second name of
the secret is replaced as a name and the secret keeps its content. This scenario is tested by
`ProjectTools_Write_HardLinkToAFileOutside_IsNotWrittenThrough`.

**ProjectTools_Write_BadRequestsAndFailures_AreAnswersAndLeaveTheOldFileAndNoTemporaryFile**: A missing content, a
folder in the place of the file, and a file that is open without sharing give answers (the last names only the exception
type and no path), the old file keeps its content, and no temporary file is left. This scenario is tested by
`ProjectTools_Write_BadRequestsAndFailures_AreAnswersAndLeaveTheOldFileAndNoTemporaryFile`.

**ProjectTools_Write_ListenerThatThrows_DoesNotFailTheTool**: A listener of `FileWritten` that throws does not stop the
write or its answer. This scenario is tested by `ProjectTools_Write_ListenerThatThrows_DoesNotFailTheTool`.

**ProjectTools_Log_CallsOfTheTools_RecordOnlyMetadata**: The log has the tool, the relative path, the size, and the
outcome of a write and a read and a refusal, and neither the content nor the path of the refused request. This scenario
is tested by `ProjectTools_Log_CallsOfTheTools_RecordOnlyMetadata`.

**ProjectTools_Log_LoggerThatCannotWrite_DoesNotFailTheTool**: With a log file that cannot be appended to, the tool
still answers. This scenario is tested by `ProjectTools_Log_LoggerThatCannotWrite_DoesNotFailTheTool`.

**ProjectTools_Calls_MissingOrEmptyPath_AreAnswers**: A missing path is an answer for reading and writing. This scenario
is tested by `ProjectTools_Calls_MissingOrEmptyPath_AreAnswers`.

**RenderImageTool_Render_ProjectFile_ReturnsThePngAndItsDescriptionAndWritesNoFile**: A picture is returned as a PNG
part and a text with the size, the format, and both notes of the renderer; the renderer is asked with the full path and
the project folder; and the snapshot of the temporary folder is unchanged. This scenario is tested by
`RenderImageTool_Render_ProjectFile_ReturnsThePngAndItsDescriptionAndWritesNoFile`.

**RenderImageTool_Render_LargePicture_IsScaledToAtMost1280PixelsAndSmallOnesAreNotEnlarged**: A wide, a tall, and a
small picture are encoded at 1280 x 640, 288 x 1280, and 100 x 50, and the text says when a picture was scaled. This
scenario is tested by `RenderImageTool_Render_LargePicture_IsScaledToAtMost1280PixelsAndSmallOnesAreNotEnlarged`.

**RenderImageTool_Render_PngOverTheCeiling_IsMadeSmallerInStepsOrRefused**: A PNG that is too large at 1280 and 960 and
fits at 640 is made smaller in steps, and one that never fits is refused. This scenario is tested by
`RenderImageTool_Render_PngOverTheCeiling_IsMadeSmallerInStepsOrRefused`.

**RenderImageTool_Render_RealSvgWithTheRealRenderService_ReturnsARealScaledPng**: A real SVG of 3000 x 1500 pixels is
drawn by the real render service, the PNG has the signature and the size 1280 x 640 in its header, and no file is
written. This scenario is tested by `RenderImageTool_Render_RealSvgWithTheRealRenderService_ReturnsARealScaledPng`.

**RenderImageTool_Render_FailureOfTheRender_IsAnAnswerWithCodeMessageAndHint**: A render error is an answer with the
code, the message, and the hint, and a renderer that throws is an answer with the exception type only. This scenario is
tested by `RenderImageTool_Render_FailureOfTheRender_IsAnAnswerWithCodeMessageAndHint`.

**RenderImageTool_Render_CanceledTurn_ThrowsCancellationAndDoesNotDraw**: A canceled token throws the cancellation and
the renderer is not asked; a render that reports cancellation on its own is an answer. This scenario is tested by
`RenderImageTool_Render_CanceledTurn_ThrowsCancellationAndDoesNotDraw`.

**RenderImageTool_Render_RefusedFilesAndEscapingPaths_AreNeverDrawn**: A text file, a missing file, paths outside the
project, the project file, the data folder, and files behind a junction are not drawn and the renderer is never asked.
This scenario is tested by `RenderImageTool_Render_RefusedFilesAndEscapingPaths_AreNeverDrawn`.

**RenderImageTool_Render_StyleKitExample_IsAllowedFromTheActiveKitFolderOnly**: An example of the active kit is drawn
with the kit folder as the root; with no active kit, with a file of the kit that is not an image, a picture beside the
kit, a path that climbs out of the kit, a file behind a link in the kit, a relative path, or after the kit is no longer
active, nothing is drawn. This scenario is tested by
`RenderImageTool_Render_StyleKitExample_IsAllowedFromTheActiveKitFolderOnly`.

**RenderImageTool_Render_StyleKitInsideTheProject_IsDrawnAsAProjectFile**: A kit inside the project is drawn with the
project folder as the root, by its full path and by its relative path. This scenario is tested by
`RenderImageTool_Render_StyleKitInsideTheProject_IsDrawnAsAProjectFile`.

**RenderImageTool_Render_MissingPath_IsAnAnswer**: A missing path is an answer and the renderer is not asked. This
scenario is tested by `RenderImageTool_Render_MissingPath_IsAnAnswer`.

**ProjectPathGuard_IsLink_JunctionIsALinkAndOrdinaryEntriesAreNot**: A junction is a link and a file, a folder, and a
name that does not exist are not. This scenario is tested by
`ProjectPathGuard_IsLink_JunctionIsALinkAndOrdinaryEntriesAreNot`.

**ProjectPathGuard_FindLink_DanglingJunctionAndNestedPath_NamesTheFirstLink**: A junction whose target is gone is found,
with its path relative to the folder. This scenario is tested by
`ProjectPathGuard_FindLink_DanglingJunctionAndNestedPath_NamesTheFirstLink`.

**ProjectPathGuard_IsInside_FolderBesideWithTheSamePrefix_IsOutside**: Containment is by whole folder names. This
scenario is tested by `ProjectPathGuard_IsInside_FolderBesideWithTheSamePrefix_IsOutside`.

**ProjectPathGuard_TryResolveUnder_Names_AreCheckedForSpellingsThatMeanSomethingElse**: Ordinary names are accepted, and
a stream name, the data folder, a project file, a short name, a trailing dot or space (where the system does not trim
it), and device names are refused with a reason. This scenario is tested by
`ProjectPathGuard_TryResolveUnder_Names_AreCheckedForSpellingsThatMeanSomethingElse`.

**ProjectPathGuard_TryResolveUnder_PathsOutsideTheFolder_AreRefused**: A full path outside and one that climbs out are
refused, and one that goes down and up again inside is resolved. This scenario is tested by
`ProjectPathGuard_TryResolveUnder_PathsOutsideTheFolder_AreRefused`.

**ImageScaler_FitWithin_SizesAreScaledByTheLongerSideAndSmallOnesAreKept**: A fitting picture is returned as it is, a
larger one is scaled by its longer side with the notes kept, and a very thin one keeps one pixel. This scenario is
tested by `ImageScaler_FitWithin_SizesAreScaledByTheLongerSideAndSmallOnesAreKept`.

**ImageScaler_FitWithin_Pixels_AreAveragedWeightedByOpacity**: Black and white average to grey, a red pixel beside a
clear pixel stays red at half opacity, and a clear block stays clear. This scenario is tested by
`ImageScaler_FitWithin_Pixels_AreAveragedWeightedByOpacity`.

**ImageScaler_FitWithin_BadArguments_AreRefused**: A missing picture and a size below one are refused. This scenario is
tested by `ImageScaler_FitWithin_BadArguments_AreRefused`.

**ProjectFileKinds_Classify_TextSourcesDataFilesAndBinaryPictures**: Text sources and data files are text files, binary
pictures are image sources and not text, and other names are neither. This scenario is tested by
`ProjectFileKinds_Classify_TextSourcesDataFilesAndBinaryPictures`.

**AgentSubsystem_ProjectTools_ProjectFolder_WriteListReadAndDrawStayInsideTheProject**: The subsystem scenario that is
described in the verification of the Agent subsystem. This scenario is tested by
`AgentSubsystem_ProjectTools_ProjectFolder_WriteListReadAndDrawStayInsideTheProject`.
