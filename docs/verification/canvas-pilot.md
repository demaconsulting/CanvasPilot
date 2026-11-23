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
preview, which is also what proves that the two work together. The AI copilot is verified in the `Agent` subsystem
with a scripted model, a stand-in Ollama server, and fake starters and a stand-in start function for the Copilot
runtime, and the system tests compose the copilot, the history, and the settings check as the application does; live
Ollama and live GitHub Copilot are not part of the automated evidence, except for opt-in tests of GitHub Copilot
that are skipped unless `CANVASPILOT_LIVE_COPILOT` is `1` (see *Manual Verification* below).

Dictation is verified at this level over the speech library's own recognition pipeline with a recognition backend
written in managed code and a recorded sound file for the microphone, so no native speech runtime, model, or microphone
is needed. Dictation with a real microphone and a real model, the native speech files in an installed application, and
the reaction of the application to a blocked microphone or missing native files need a person and an installed package;
they are checked by hand before each release as listed under *Manual Verification* below and in the verification of the
Speech subsystem and of the Chat subsystem (the talk button of the chat panel with a real microphone and model, and the
Windows microphone privacy setting), together with the checks of the installed package, and are not evidence for a
requirement.

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
- AI provider choices made in the settings dialog are stored and restored; the unrestricted agent tools are not offered,
  the context window of a new installation is 32768 and a stored one is kept, and Check connection on the page shows one
  sentence from the readiness check of the copilot, for Ollama (over a stand-in server) and for GitHub Copilot (over a
  fake machine), without saving anything.
- The model of the AI provider is chosen from the list that the provider offers (shown with what each model can do and a
  warning for what it lacks), with a reasoning level that the model offers; the choice is stored and reaches the agent
  as the model selection; model settings that cannot work (a model without tool calling, a level that the model does not
  offer) are told in the banner with the sentence of the AI library before the first turn, no agent is made for them,
  and a model that cannot see pictures is a warning that does not stop a turn.
- A message sent in the composed application gets a streamed answer from the configured provider (a scripted Ollama
  model) with the tool row, the drawing that the copilot wrote exists in the project, the explorer selects it and the
  preview draws it, and the copilot was given only the tools of the drawing knowledge, the style, and the project.
- The words of the conversation are kept below the history folder that the composition was given and never in the
  project, are shown again as text when the application starts in the same project, and are deleted by Clear chat
  history.
- With GitHub Copilot configured on a machine where the runtime is not signed in, the banner of the chat panel and the
  conversation say how to sign in (run `copilot` and type `/login`, or use `gh auth login` or a token variable), and no
  answer is made; with the installed CLI and a sign-in a message gets a streamed answer from
  the runtime that the starter runs on the installed CLI, with the tool row, and the drawing is written in the project.
- Speech devices, models, and a model setting chosen in the Speech page of the settings dialog are stored by device
  name and model identifier and restored by the next run, using substitute audio and model services.
- A project created in a folder is reopened from the recent projects list, and a damaged project file is
  refused with a message and a log entry without changing the open project.
- The project explorer shows the folders and the image source files of the open project as a tree, and not other
  content.
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
- Holding the talk key opens neither the microphone nor the speech engine when no model is chosen or the chosen model is
  not installed; the status says what to do, the message is unchanged and can still be sent, and with a model the words
  recognized while the key was held are added as one dictated block that one undo removes.
- Marks keep their numbers, points, and picture when the diagram is drawn again; a changed picture does not show them
  and the sent message holds the picture they were drawn on; the Edit menu's undo and redo go through every action of
  the message one step at a time, and what is dropped is never opened, run, or shown, whatever it is.
- A user who marks a node of the shown diagram, writes around the mark, and sends the message through the chat panel
  sees the message in the transcript with its words and mark chip in order, and "View payload" shows the payload
  exactly as the payload describer writes it, with the place of the mark, the node under it, and the annotated picture
  and the area around the mark.
- The drawing knowledge that the application holds is looked up as an assistant would look it up, from the program
  itself: the compact index is within its budget and names the cards of CanvasNet and of CanvasPilot, the help tool
  answers the index, a card, an example, and a search, there is a skill for each card, and no card about what the host
  program controls is offered.
- Drawing Knowledge in the Help menu of the composed application gives a browser over that catalog, in which the user
  can search, open a card, follow a related card, and copy an example.
- Style kits are found in the program, the folder of the user, the project, and the shared folders, the most local copy
  of a kit wins, an unreachable shared folder and a kit with a wrong value are reported without hiding the other kits,
  and the Style Kits window makes, exports, deletes, imports, and copies kits into the project, and chooses the style of
  the project, while the kits of the program cannot be changed.
- The style of a project is chosen in the pill of the title bar, is kept in the project file, comes from the default kit
  for a new project only, is the same after a restart, is said to be missing when the kit is not found, and stays empty
  for a project that never had one.
- The shown picture is exported at the sizes of the style kit and of the application with the size, the background, and
  the resolution that the size says, and as drawn; a project with no style is offered the sizes of the application only.
- The guidance, the two tools, and the card about style kits agree: the guidance is within its budget, the tools read
  the style and check a picture of the project and refuse a file outside it, and the index of the knowledge fits.
- Check Against Style in the Image menu shows the colors on the palette and off it as notes of the preview, the notes go
  when the picture is drawn again, and a project with no style is told to choose one.
- The published win-x64 application contains the native speech and audio runtime libraries and so does the installer
  package, so that the installed application needs nothing but the model download to run a chosen speech model.
- The published win-x64 application and the installer package contain no GitHub Copilot runtime, and the executable
  stays within its size budget: the application never ships or downloads a Copilot runtime.
- The application starts on every supported platform: composing its units without a window gives a titled shell
  with the right commands available, and the log records the start and stop.
- The Windows build produces exactly one MSI package of non-trivial size for the application, named for the build
  version; installing it is checked by hand before each release.
- The main window shell creates a project, lists its image files, finds it in the recent projects after a
  restart, and keeps a changed setting across the restart.
- A folder and a drawing can be created in the open project and a picture added to it, a name or file that exists is
  never replaced, and the tree shows the result.
- In the windows of the composed application every icon-only button has a name, a tooltip, and a target of at least 28
  by 28 pixels, every field has a name, the menus have unique access keys, the shared buttons show the focus ring on the
  keyboard only, and the text of notices and of the welcome screen has the contrast that is asked for.

## Manual Verification

These checks need a person, a physical machine, and programs or hardware that the automated tests cannot stand in for.
They are performed on an installed package before each release and are not evidence for a requirement. **None of them
has been performed on the machine on which this version was built and tested, except the live GitHub Copilot runs, the
start of the published application, and the package inspection that check 10 records**: the automated tests prove the
application's own behavior with substitutes (a managed recognition backend and a recorded sound file instead of a
microphone, headless pointer and keyboard events instead of an operating system drag, and Avalonia's own clipboard
and data objects instead of the Windows ones), so what follows is unverified until someone ticks it off. The detailed
steps are in the verification of the unit or subsystem named at each check.

1. *Real dictation.* On a machine with a microphone, install the MSI, download a speech-to-text model on the Speech
   page, choose it and the microphone, open a project, and dictate with both the talk button and the F8 key: the words
   show while the control is held, one dictated block is added at the caret on release, and one undo removes it. Also
   try the reasons: no model chosen, a model not downloaded, the microphone unplugged. (Verification of the `Speech` and
   `Chat` subsystems.)
2. *Windows microphone privacy.* Turn off "Let desktop apps access your microphone" and hold the talk button: the panel
   says that the microphone cannot be used and the application keeps running. (Verification of the `Chat` subsystem.)
3. *Dragging the shown picture out.* Drag the shown picture into Windows Explorer, a PowerPoint slide, the body of an
   Outlook message, and Paint: each should receive the picture, as a file or as a bitmap. Which of the two formats each
   program takes is not known, because the drag was never tried against these programs; if a program refuses the
   picture, record which, since the drag then needs another format. Also check that a drop on a program that refuses it
   leaves the application working. (Verification of `ImageDragSource`.)
4. *Real clipboard.* Press Print Screen or use the Snipping Tool, paste into the message, and check that a picture chip
   shows the screenshot and is sent; copy files in Explorer and paste: file chips; copy text from another program and
   paste: text. Check the same with a drag of a file, a folder, and a picture from Explorer and from a browser onto
   the message. (Verification of `ComposerEditor`.)
5. *Real input method.* With an input method that composes text (for example Japanese or Chinese), type into the
   message with and without chips in it: composing shows in place, the committed text is one undo step, and chips are
   not damaged. (Verification of `ComposerEditor`.)
6. *Installed package.* Install, upgrade over an older version, start from the Start Menu shortcut, and close a running
   instance by installing; and look at the installed folder, or the MSI file table (`Orca` or the Windows Installer
   `File` table), for `sherpa-onnx-c-api.dll`, `onnxruntime.dll`, and `portaudio.dll` next to `CanvasPilot.exe`. The
   build already fails when any of the three is missing from the published folder or the file table
   (`Speech_NativeRuntimePublished`); this check confirms what a user gets. Also check that no file whose name starts
   with `copilot` and no `runtime.node` is in the installed folder (the build asserts the same in
   `Copilot_RuntimeNotPublished` and in the file table). (Verification of the `Speech` subsystem and of the WiX
   Toolset.)
7. *Marking on a real pointer and a screen reader.* Draw the four mark shapes with a mouse, a pen, and touch, and with
   the keyboard, and check the tool bar and the chips with a screen reader. (Verification of `AnnotationOverlay`.)
8. *Style kits with the real pickers.* On the installed package, open Tools > Style Kits, choose Import and Export with
   the system zip pickers, add example images with the image picker in the editor, change a color with the flyout and
   its sliders, and add a shared folder on the Style Kits page of the settings with the folder picker, including a
   network path that is not reachable; then check that the three kits that come with the program are listed, that the
   title bar of a project shows the chosen kit, and that exporting at a size writes the file that the window said.
   (Verification of the `Style` subsystem and of the units `StyleKitManager`, `StyleKitEditor`, and
   `StyleSettingsPage`.)
9. *Live Ollama (a reviewer with Ollama installed; Ollama is not installed on the machine on which this version was
   built, so none of this has been run).* Install Ollama and pull a vision-capable model that can call tools (for
   example `gemma3`). Each step has a pass criterion; record the date, machine, and versions with the
   release. (Verification of the `Agent` and `Chat` subsystems and of the `AiProviderSettingsPage` unit.)
   1. *Ready.* Choose Ollama, keep the endpoint, type the model name, and see the context window of a new installation,
      32768: Check connection reports that Ollama is running with the model, and the banner of the chat panel is
      hidden.
   2. *Small context.* Set the window to 8192: the warning that the window is below 16,384 appears on the page and in
      the banner, sending stays possible; set 32768 again: the warning goes away.
   3. *A drawing.* Ask for a drawing: the answer streams, the transcript shows a line for each tool
      (`canvasnet_help`, `style_get`, `project_write`, `render_image`), the new file is selected in the explorer and
      drawn, and, after the copilot looked at its own picture, a second `project_write` can correct an error.
   4. *Replacing a file.* Ask for a change of an existing file: the line says that the file was replaced, the old
      content is gone (expected), and the project folder has no backup file and no `.canvaspilot\backups` folder.
   5. *Stop.* Stop in the middle of a turn: the text so far is kept and marked Stopped, and Send works again.
   6. *Errors.* Stop the Ollama server and send: an error notice with the remedy and **Open Settings**; ask for a
      model that is not pulled: the notice names `ollama pull {model}`.
   7. *History.* Close and open the project, and then restart CanvasPilot: the earlier messages come back as text only;
      `%LOCALAPPDATA%\CanvasPilot\history\{key}\history.json` and `project.json` exist and the project folder has no
      history file; **Clear chat history** empties the panel and removes the folder; after the project folder is
      renamed, its history is empty.
   8. *Marks and pictures.* With marks and an attached picture on a drawing, the answer shows that the model saw the
      marked picture and the close-ups (compare with **View payload**); with a model that cannot see pictures the
      answer works from the words only.
10. *Live GitHub Copilot (steps 6, 7, and 8 were performed on 2026-11-22 on the development machine and step 10 on
    2026-11-23, and they are recorded below; steps 1 to 5 and 9 are open, step 1 is covered for the sign-in of the
    Copilot CLI by step 10, and steps 2 to 4 need a machine in another state).* Choose GitHub Copilot and use **Check
    connection**, the banner, and a conversation. (Verification of the `Agent` subsystem and of the `CopilotCliLocator`,
    `CopilotRuntimeStarter`, `AgentReadiness`, and `AgentProviderFactory` units.)
    1. With the winget CLI at version 1.0.113 or later and the Copilot CLI signed in (run `copilot`, then type
       `/login`), or `gh auth login` done, or one of `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, `GITHUB_TOKEN` set: the check
       ends with "GitHub Copilot CLI {version} is ready." followed by "Signed in as {login} on {host}
       ({kind of sign-in})." (for example "(Copilot CLI sign-in)" for the sign-in of the CLI, "(token variable)", or
       "(GitHub CLI sign-in)"), the tooltip of the provider pill shows the same sentence, no token is shown, the banner
       of the chat panel is hidden, and a sent message gets an answer.
    2. With no token variable, no `gh`, and a Copilot CLI that is not signed in (or a data folder with no sign-in): the
       "not signed in" sentence, which tells the user to run `copilot` and type `/login` and names `gh auth login` and
       the variables as alternatives, followed by "What was found:" and a digest of what the library looked at; the
       banner offers **Open Settings**, and a sent message gets an error notice with the sentence and starts no run.
       After the user signs in, Check connection finds the sign-in without restarting CanvasPilot.
    3. With only the VS Code `copilot-shim` on the `PATH` (take the winget entry out of the `PATH` and clear the path in
       the settings): the "not found" sentence ends with "Only the VS Code copilot shim was found at ...". With the path
       of the real `copilot.exe` in the settings the CLI is found. With the path of the shim in the settings the choice
       is accepted as the user's own and only its version is checked.
    4. With a CLI at version 1.0.112 or older: the "older than 1.0.113" sentence with `winget upgrade GitHub.Copilot`.
    5. The installed MSI holds no file whose name starts with `copilot` and no `runtime.node` anywhere in the install
       folder, the size of `CanvasPilot.exe` is within the range that `Copilot_RuntimeNotPublished` asserts (about 125
       MB), and the banner is clear on a machine that is signed in. (This repeats part of check 6.)
    6. *Live runs on the installed CLI.* On 2026-11-22, on the development machine (Windows, Copilot CLI 1.0.114 in the
       winget package folder, the GitHub CLI signed in to github.com, no token variable), the opt-in tests
       `LiveCopilotTurn_AgentService_ListsProjectFilesAndRendersAPicture` and
       `LiveCopilotTurn_ComposedApplication_ChatWritesADrawingOnTheSharedRuntime`
       (`LiveCopilotTurnTests.cs`; skipped unless `CANVASPILOT_LIVE_COPILOT` is `1`; the model is taken from
       `CANVASPILOT_LIVE_COPILOT_MODEL`, and empty means the default of the runtime) passed:
       1. *Run 1 (the agent service with the real factory and starter, model `claude-haiku-5.5`).* The tools
          `project_list` and then `render_image` were called, the final text was "I found one file, `box.svg` (114
          bytes), and the picture is 80 x 40 pixels: a plain blue rectangle.", and the turn was `Completed` in about 21
          seconds.
       2. *Run 2 (the same test with the default model of the runtime).* The same two tools were called, the final text
          was "I found one file, `box.svg` (114 bytes), and its picture is 80 x 40 pixels.", and the turn was
          `Completed`.
       3. *Run 3 (the composed application, which is the product wiring: the real locator, readiness, and factory over
          one shared starter, the default settings that choose GitHub Copilot, model `claude-haiku-5.5`).* The banner
          was clear (no readiness issue); the message produced the transcript rows User, Tool, Tool, and Assistant;
          `box.svg` was written in the project; and the starter was asked 3 times and handed out exactly 1 runtime, so
          the readiness check and the conversation share one runtime.
       A run of the packaged application with a window and a conversation in it is not covered by these runs (see steps
       8 and 9).
       4. *Run 4 (model settings through the Settings page of the composed application; the opt-in test
          `LiveCopilotTurn_SettingsPage_ListsModelsAndTheChosenModelAndEffortRun`, with `CANVASPILOT_LIVE_COPILOT_MODEL`
          and `CANVASPILOT_LIVE_COPILOT_EFFORT` for another choice).* The page listed 23 models of the account in one
          list (no message): none lacks tool calling, none lacks pictures, none is unusable, and all 23 offer reasoning
          levels. The ids and levels were `claude-haiku-5.5`, `claude-opus-5.5`, `claude-sonnet-5`, and
          `claude-sonnet-5.5` (low, medium, high, xhigh, max); four `gemini-4` models (low, medium, high);
          `gpt-5.3-codex` (low to xhigh); `gpt-5.6-luna`, `gpt-5.6-sol`, `gpt-6-luna`, and `gpt-6-sol` (none to max);
          five more `gpt-6` models (low to max); `grok-4.6` and `grok-4.7` (low to xhigh); `mai-code-2` and
          `mai-code-2-flash` (low to max); and `mai-code-1.1-flash` (low, medium, high). The test chose
          `claude-haiku-5.5` and the level
          `low` on the page (the details read "Tool calling: yes", "Pictures: yes", "Reasoning levels: low, medium,
          high, xhigh, max", and "Context window: up to 1,000,000 tokens"); OK stored `claude-haiku-5.5` and `low`; the
          banner was clear; the message produced the rows User, Tool, Tool, and Assistant and wrote `box.svg`; the
          starter was asked 4 times and handed out exactly 1 runtime. With the stored level changed by hand to `bogus`,
          the banner said "Model 'claude-haiku-5.5' does not support reasoning effort 'bogus'. Supported: low, medium,
          high, xhigh, max." (the sentence of the AI library, from the live catalog). Not covered: a model that lacks
          tool calling or pictures (this account has none), and the packaged application.
       5. *Run 5 (the default-deny shell check; the opt-in test
          `LiveCopilotTurn_AgentService_ShellCommandRequest_IsRefusedAndRunsNothing`, through the agent service with the
          real factory and starter).* The model was asked to run a shell command that writes `shell-ran.txt` and to say
          so if it is not allowed to. With the model `claude-haiku-5.5` and with the default model of the runtime: no
          tool was started (the list of started tools was empty), no `shell-ran.txt` exists in the project folder or
          beside it, and the turn was `Completed`. The answers were "I can't run that: this session has no shell tool,
          and I'm not allowed to write the file another way." and "I can't run that command. I have no shell tool here,
          only the CanvasNet drawing and project file tools, and you asked me not to write the file any other way." So
          the copilot is refused: the runtime offers it no shell tool, and nothing outside the tools of the
          application runs. What this run does not show is a refusal by the permission handler itself, because the
          model never asked for a tool that it was not offered; the permission handler is the library's own and is not
          reached in this run.
    7. *Packaging with beta.4 (final build).* On 2026-11-22 the win-x64 single-file publish of this change was made as
       the pipeline does (`dotnet publish` with
       `--runtime win-x64 --self-contained true --property:PublishSingleFile=true`, then the `.pdb` and `.xml` files
       removed) and the MSI was built from it with the WiX project (version 0.0.1). The published folder holds 19 files
       (`CanvasPilot.exe`, six native libraries for graphics, text shaping, speech and audio, and the three style
       kits with four files each) and no file whose name
       starts with `copilot` or `libcopilot`, no `runtime.node`, and no `.copilot-explicit-cli`. `CanvasPilot.exe` is
       131,489,762 bytes (125.4 MB; 131.3 MB with AgentKit 0.1.0-beta.3). The MSI is 58,281,984 bytes (55.6 MB; 55.5 MB
       with beta.3), and its file table lists 19 files (the executable, the six libraries, and the twelve style kit
       files) and none of those names. `dotnet fileassert installer` passed all four assertions (`WiX_MsiPackage`,
       `Speech_NativeRuntimePublished`, `Copilot_RuntimeNotPublished`, and `StyleKits_BuiltInPublished`). The build
       output of the application and of the three test projects holds no Copilot runtime file either (no file that
       starts with `copilot` or `libcopilot`, no `runtime.node`, and no file over 100 MB other than an executable).
       *Re-measured after the upgrade of the AI libraries to beta.5 (2026-11-23).* The same publish (without the MSI,
       which was not rebuilt) holds 20 files (the 19 above and `THIRD-PARTY-NOTICES.md`) and no file whose name starts
       with `copilot` or `libcopilot`, no `runtime.node`, and no `.copilot-explicit-cli`. `CanvasPilot.exe` is
       131,943,394 bytes (125.8 MB, 0.4 MB more than with beta.4), well below the limit of 160 MB, so the limit stays.
       The FileAssert assertion `Copilot_RuntimeNotPublished` passed on this folder, and `smoke.ps1` passed (the main
       window "CanvasPilot" after 10 seconds and no error in the log).
    8. *The published application starts (the window was not used).* On 2026-11-22 the published `CanvasPilot.exe` was
       started from `publish\win-x64`: after 10 seconds the process was alive and responding, its window was titled
       "CanvasPilot", it used about 199 MB of working set and had started no child process, the log
       (`%LOCALAPPDATA%\CanvasPilot\logs\canvaspilot.log`) had the entries "No settings file ... using default
       settings" and "Application started.", and the process was then closed by its process id. The installed MSI, the
       window, the Settings page, and the chat panel were not used.
    9. *Open (a person with the installed package).* Model list and reasoning level in the window: open Settings > AI
       Provider with GitHub Copilot selected: the models of the account are listed with their notes, the choice of a
       model shows its capabilities, the reasoning level is enabled with the levels of the model, and OK stores them;
       **Refresh models** asks again. With the Copilot path set to a missing file the page says why there is no list and
       the model name can still be typed. For Ollama (check 9): the models that are pulled are listed, a model without
       tool calling is warned about and refused by the banner, and with the server stopped the page keeps the typed name
       and says that Ollama did not answer. Then a live turn in the packaged application with a picture, a tool that
       returns a picture, and Stop, a look that the project folder holds only intentional files afterwards, and a look
       that no `copilot` process that the application started remains after it is closed. In that turn ask the copilot
       to run a shell command: the transcript shows no tool line for a tool that is not one of the tools of the
       application (run 5 shows this through the agent service without a window).
    10. *Copilot CLI sign-in live runs.* On 2026-11-23, on the development machine (Windows, Copilot CLI 1.0.114), the
        five opt-in live tests of `LiveCopilotTurnTests.cs` passed in both runs below, in 36 to 37 seconds each run:
        `LiveCopilotTurn_AgentService_ListsProjectFilesAndRendersAPicture`,
        `LiveCopilotTurn_AgentService_ShellCommandRequest_IsRefusedAndRunsNothing`,
        `LiveCopilotTurn_ComposedApplication_ChatWritesADrawingOnTheSharedRuntime`,
        `LiveCopilotTurn_SettingsPage_ListsModelsAndTheChosenModelAndEffortRun`, and the new
        `LiveCopilotTurn_Readiness_ReportsHowCopilotSignedIn`. No token is recorded here or by the tests.
        1. *Run 1 (the normal machine).* `gh` was on the `PATH` and was signed in only to `hiarc.ghe.com` (the same
           account), no token variable was set, and `COPILOT_HOME` was not set. The runtime reported the sign-in source
           `CopilotCli`, the login `Malcolm-Nixon`, and the host `https://hiarc.ghe.com`. Check connection (the
           readiness check over the real locator and starter) was usable in 3.5 seconds, with the sentence "GitHub
           Copilot CLI 1.0.114 is ready. Signed in as Malcolm-Nixon on hiarc.ghe.com (Copilot CLI sign-in)."
        2. *Run 2 (only the sign-in of the Copilot CLI is left).* `gh` was hidden from the `PATH`,
           `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, and `GITHUB_TOKEN` were cleared, and
           `CANVASPILOT_LIVE_COPILOT_EXPECT_SIGNIN` was `CopilotCli`, so the test failed unless that source was used.
           The result was the same as in run 1 (`CopilotCli`, `Malcolm-Nixon`, `https://hiarc.ghe.com`), usable in 3.2
           seconds. So the sign-in of the Copilot CLI (`copilot`, then `/login`) works alone, with no GitHub CLI and no
           token variable, and the host follows the account of the CLI (a GitHub Enterprise Cloud host here).
        Not covered by these runs: a token variable as the source, the GitHub CLI as the only source, a live "not signed
        in" machine, and a look at the window (the Check connection result on the AI Provider page and the tooltip of
        the provider pill, which show the same sentence and are verified by the system test with a stand-in runtime).
11. *The redesigned window on a real display (a person with the installed package; none of this has been run).* Check
    the window with a mouse, a keyboard, and a screen reader on Windows at 100, 125, and 150 percent display scale.
    (Verification of the `AppShell`, `Project`, `Preview`, `Chat`, and `Annotation` subsystems.)
    1. The title bar: the installed program starts and shows the window (the launch smoke test, `smoke.ps1`, checks this
       on the published build in CI, including the start while the window loads; here it is run once by hand on the
       installed package at 100, 125, and 150 percent), the system buttons are not covered by the menu or the provider
       pill, no system title text lies over the brand mark or the menu, the height of the system buttons against the 44
       pixel bar, dragging the window by the empty strip, double-clicking it to maximize, the snap layouts, and the
       window when maximized (no part is off the screen). The menu items open with the keys and the access keys.
    2. The splitters are found and moved with the mouse (the line and the grip show on hover and when pressed), the
       widths come back after a restart, and a window moved to another monitor and back stays on the screens.
    3. Zoom: Ctrl+wheel, Ctrl+0, Ctrl+1, Ctrl+plus, Ctrl+minus while the preview has the focus, and panning with the
       middle button; the tool keys V, R, E, F, and A choose a tool while the focus is in the preview and not in a text
       box.
    4. The chat: Enter sends and Shift+Enter writes a line break; hold the talk button and the F8 key (the button turns
       red and pulses); the Attach button opens the system file dialog; the header menu clears the history; the collapse
       button hides the column and the chip on the stage brings it back.
    5. A screen reader reads the names of the icon buttons, the status bar sentence, and the AI provider pill, and the
       arrow keys move between the cards of the welcome screen and between the rows of the project tree.

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

**CanvasPilot_ConfigureAiProvider_FreshAndStoredContextWindow_UsesNewDefaultAndKeepsStoredValue**: A settings folder
without a file is loaded and holds a context window of 32768 tokens. In a second folder, settings with a context window
of 8192 are saved, another setting (the recent projects) is changed with an update, and the file is loaded by a new
service; the context window is still 8192. This scenario is tested by
`CanvasPilot_ConfigureAiProvider_FreshAndStoredContextWindow_UsesNewDefaultAndKeepsStoredValue`.

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

**CanvasPilot_ManageProjectFiles_NewFileFolderAndAddedPicture_ShowInTheTreeAndReplaceNothing**: The composed
application's user creates a project that holds one SVG file and has a picture outside the project. A new folder and a
new Mermaid file are made, the new file being created in the new folder and selected; making the same file again is
refused with a message; and the picture is added twice, which copies it into the `attachments` folder under two
different names and leaves the original where it was. The tree lists the new folder and the existing file, and the
existing file is unchanged. This scenario is tested by
`CanvasPilot_ManageProjectFiles_NewFileFolderAndAddedPicture_ShowInTheTreeAndReplaceNothing`.

**CanvasPilot_Startup_ComposeApplication_ProducesWorkingShell**: The application's units are composed exactly as
at startup, without a window, over temporary per-user folders; the shell is titled with the application name,
the commands that need nothing are available, those that need a project are not, the default settings are in
effect, and the log holds the start and stop entries. The platform requirements link this test to each platform
and runtime with source filters. This scenario is tested by
`CanvasPilot_Startup_ComposeApplication_ProducesWorkingShell`.

**CanvasPilot_ProvideDrawingKnowledge_ComposedCatalog_IndexCardExampleAndSearchWork**: The catalog of the application,
which the Help menu shows, is asked for as an assistant would ask. The compact index is within its budget and has a line
for the card of CanvasPilot and a card of CanvasNet; the help tool answers with no arguments the same index, for a topic
the card of the marks of a message, for an example the text of the example in a fenced block, and for a query the cards
that fit it. There is one skill for each card, with names that are all different. The card of the layout of a project is
there, no card that is only about the host program is, and the card of the PDF format is. This scenario is tested by
`CanvasPilot_ProvideDrawingKnowledge_ComposedCatalog_IndexCardExampleAndSearchWork`.

**CanvasPilot_BrowseDrawingKnowledge_HelpMenuCommand_ShowsBrowserOnTheCatalog**: The application is composed as at
startup with a dialog service that plays the window, and the user chooses Drawing Knowledge. The command is usable and
gives the dialog service one browser whose tree is the roots of the catalog of the application, with the cards of
CanvasPilot among them. The user searches for a word and gets hits, opens the card of the layout of a project and sees
its title and text, follows the first related card of it, opens the card of deliverables, and copies its first example:
the text of the example is on the clipboard that records and the status names it. No message was shown. This scenario is
tested by `CanvasPilot_BrowseDrawingKnowledge_HelpMenuCommand_ShowsBrowserOnTheCatalog`.

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

**CanvasPilot_ComposeMessage_TypedAttachmentAndMark_SendsAndShowsInTranscript**: The composed application's user has a
project with a two-node Mermaid diagram shown and a file outside the project. An empty message cannot be sent. The user
types "Make ", draws a rectangle with the note "bigger" around the node `Start`, types " like ", drops the file, and
sends the message through the chat panel. While drafting nothing is in the attachments folder. The transcript holds one
entry whose items are the text "Make ", the mark chip, the text " like ", and the file chip, with the summary "1 mark, 1
attachment"; its payload text is `Make [Mark 1] like [Attachment 1: brief.txt]`, its one attachment is
`attachments/brief.txt` and the file is there with its content, its one picture holds one mark with the note and the
node `Start` as its source element, and the message, what can be undone, the marks, and the marks shown are empty. This
scenario is tested by `CanvasPilot_ComposeMessage_TypedAttachmentAndMark_SendsAndShowsInTranscript`.

**CanvasPilot_UndoRedoMessage_EachActionKind_StepsAsSpecified**: The composed application's user has a project with a
two-node Mermaid diagram shown, two files outside the project, and the message as the target of the Edit menu. The user
types two characters, dictates a block, drops the two files, marks the node `Start`, and deletes a file chip, one action
each, and the state of the message after each action is noted. The Edit menu's undo command, run until it is disabled,
passes through the noted states in reverse order down to the empty message, with one step for each character, the block,
the drop of two files, the mark and its shown mark, and the deleted chip; the redo command passes through them in order.
The files of the project are the same before and after drafting. After the message is sent, nothing can be undone or
redone and both commands are disabled. This scenario is tested by
`CanvasPilot_UndoRedoMessage_EachActionKind_StepsAsSpecified`.

**CanvasPilot_AttachToMessage_DropFileFolderImage_CopiedIntoProjectOnlyAtSend**: In the composed application's project,
a text file, a folder with a nested folder and two files, and a picture file are dropped in one drop, and a screenshot
is pasted. The message holds a file chip, a folder chip, and two picture chips, and the project has no attachments
folder. A file of the same name as the text file is then put into the attachments folder. After the message is sent, the
four attachments are in the attachments folder with their exact content, the text file under another name, the earlier
file unchanged, the pasted picture's draft folder is empty, and the originals are where they were. This scenario is
tested by `CanvasPilot_AttachToMessage_DropFileFolderImage_CopiedIntoProjectOnlyAtSend`.

**CanvasPilot_AttachToMessage_DroppedContent_IsNeverOpened**: In the composed application with a diagram shown, scripts
that would write a marker file when run, a web page, a shortcut, a picture file that is no picture, a vector picture
with a script in it, a program, and a file that another program holds open exclusively are dropped in one drop. Each
becomes a chip without a message in the status, and the picture shown is still the diagram. After the lock is released
and the message sent, every file is in the attachments folder with the content that was dropped, the originals are
unchanged, the two picture files are attachments of the kind picture, and the marker file was never written. This
scenario is tested by `CanvasPilot_AttachToMessage_DroppedContent_IsNeverOpened`.

**CanvasPilot_DictateMessage_HeldAndReleased_InsertsOneBlock**: The application is composed over a model that is
installed and chosen in the settings and a microphone that is a recorded sound file, with a project and the typed text
"Please ". The talk key is pressed: the shell and the chat panel report talking, the message cannot be sent and is
unchanged. When the key is released nothing is talking, the status is empty, and the message holds the typed text and
one block of dictated text "Make the second box blue." in that order; one undo removes the whole block and redo puts it
back. This scenario is tested by `CanvasPilot_DictateMessage_HeldAndReleased_InsertsOneBlock`.

**CanvasPilot_DictateMessage_NoModel_ExplainsAndKeepsDraft**: The application is composed with a project, the typed text
"Keep this ", and a microphone that fails when it is opened. The talk key is pressed and released with no model chosen:
the status names the Speech page, the reason is that no model is chosen, and the message is unchanged, idle, and has the
one step of typing. With a model chosen that is not installed, the status is another one that names the model and the
reason is that it is not installed. The microphone was never opened, the message can still be sent, and what is sent is
"Keep this". This scenario is tested by `CanvasPilot_DictateMessage_NoModel_ExplainsAndKeepsDraft`.

**CanvasPilot_MarkImage_DrawOnShownPicture_AddsNumberedChipPinnedToSnapshot**: The composed application's user has a
two-node Mermaid diagram shown and the typed text "Look: ". The user draws a rectangle with the note "this one" around
the node `Start` with the pointer, types " and ", and draws an ellipse with the keyboard cursor, skipping the note. The
two marks are numbered one and two, are a rectangle and an ellipse, and each holds the very picture that was shown, with
its file and pixel fingerprints; the rectangle's points enclose the node; the message holds the text, the first chip,
the text, and the second chip in that order; and the layer shows both marks. This scenario is tested by
`CanvasPilot_MarkImage_DrawOnShownPicture_AddsNumberedChipPinnedToSnapshot`.

**CanvasPilot_MarkImage_Rerender_DoesNotMoveMarks**: The composed application's user has a two-node Mermaid diagram
shown and marks the node `Start` with a note. Drawing the unchanged file again gives a picture with the same snapshot
key and shows the mark. After the file is edited and drawn again, the picture has other pixels, the layer shows no mark
and counts one hidden mark, the message still holds the chip, and the mark still holds the first picture and the same
points. The message that is sent has a picture with the fingerprints of the first picture, the mark with the same
points, and the node `Start` as the source element under it. This scenario is tested by
`CanvasPilot_MarkImage_Rerender_DoesNotMoveMarks`.

**CanvasPilot_DefineStyleKits_BuiltInUserProjectAndSharedKits_ResolveByPrecedence**: The application is composed with a
kit `clean` in a shared folder and in the folder of the user, a second shared folder that does not exist, and a project.
The kit of the user wins over the shared and built-in copies. A copy added to the project then wins and the other three
are listed as hidden; the program has its three kits; a kit of the project with a wrong color is listed as broken with
the message that names `palette.primary` and is not offered. As the more local copies are deleted the user's copy, the
shared copy, and the copy of the program win in turn, and the missing shared folder is reported while the other places
are still read. This scenario is tested by
`CanvasPilot_DefineStyleKits_BuiltInUserProjectAndSharedKits_ResolveByPrecedence`.

**CanvasPilot_ManageStyleKits_ShellCommandAndManager_CreateEditExportImportAndDelete**: The Style Kits command of the
shell gives a window with the groups of the project, the user, the shared folders, and the program. The user makes a
kit, describes it in the editor, and saves it; the file is in the folder of the user. The kit is exported to a zip file
that holds its `style.json`, deleted after a confirmation, and imported again with the same file. For a kit of the
program the edit and delete commands are not available and copying into the project is; the copy has its folder in the
project, wins over the kit of the program, and is made the style of the project, which the project file says. This
scenario is tested by `CanvasPilot_ManageStyleKits_ShellCommandAndManager_CreateEditExportImportAndDelete`.

**CanvasPilot_ChooseProjectStyle_PickerAndNewProject_KeepTheChoiceInTheProjectFile**: With a default kit in the
settings, a new project says that kit in its file and the title bar shows it. The user chooses another kit, no style,
and a kit again, and the file follows each choice. After the application is started again on the same files the project
shows the chosen kit and not the default. A kit that is not found is shown as missing with the reason, and a project
created without the shell has no style, shows none, and its file has no style property. This scenario is tested by
`CanvasPilot_ChooseProjectStyle_PickerAndNewProject_KeepTheChoiceInTheProjectFile`.

**CanvasPilot_ExportAtPresetSizes_ComposedExporter_WritesPngOfThePresetSize**: A project uses a kit with two sizes and a
dark background and shows a small picture with empty corners. The export offers the picture as drawn, the two sizes of
the kit, and the three of the application. The size of the kit gives a PNG of exactly that size with the background of
the kit in the corner; the A4 size gives its size and a resolution of 5906 pixels per meter in the `pHYs` chunk; the
picture as drawn has its own size. With the style taken away only the picture as drawn and the sizes of the application
are offered, and the background is white. This scenario is tested by
`CanvasPilot_ExportAtPresetSizes_ComposedExporter_WritesPngOfThePresetSize`.

**CanvasPilot_GuideAssistantStyle_ComposedParts_GuidanceCheckAndCardAgree**: For a project that uses a built-in kit, the
style tool answers the guidance of the kit, within the budget, with its palette; the check tool passes a picture that
uses only palette colors and fails one with another color and names it, and refuses a file outside the project. With the
style taken away the style tool says to ask the user to choose a kit. The card about style kits names both tools and the
index of the knowledge is within its budget. This scenario is tested by
`CanvasPilot_GuideAssistantStyle_ComposedParts_GuidanceCheckAndCardAgree`.

**CanvasPilot_CheckPictureAgainstStyle_ImageMenuCommand_ShowsNotesInTheDiagnosticsArea**: The Check Against Style
command is not available before a picture is shown. For a picture on the palette it adds a summary that says passed and
a note of the palette colors; for one with another color it adds a summary that says failed and a warning that names the
color; drawing a picture again removes the notes. With the style taken away the command shows a message that says there
is no style kit. This scenario is tested by
`CanvasPilot_CheckPictureAgainstStyle_ImageMenuCommand_ShowsNotesInTheDiagnosticsArea`.

**StyleKits_BuiltInPublished**: After the Windows build job publishes the application self-contained for win-x64,
FileAssert asserts that the `StyleKits` folder beside the executable holds three `style.json` files and, for each kit,
the three example images, and a following step reads the file table of the installer package; the installer takes the
published folder whole. This scenario is tested by `StyleKits_BuiltInPublished`, which is run in CI only.

**Speech_NativeRuntimePublished**: After the Windows build job publishes the application self-contained for win-x64,
FileAssert asserts that the published folder, which the installer takes whole, holds exactly one each of
`sherpa-onnx-c-api.dll`, `onnxruntime.dll`, and `portaudio.dll`, each of a plausible size, and a following step reads
the installer package's file table and fails when any of the three is missing. This scenario is tested by
`Speech_NativeRuntimePublished`, which is run in CI only on Windows and is described in the verification of the Speech
subsystem and of the Speech OTS item.

**Copilot_RuntimeNotPublished**: After the Windows build job publishes the application self-contained for win-x64,
FileAssert asserts that the published folder, which the installer takes whole, holds no file whose name starts with
`copilot` or `libcopilot`, nothing under `runtimes\*\native\copilot*`, no `runtime.node`, and no
`.copilot-explicit-cli`, and that `CanvasPilot.exe` exists once and is below 160 MB; and the step that reads the
installer package's file table fails when it lists such a file. This scenario is tested by
`Copilot_RuntimeNotPublished`, which is run in CI only on Windows and is described in the verification of the AgentKit
OTS item.

**CanvasPilot_UseInstalledCopilotCli_MachineWithShim_UsesInstalledCliAndHonorsStoredPath**: The file system,
environment, and programs are replaced by the layout of the development machine, which has the winget Copilot CLI and
the VS Code shim on the `PATH`. With the default settings the locator finds the winget CLI and runs only it. A settings
file that holds a path to a file that does not exist is saved and loaded by a new service; the result for that path is
not found, names the path, and no program is run, so the installed CLI is not used instead. This scenario is tested by
`CanvasPilot_UseInstalledCopilotCli_MachineWithShim_UsesInstalledCliAndHonorsStoredPath`.

**CanvasPilot_ReportProviderReadiness_CopilotAndOllamaMachines_GiveOneActionableSentenceEach**: The settings of a new
installation (GitHub Copilot) are loaded by a settings service and checked on the development machine, whose Copilot CLI
is good, with a starter that says that the runtime finds no sign-in: the status is not signed in and the sentence names
`/login`, `gh auth login`, and `COPILOT_GITHUB_TOKEN`. After the starter is changed to start a runtime that signed in as
`mnixon` on `https://github.com` through the Copilot CLI (the user signed in), the same check reports Copilot ready with
"GitHub Copilot CLI 1.0.114 is ready. Signed in as mnixon on github.com (Copilot CLI sign-in)." A settings file that
holds an Ollama choice with a window of 8,192 tokens is saved and loaded by a new service; against a scripted server
that has the model, the result is the warning status, which can run a turn, with the sentence that tells the user to
raise the window in Settings to the recommended 32,768. This scenario is tested by
`CanvasPilot_ReportProviderReadiness_CopilotAndOllamaMachines_GiveOneActionableSentenceEach`.

**CanvasPilot_ReportProviderReadiness_CopilotNotSignedIn_BannerSaysHowToSignInAndSendDoesNotRunIt**: The application is
composed over the real factory of the providers on the fake development machine, which has the installed CLI, with the
default settings (GitHub Copilot) and a starter that answers that the runtime is not signed in. The panel checks the
provider and the banner shows the sign-in sentence of `CopilotRuntimeStarter` (run `copilot` and type `/login`); the
check asked the starter once, because the runtime start decides the sign-in. A message is sent; the starter is asked and
the conversation ends with
a notice that holds the same sentence, has no answer of the copilot, and the only processes that were run
are the version checks of the installed CLI. This scenario is tested by
`CanvasPilot_ReportProviderReadiness_CopilotNotSignedIn_BannerSaysHowToSignInAndSendDoesNotRunIt`.

**CanvasPilot_ReportProviderReadiness_CheckConnection_OllamaStubAndCopilotFakeMachine_ShowOneSentenceEach**: The
settings dialog of the composed application is opened and the AI Provider page chooses Ollama and a model that the
stand-in server has: Check connection says that Ollama is running with the model; with the server not answering it says
that Ollama did not answer. With GitHub Copilot chosen on the fake machine, with a starter that finds no sign-in, it
names `/login` and `gh auth login`; after the starter is changed to start a runtime that signed in through the Copilot
CLI it says "GitHub Copilot CLI 1.0.114 is ready. Signed in as mnixon on github.com (Copilot CLI sign-in)."; with a path
of a file that does not exist it names the
path. The stored settings are the same afterwards. This scenario is tested by
`CanvasPilot_ReportProviderReadiness_CheckConnection_OllamaStubAndCopilotFakeMachine_ShowOneSentenceEach`.

**CanvasPilot_ReportProviderReadiness_CopilotSignedInWithCopilotCli_CheckConnectionAndPillTooltipNameTheAccountAndNoToken**:
The application is composed over a runtime that signed in as `mnixon` on `https://github.com` through the Copilot CLI,
on a machine whose environment also holds a token variable with a sentinel value. Check connection on the AI Provider
page succeeds and its result names "mnixon on github.com" and "Copilot CLI sign-in"; after the chat panel checks the
provider, the tooltip of the provider pill of the title bar names the same account, host, and kind of sign-in; and the
sentinel is in neither. This scenario is tested by
`CanvasPilot_ReportProviderReadiness_CopilotSignedInWithCopilotCli_CheckConnectionAndPillTooltipNameTheAccountAndNoToken`.

**CanvasPilot_ChooseModel_CopilotModelAndReasoningLevel_AreStoredAndReachTheAgent**: The application is composed over
the real factory on a fake machine with the installed CLI and a fake runtime whose catalog offers
`claude-haiku-5.5` (with the levels low, medium, and xhigh) and `blind-model`. The settings dialog opens, the AI
Provider page lists the models of the runtime (the default entry and the two), the test chooses `claude-haiku-5.5` and
the level `xhigh` as a user would, and OK stores them. The panel checks the provider and the banner is clear; a message
is sent, and the agent was made on the runtime with a selection of that model and that level. The settings file holds
`claude-haiku-5.5` and `xhigh`, and the answer is shown. This scenario is tested by
`CanvasPilot_ChooseModel_CopilotModelAndReasoningLevel_AreStoredAndReachTheAgent`.

**CanvasPilot_ChooseModel_SettingsThatCannotWork_BannerSaysSoBeforeTheFirstTurn**: The same application with a catalog
of a model that cannot call tools, a model that cannot see pictures, and a model with levels. With each model stored in
turn (and for the last, the level `bogus`) the panel checks the provider: the banner says that the model does not
support tool calling, says that the model does not support the reasoning effort `bogus` and which are supported, and
says that the model does not accept pictures; all three are readiness issues. A message sent with the model that cannot
call tools shows the sentence as a notice, and no agent was made on the runtime. This scenario is tested by
`CanvasPilot_ChooseModel_SettingsThatCannotWork_BannerSaysSoBeforeTheFirstTurn`.

**CanvasPilot_ConverseWithCopilot_CopilotRuntimeOnInstalledCli_StreamsAnswerAndWritesDrawing**: The application is
composed over the real factory of the providers on a fake machine that has the installed CLI, with
the default settings (GitHub Copilot), and a start of the runtime that hands over a fake runtime whose agents run a
scripted model that writes `box.svg` with the project tool and then says that the box is drawn. The banner is clear. A
message is sent; the conversation holds the user message, an answer, a tool row, and the last words ("The box is
drawn."), and `box.svg` is in the project. The runtime was asked for on the installed CLI that the locator found with a
data folder below the local data root, and the agent that was made on it got the project folder as its working
directory and the tool `project_write`. This scenario is tested by
`CanvasPilot_ConverseWithCopilot_CopilotRuntimeOnInstalledCli_StreamsAnswerAndWritesDrawing`.

**CanvasPilot_ConverseWithCopilot_ScriptedModel_StreamsAnswerWritesDrawingAndExplorerShowsIt**: The application is
composed with the copilot over a scripted model that says a word, writes `box.svg` with the project tool, and says that
it is drawn; Ollama is chosen and a project is created. A message is sent; the conversation holds the message, the
words, a tool row that says the file was created, and the last words; the file exists, the explorer selects it, and the
preview shows its picture of 40 by 20. The agent was made with the tools `canvasnet_help`, `style_get`, `style_check`,
`project_write`, and `render_image`, and with no tool of another family. This scenario is tested by
`CanvasPilot_ConverseWithCopilot_ScriptedModel_StreamsAnswerWritesDrawingAndExplorerShowsIt`.

**CanvasPilot_KeepChatHistoryOnThisComputer_ComposedApplication_HistoryIsUnderTheGivenRootAndClearRemovesIt**: After the
turn of the scenario above the history folder of the project (its key from the project file) is below
`CanvasPilot\history` of the local data root that the composition was given and holds `history.json` and `project.json`;
the project holds only its project file, its empty data folder, and the drawing, and no file with "history" in its name.
The composition is disposed and made again over the same folders and the project is opened: the conversation is shown as
restored text rows and none is a message of this session. Clear chat history removes the folder and the restored rows.
This scenario is tested by
`CanvasPilot_KeepChatHistoryOnThisComputer_ComposedApplication_HistoryIsUnderTheGivenRootAndClearRemovesIt`.

**CanvasPilot_ConfineCopilotToProject_CreatedProject_ToolsAreTheApplicationsAndTheProjectFilesAreOutOfReach**: A project
is created by the project service, and the tools of the copilot are built for its folder over the real drawing knowledge
with a supplied pack that keeps its policy. Only the knowledge tool and the supplied pack's tool are offered, the
knowledge tool answers with the index of the real cards, and the policy permits writing a picture file but refuses the
project file and the data folder that the project service made, and a path that climbs out of the project. This scenario
is tested by
`CanvasPilot_ConfineCopilotToProject_CreatedProject_ToolsAreTheApplicationsAndTheProjectFilesAreOutOfReach`.

**CanvasPilot_ConfineCopilotToProject_ProjectTools_WriteReplaceAndDrawInsideTheProjectAndNothingElse**: On a project
that the project service created, with a secret picture beside the project folder, the copilot's project tools create a
drawing, replace it directly, and draw it at the new size. The project file, a file in the data folder (also under a
`backups` name), and a path that climbs out to the secret are written without effect, each answer being a refusal.
Afterwards the project file and the secret are unchanged, the data folder is still empty, and the project folder holds
only the data folder, the project file, and the drawing. This scenario is tested by
`CanvasPilot_ConfineCopilotToProject_ProjectTools_WriteReplaceAndDrawInsideTheProjectAndNothingElse`.

**CanvasPilot_DesktopShell_CreateProject_ExplorerRecentProjectsAndSettingsRoundTrip**: The composed application's user
creates a project through the New Project dialog; two files are added, one an image; the explorer lists only the
image and the title names the project. The user changes the AI model name in the settings dialog and the
application is restarted: the project is in the recent projects menu and the model name is in effect. This
scenario is tested by `CanvasPilot_DesktopShell_CreateProject_ExplorerRecentProjectsAndSettingsRoundTrip`.

**CanvasPilot_Theme_ComposedWindowIsDarkInDarkAndLightInLight**: The composed application's main window is drawn
headless in the dark and in the light theme variant (a headless UI test in `CanvasPilotThemeTests.cs`); the picture of
the dark window is dark on average and the picture of the light window is light. This scenario is tested by
`CanvasPilot_Theme_ComposedWindowIsDarkInDarkAndLightInLight`.

**CanvasPilot_Theme_ChoiceInSettingsDialogChangesTheWindowAndIsRemembered**: The application is composed for the running
application with its main window shown (a headless UI test in `CanvasPilotThemeTests.cs`); the user opens Settings from
the Tools menu, chooses the Appearance page, chooses Dark, and chooses OK. The window turns dark and a new settings
service over the same folder reads the dark choice. This scenario is tested by
`CanvasPilot_Theme_ChoiceInSettingsDialogChangesTheWindowAndIsRemembered`.

**Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize**: The windows of the composed application (the welcome
screen, the workspace of an open project, About, Drawing Knowledge, New Project, Style Kits, and every page of the
Settings dialog) are walked in both theme variants (a headless UI test in `AccessibilityViewTests.cs`). Every button or
toggle that shows no text has a name and a tooltip, can take the keyboard focus, and is at least 28 by 28 pixels, and
every text field, drop-down, slider, number box, list, and tree has a name or a placeholder. This scenario is tested by
`Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize`. The other three tests of the same file complete the
requirement: `Accessibility_MainMenu_EveryEntryHasAUniqueAccessKey` (every menu and menu item has a unique access key),
`Accessibility_ButtonThemes_TabKeyShowsTheFocusRingOnTheFocusedButtonOnly` (the main, quiet, and card buttons show the
focus ring on the Tab key and on no other control), and `Accessibility_Colors_NoticesAndWelcomeTextAreReadable` (the
text on notices, the amber button, and the welcome backdrop has a contrast of at least 4.5 to 1).

**ThirdPartyNotices_Published**: After the Windows build job publishes the application self-contained for win-x64,
FileAssert asserts that the published folder, which the installer takes whole, holds exactly one
`THIRD-PARTY-NOTICES.md` that names Fluent System Icons and the MIT License, so the notice of the vendored icons is in
the package. This scenario is tested by `ThirdPartyNotices_Published`, which is run in CI only.

**WiX_MsiPackage**: After the Windows build job publishes the application self-contained for win-x64, builds the
installer project with the numeric build version, and copies the package, FileAssert asserts that exactly one
`package/CanvasPilot-*-win-x64.msi` exists with a non-trivial size. This scenario is tested by `WiX_MsiPackage`, which
is run in CI only and is described in the WiX Toolset OTS verification document.
