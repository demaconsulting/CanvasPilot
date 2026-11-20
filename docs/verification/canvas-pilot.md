# CanvasPilot

## Verification Approach

System-level verification uses tests in `CanvasPilotTests.cs` that compose real units on temporary
directories without opening a window. Each test exercises the units together as the application does
and asserts on observable outcomes such as the files written to disk and the exceptions raised. The
tests use real file I/O and no mocks for the units themselves; where a user would act in a dialog, a hand-written
dialog service plays the user, and a hand-written refresh scheduler replaces timed refreshes so no test waits.

The `AppShell` subsystem depends on the `Preview` subsystem and not the reverse (see *Dependency Direction* in the
design). Verification follows that direction: the `Preview` units are verified with a substitute for the
`IExportDialogs` interface that `Preview` owns, the shell is verified with a substitute image exporter and with the
disabled default exporter, and only the system tests and the user interface tests compose the real shell with the real
preview, which is also what proves that the two work together. The stored AI provider choices are verified through the
settings service and the settings dialog; the agent that will use them does not exist yet and has no verification.

Dictation is verified at this level over the speech library's own recognition pipeline with a recognition backend
written in managed code and a recorded sound file for the microphone, so no native speech runtime, model, or microphone
is needed. Dictation with a real microphone and a real model, the native speech files in an installed application, and
the reaction of the application to a blocked microphone or missing native files need a person and an installed package;
they are checked by hand before each release as listed under *Manual Verification* in the verification of the Speech
subsystem and of the Chat subsystem (the talk button of the chat panel with a real microphone and model, and the Windows
microphone privacy setting), together with the checks of the installed package, and are not evidence for a requirement.

## Test Environment

System tests run under the standard .NET test runner on Windows, Linux, and macOS with the .NET 10
runtime. Temporary directories are created and removed within each test. No network access, external
services, or display is required.

## Acceptance Criteria

- All system tests pass with zero failures on every supported platform.
- Application events and failures logged by the application appear in a local log file within the log
  folder.
- A file location built from a name that attempts to leave its intended folder is rejected, while a
  name inside the folder is accepted.
- Settings saved by one run are restored by the next run, and a damaged settings file yields default
  settings, a kept-aside copy of the file, and a log entry rather than a failure.
- AI provider choices made in the settings dialog are stored and restored, and unrestricted agent tools
  remain off unless the user opts in.
- Speech devices, models, and a model setting chosen in the Speech page of the settings dialog are stored by device
  name and model identifier and restored by the next run, using substitute audio and model services.
- A project created in a folder is reopened from the recent projects list, and a damaged project file is
  refused with a message and a log entry without changing the open project.
- The project explorer lists the image source files of the open project and not other content.
- A selected image source file is rendered and shown with its size, a file that cannot be drawn shows an error with
  its code, and the shown picture can be exported as a valid PNG file.
- The shown picture of a diagram is identified by its file and by fingerprints of the file and the pixels, which
  change when the file is edited, and it locates the diagram's elements in picture pixels.
- The preview pane has a layer over the shown picture that holds the content placed in it, beside every picture.
- A message that is sent with marks on the shown diagram and an attached file gives a payload with the text and its
  references in order, the attachment with its path in the project, the picture with the marks drawn and numbered, an
  area image and the geometry for each mark, and the source elements under each mark, and the draft is empty afterwards.
- Marks drawn on the shown picture are numbered in order, keep their geometry in picture pixels and an optional note,
  stay with the exact picture they were drawn on and are not shown on a picture of the edited file, and report the parts
  of the diagram's source that lie under them.
- A user who draws a rectangle on the shown picture gets a mark whose chip is in the message at the caret; undo of the
  chip hides the mark and redo shows it; dragging the picture out is off only while a marking tool is chosen; and when
  the file is edited the mark is not drawn on the new picture but stays in the message.
- The shown picture can be prepared for dragging out as a PNG file in the application's drag folder; the files of
  earlier runs are deleted at start, and the application's own are deleted when it ends. Whether other programs accept
  the dropped picture is checked by hand before a release (see the verification of `ImageDragSource`).
- A message is composed from typed text, dictated text, and chips for attached files, folders, images, and marks, which
  stay in the order they were placed with the origin and kind of each; each user action is one undo step and redo gives
  the same steps; sending clears the draft and what can be undone, and nothing of the draft is written to disk.
- Files, folders, pictures, and text that are dropped or pasted become chips and text without anything being opened or
  copied; sending copies them into the project's attachments folder byte for byte, never over another file, within the
  limits of one message; the explorer does not list them.
- With the speech settings composed as at startup, dictation tells a user who has no model chosen to choose one, and a
  user who holds the talk control with a model chosen gets the recognized words as text that is added to the message as
  one dictated block, in order after the typed text.
- A user who marks a node of the shown diagram, writes around the mark, and sends the message through the chat panel
  sees the message in the transcript with its words and mark chip in order, and "View payload" shows the payload
  exactly as the payload describer writes it, with the place of the mark, the node under it, and the annotated picture
  and the area around the mark.
- The published win-x64 application contains the native speech and audio runtime libraries and so does the installer
  package, so that the installed application needs nothing but the model download to run a chosen speech model.
- The application starts on every supported platform: composing its units without a window gives a titled shell
  with the right commands available, and the log records the start and stop.
- The Windows build produces exactly one MSI package of non-trivial size for the application, named for the build
  version; installing it is checked by hand before each release.
- The main window shell creates a project, lists its image files, finds it in the recent projects after a
  restart, and keeps a changed setting across the restart.

## Test Scenarios

**CanvasPilot_OperationalLogging_ApplicationEvent_WritesLocalLog**: A log folder is resolved beneath a
per-user data root, an informational event and a failure with exception details are logged, and the log
folder is inspected; exactly one log file exists and it contains both entries including the exception
detail. This scenario is tested by `CanvasPilot_OperationalLogging_ApplicationEvent_WritesLocalLog`.

**CanvasPilot_SafeFileHandling_TraversalName_IsRejected**: A supplied name containing parent-directory
traversal is combined with a base folder and an `ArgumentException` is thrown; a name inside the folder
is combined successfully and the result stays under the base folder. This scenario is tested by
`CanvasPilot_SafeFileHandling_TraversalName_IsRejected`.

**CanvasPilot_PersistUserSettings_SaveAndReload_RestoresSettingsAndToleratesDamage**: Settings with a recent
project are saved by one service, loaded by a second, and restored; the settings file is then replaced by
invalid text and loaded by a third service, which yields default settings, leaves a `settings.json.bad` copy,
and logs the corruption. This scenario is tested by
`CanvasPilot_PersistUserSettings_SaveAndReload_RestoresSettingsAndToleratesDamage`.

**CanvasPilot_ConfigureAiProvider_DialogOk_StoresChoicesAndKeepsToolsOptIn**: The settings dialog is composed
over the AI provider page; Ollama, a model name, a context window, and an endpoint are chosen and OK is
selected; a new service loads those choices and unrestricted agent tools are still off. This scenario is
tested by `CanvasPilot_ConfigureAiProvider_DialogOk_StoresChoicesAndKeepsToolsOptIn`.

**CanvasPilot_ConfigureSpeech_DialogOk_StoresChoicesAndRestoresThemNextRun**: The application is composed with
substitute audio and model services that list two microphones, two speakers, a speech-to-text model, and a
text-to-speech model with a voice choice. The user opens Settings, chooses the second microphone and speaker, both
models, and the first voice on the Speech page, and chooses OK. After a restart the stored settings hold the device
names, the model identifiers, and the voice, and a Speech page loaded from them shows the microphone and the voice.
No hardware is used and nothing is downloaded. This scenario is tested by
`CanvasPilot_ConfigureSpeech_DialogOk_StoresChoicesAndRestoresThemNextRun`.

**CanvasPilot_ManageProjects_NewProject_CreatesReopensAndRemembersProject**: A project is created in a
documents folder, added to the recent projects list, and closed; a new settings service loads the list and the
first entry is opened. The project reopens with its name. A damaged project file is then opened and refused with
a failed result, the open project is unchanged, and the refusal is logged. This scenario is tested by
`CanvasPilot_ManageProjects_NewProject_CreatesReopensAndRemembersProject`.

**CanvasPilot_BrowseProjectImages_OpenProject_ListsImageSourceFiles**: A created project holds two SVG files and
a text file; the project explorer is opened on the project folder and lists the two SVG files in name order,
and selecting one reports its path. This scenario is tested by
`CanvasPilot_BrowseProjectImages_OpenProject_ListsImageSourceFiles`.

**CanvasPilot_ManageProjectFiles_RenameAndDelete_UpdateFilesAndReportProblems**: The composed application's user
creates a project that holds two SVG files and selects the first. Renaming it to a name that is not an image source
file is refused with a message; renaming it to `cover.svg` renames the file. Deleting `cover.svg` is first declined
at the confirmation, which keeps the file, and then confirmed, which removes it. The explorer then lists only the
other file. This scenario is tested by `CanvasPilot_ManageProjectFiles_RenameAndDelete_UpdateFilesAndReportProblems`.

**CanvasPilot_Startup_ComposeApplication_ProducesWorkingShell**: The application's units are composed exactly as
at startup, without a window, over temporary per-user folders; the shell is titled with the application name,
the commands that need nothing are available, those that need a project are not, the default settings are in
effect, and the log holds the start and stop entries. The platform requirements link this test to each platform
and runtime with source filters. This scenario is tested by
`CanvasPilot_Startup_ComposeApplication_ProducesWorkingShell`.

**CanvasPilot_PreviewImages_SelectedFile_ShowsRenderedPictureOrError**: The composed application's user creates a
project that holds a good and a broken SVG and selects the good file: the preview shows a 32 by 16 picture and the
image export is available. Selecting the broken file shows an error with the code `cannot-draw`, no picture, and no
export. This scenario is tested by `CanvasPilot_PreviewImages_SelectedFile_ShowsRenderedPictureOrError`.

**CanvasPilot_ExportImages_RenderedSvg_WritesValidPngFile**: With the good SVG shown, the Export as PNG command is
enabled and, with a chosen file, writes a file that starts with the PNG signature and whose header holds width 32 and
height 16. This scenario is tested by `CanvasPilot_ExportImages_RenderedSvg_WritesValidPngFile`.

**CanvasPilot_BuildMessagePayload_SelectedDiagram_PinsPictureToFileAndReportsElements**: The composed application's
user selects a two-node Mermaid diagram in a project. The shown picture names the diagram's file, has a file
fingerprint and a pixel fingerprint of 64 hexadecimal characters each, has a box for each of its two nodes, and the
nodes' text is `Start` and `End`. After another file is shown, the diagram is edited so that the second node is
`Finish`, and it is selected again: the file fingerprint, the pixel fingerprint, and the snapshot key all differ and
the node text is `Start` and `Finish`. This scenario is tested by
`CanvasPilot_BuildMessagePayload_SelectedDiagram_PinsPictureToFileAndReportsElements`.

**CanvasPilot_BuildMessagePayload_MermaidWithMarks_ContainsImagesCropsGeometryAndSourceElements**: The composed
application's user shows a two-node Mermaid diagram, marks the node `Start` with a rectangle with a note and the node
`End` with an ellipse, and composes a message from typed text, both marks, an attached file from outside the project,
and dictated text. The message is sent: the snapshot is taken, the file is copied into the project, and the payload is
built. The text is `Swap [Mark 1] and [Mark 2] see [Attachment 1: brief.txt] please`; the attachment's project path is
`attachments/brief.txt` and the file is there; there is one image with the file and fingerprints of the shown picture
and an annotated picture of its size; the two marks have their kinds, the rectangle has its points and note, each has a
cut-out of the size of its area, and the source elements of the marks are the nodes `Start` and `End`; the draft and
what can be undone are empty. This scenario is tested by
`CanvasPilot_BuildMessagePayload_MermaidWithMarks_ContainsImagesCropsGeometryAndSourceElements`.

**CanvasPilot_MarkImage_ShownPicture_HasOverlayLayerThatSurvivesNewPictures**: The composed application's user creates a
project that holds a 32 by 16 and an 8 by 8 SVG. Content is placed in the overlay layer of the pane that the workspace
shows, which is the application's own pane. After the first SVG is selected the layer holds the content beside the 32
pixel wide picture, and after the second SVG is selected it still holds the same content beside the 8 pixel wide
picture. This scenario is tested by `CanvasPilot_MarkImage_ShownPicture_HasOverlayLayerThatSurvivesNewPictures`.

**CanvasPilot_MarkImage_MarksOnShownPicture_AreNumberedAndStayOnTheirPicture**: The composed application's user shows a
two-node Mermaid diagram and draws a rectangle with a note around the node `Start`, and an arrow. The marks are numbered
one and two, hold the shown picture, and the parts of the source under the rectangle include the node `Start`. After the
diagram is edited and shown again, a third mark is drawn on the new picture and is number three. Each picture shows only
its own marks while all three belong to the message. This scenario is tested by
`CanvasPilot_MarkImage_MarksOnShownPicture_AreNumberedAndStayOnTheirPicture`.

**CanvasPilot_MarkImage_DrawRectangleOnShownPicture_AddsMarkChipAndHidesMarkOnNewPicture**: The composed application's
user has a project with an 80 by 40 SVG shown and some typed text in the message, chooses the rectangle tool, and draws
with the overlay's pointer methods and skips the note. Dragging the picture out is off while the tool is chosen and on
again after the tool is released. The message holds one mark chip with number one after the typed text, and the picture
shows the mark. Undo removes the chip and the mark is not shown; redo shows both again. After the file is edited and
shown again the mark is not shown, the overlay counts one hidden mark, and the message still holds the chip. The overlay
is the overlay of the preview pane and its tool bar is the pane's tool bar. This scenario is tested by
`CanvasPilot_MarkImage_DrawRectangleOnShownPicture_AddsMarkChipAndHidesMarkOnNewPicture`.

**CanvasPilot_DragImageOut_ShownPicture_ProvidesPngFile**: The composed application is started with a file of an
earlier run in its drag folder, which is removed at start. Without a picture no file is offered. With a 32 by 16 SVG
shown the offered file is in the drag folder, is named `logo-` and the start of the pixel fingerprint, and is a PNG
of width 32 and height 16; with dragging out turned off none is offered; after the composition is disposed the file is
gone. This scenario is tested by `CanvasPilot_DragImageOut_ShownPicture_ProvidesPngFile`.

**CanvasPilot_ComposeMessage_TextDictationAndChips_AreOneOrderedMessage**: A message is composed by typing, dictating a
block, dropping a file and a folder in one operation, adding a mark where the caret was, and typing more. The message
holds the typed text up to the mark, the mark, the dictated text with its dictated origin, the file, the folder, and the
last typed text, in that order, and its length is the number of characters plus one for each chip. This scenario is
tested by `CanvasPilot_ComposeMessage_TextDictationAndChips_AreOneOrderedMessage`.

**CanvasPilot_UndoRedoMessage_StepsOfEachAction_UndoAndRedoOneAtATimeAndSendClears**: A draft is built from a typed
character, a dictated block, a drop of two chips, and a deleted chip. Undoing the deleted chip brings back one chip,
undoing the drop removes both chips together, undoing the dictation removes the whole block, and redoing it restores the
whole block. After sending, nothing can be undone or redone, the draft is empty, and no file or folder was written in
the temporary folder of the test. This scenario is tested by
`CanvasPilot_UndoRedoMessage_StepsOfEachAction_UndoAndRedoOneAtATimeAndSendClears`.

**CanvasPilot_AttachToMessage_DroppedAndPastedContent_IsCopiedIntoProjectAtSend**: In a project created by the project
service that holds one image, a text file, a program, and a folder with a file are dropped from outside the project and
a screenshot is pasted, in one operation. Nothing is copied into the project while drafting; undo removes the four chips
and redo brings them back; after sending, the text file, the program, the folder with its file, and the screenshot are
in the attachments folder with their exact bytes, the original text file is unchanged, the explorer lists only the
project's image, and the folder for pasted pictures is empty. This scenario is tested by
`CanvasPilot_AttachToMessage_DroppedAndPastedContent_IsCopiedIntoProjectAtSend`.

**CanvasPilot_DictateMessage_HoldToTalk_AddsOneDictatedBlockAndExplainsWhenUnavailable**: The application is composed as
at startup over a model service whose model is installed in a temporary store, with a recognition backend written in
managed code, and an audio service whose microphone is a recorded sound file. Before a model is chosen, dictation
reports that none is chosen and names the Speech page. After the model is chosen the way the settings page stores it,
dictation is available; the user holds the talk control and lets go, and the final text is "Make the second box blue."
Added to a message after typed text with the dictation edit, the message holds the typed text and one block of dictated
text, in that order. This scenario is tested by
`CanvasPilot_DictateMessage_HoldToTalk_AddsOneDictatedBlockAndExplainsWhenUnavailable`.

**CanvasPilot_InspectPayload_SentMessageWithMark_InspectorShowsExactlyThePayload**: The composed application's user has
a project with a two-node Mermaid diagram shown and a payload inspector launcher that records what it is asked to show.
The user types "Make ", draws a rectangle around the node `Start` and skips the note, types " green", and sends the
message through the chat panel, which could be sent. The transcript holds one entry whose items are the text "Make ", a
chip, and the text " green"; its payload text is `Make [Mark 1] green`. Choosing "View payload" gives the launcher an
inspector whose description is exactly the output of the payload describer for that payload, contains the node text
`Start` as a source element, and has two pictures, the annotated picture and the area around the mark. This scenario is
tested by `CanvasPilot_InspectPayload_SentMessageWithMark_InspectorShowsExactlyThePayload`.

**Speech_NativeRuntimePublished**: After the Windows build job publishes the application self-contained for win-x64,
FileAssert asserts that the published folder, which the installer takes whole, holds exactly one each of
`sherpa-onnx-c-api.dll`, `onnxruntime.dll`, and `portaudio.dll`, each of a plausible size, and a following step reads
the installer package's file table and fails when any of the three is missing. This scenario is tested by
`Speech_NativeRuntimePublished`, which is run in CI only on Windows and is described in the verification of the Speech
subsystem and of the Speech OTS item.

**CanvasPilot_DesktopShell_CreateProject_ExplorerRecentProjectsAndSettingsRoundTrip**: The composed application's user
creates a project through the New Project dialog; two files are added, one an image; the explorer lists only the
image and the title names the project. The user changes the AI model name in the settings dialog and the
application is restarted: the project is in the recent projects menu and the model name is in effect. This
scenario is tested by `CanvasPilot_DesktopShell_CreateProject_ExplorerRecentProjectsAndSettingsRoundTrip`.

**WiX_MsiPackage**: After the Windows build job publishes the application self-contained for win-x64, builds the
installer project with the numeric build version, and copies the package, FileAssert asserts that exactly one
`package/CanvasPilot-*-win-x64.msi` exists with a non-trivial size. This scenario is tested by `WiX_MsiPackage`, which
is run in CI only and is described in the WiX Toolset OTS verification document.
