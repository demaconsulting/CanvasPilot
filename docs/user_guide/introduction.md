# Introduction

CanvasPilot helps you create pictures from image source files such as SVG drawings, Mermaid diagrams, Vega-Lite
charts, and OpenSCAD models. You keep the files of a drawing together in a project folder, see each file's picture
as you work, and save the picture as a PNG file or copy it for use in email, presentations, and other documents.

![The workflow of CanvasPilot: create or open a project, add image sources, preview them, and export or copy the
picture](workflow.svg)

## Purpose

CanvasPilot is an AI-powered desktop application that helps users create drawings and diagrams with the
CanvasNet library. This version is the application shell. You can manage projects, browse and preview
their image files, export pictures, and set the AI provider and speech preferences that the assistant will use. The AI
chat is not available yet.

## Scope

This user guide covers:

- Installing CanvasPilot on Windows
- Creating, opening, saving, and closing projects
- The project explorer, the preview, and its notes and errors
- Exporting a picture as a PNG file, copying it to the clipboard, and dragging it into another program
- The Settings dialog, with the AI Provider and Speech pages
- Where CanvasPilot keeps its settings, logs, and speech models
- Troubleshooting

# Continuous Compliance

CanvasPilot follows the
[Continuous Compliance](https://github.com/demaconsulting/ContinuousCompliance) methodology, which ensures
compliance evidence is generated automatically on every CI run.

## Key Practices

- **Requirements Traceability**: Every requirement is linked to passing tests, and a trace matrix is
  auto-generated on each release
- **Linting Enforcement**: LintMark, TextLint, and yamllint are enforced before any build proceeds
- **Automated Audit Documentation**: Each release ships with generated requirements, justifications,
  trace matrix, and quality reports
- **CodeQL and SonarCloud**: Security and quality analysis runs on every build

# Installation

CanvasPilot is installed with a Windows Installer (MSI) package for 64-bit Windows. The package contains the
application and everything it needs to run, so no other software has to be installed first.

1. Download `CanvasPilot-{version}-win-x64.msi` from the
   [CanvasPilot releases page](https://github.com/demaconsulting/CanvasPilot/releases).
2. Run the file. Installing for all users needs administrator rights, and Windows asks for them.
3. Start CanvasPilot from the Start Menu.

Windows SmartScreen can warn that the publisher is unknown because the package is not signed yet. Choose
*More info* and then *Run anyway* if you downloaded the package from the releases page.

Installing a newer (or older) package replaces the installed version. If CanvasPilot is running, the installer asks it
to close first. Uninstalling removes the application and leaves your projects, settings, and logs in place.

# Projects

A project is a folder. It contains a project file named `{name}.canvaspilot` and the image source files of your
drawing. Put the files you want to preview in the project folder itself, and keep supporting files such as data or
embedded models in sub-folders. CanvasPilot reserves a hidden `.canvaspilot` folder inside the project for its own
use and never lists it.

## Create a Project

Choose **File > New Project...** (Ctrl+N), enter a **Project name**, and choose the **Location**. The Browse button
opens a folder picker, and the dialog starts in your Documents folder. The dialog shows the folder that will be
created, and OK is available only when the project can be created. A project name cannot be empty, cannot contain the
characters `< > : " / \ | ? *`, cannot start or end with a period, and cannot be a reserved Windows device name. A
project is created only in a new or empty folder; a folder that already has content is refused.

## Open a Project

Choose **File > Open Project...** (Ctrl+O) and select the `.canvaspilot` file of the project. A file that cannot be
read, or that was written by a newer version of CanvasPilot, is not opened, and a message tells you why.

**File > Recent Projects** lists the projects you used last, most recent first. Choose one to open it. A project whose
file no longer exists is removed from the list after a message.

## Save and Close a Project

**File > Save Project** (Ctrl+S) writes the project file. The image source files are ordinary files that you edit with
the tool of your choice, so saving updates only the project file. **File > Close Project** closes the project and
clears the explorer and the preview. **File > Exit** closes CanvasPilot.

The title bar shows the name of the open project followed by CanvasPilot.

# Working with Images

## The Project Explorer

When a project is open, the left pane lists the image source files in the project folder. It lists a file only when
CanvasPilot can draw its type, which includes SVG drawings, Mermaid diagrams (`.mmd`), Vega-Lite charts (`.vl.json`
and `.vl`), OpenSCAD models (`.scad`), pages of PDF and PowerPoint documents, and the other formats that the
bundled CanvasNet library draws.
Files in sub-folders and hidden files are not listed.

The list follows the folder: files that you add, change, rename, or delete with other tools appear in it after a
moment. Right-click a file, or select it and press F2 or Delete, to **Rename...** or **Delete...** it. Deleting asks
you to confirm first.

## The Preview

Select a file to see its picture in the right pane. CanvasPilot draws the file in the background, so the window stays
responsive while a large file is drawn, and a busy indicator with the file's name shows while it runs. When you select
another file, or the file changes, the picture is drawn again and a result that is no longer wanted is discarded. The
picture is also redrawn when a file that it uses changes.

The **Fit to window** check box scales the picture to the pane. Without it the picture is shown at its own size with
scroll bars.

A file that references other files, such as an SVG that embeds a picture, can read them only from inside the project
folder. A file outside it is left out of the picture and reported as a note.

## Notes and Errors

Below the picture, the diagnostics list shows the notes that the drawing reported, with their number, for example that
a feature is not supported or that an embedded file was denied. The picture is still shown when there are notes.

When a file cannot be drawn, the preview shows an error with a short code, a message, and, where one exists, a hint
about what to fix. An unfinished file that you are editing often produces such an error; correct the file and the
preview updates.

## Export and Copy

With a picture shown, the **Image** menu offers:

- **Export as PNG...** asks where to save the file and writes the picture, with its transparency, as a PNG file.
- **Copy Image** puts the picture on the clipboard, so you can paste it into email, a presentation, or another
  program.

Both items are disabled while no picture is shown.

## Drag the Picture Into Another Program

You can also drag the picture straight out of the preview. Press the left mouse button on the picture, move the pointer
a few pixels, and drop it on a folder in Windows Explorer, on a presentation slide, in an email message, or in another
program that accepts pictures. CanvasPilot offers the picture both as a PNG file, named after the source file, for
example `flow-1a2b3c4d.png`, and as image data, so each program can take the form it accepts. A click on the picture
does not start a drag, and nothing is dragged while no picture is shown.

The files that CanvasPilot writes for dragging are kept in the `drag` folder of its folder in
`%LOCALAPPDATA%\CanvasPilot`, and are deleted when CanvasPilot starts and when it ends, so drop the picture where you
need it instead of keeping the dragged file. Not every program accepts a dropped picture in the same way; if a program
rejects it, use **Image > Export as PNG...** or **Image > Copy Image** instead.

# Settings

Choose **Tools > Settings...** to open the Settings dialog. The page list at the left shows **AI Provider** and
**Speech**. **OK** saves the changes from all pages and closes the dialog, **Apply** saves them and keeps the dialog
open, and **Cancel** closes it without saving. OK and Apply are unavailable while an entry is invalid, and the page
shows what is wrong.

## AI Provider

The AI Provider page stores your preferences for the assistant. This version only stores them; there is no chat that
uses them yet, and nothing is sent to a provider.

- **Provider**: GitHub Copilot or Ollama.
- **Model name**: The model to use. Leave it empty for the provider's default model.
- **Context window (tokens)**: The size of the context window, from 512 to 2,000,000 tokens (default 8192).
- **Reasoning level**: Auto, Low, Medium, or High.
- **Ollama endpoint**: The address of the Ollama server (default `http://localhost:11434`). It is used when the
  provider is Ollama.
- **GitHub Copilot command-line tool path**: The path of the Copilot command-line tool. Leave it empty to locate the
  tool automatically. It is used when the provider is GitHub Copilot.
- **Allow unrestricted agent tools**: Off by default. When it is on, the future agent may use tools that are not
  limited to the project folder. Leave it off unless you trust the agent's instructions.

## Speech

The Speech page stores which audio devices and speech models CanvasPilot is to use. This version only stores the
choices; speech is not used yet.

- **Audio devices** shows the microphone (capture) and speakers (playback) that the computer has, and the buttons
  **Refresh devices** to look again. A computer without audio hardware shows a message instead of devices, and the
  rest of the page still works.
- **Speech models** lists the speech-to-text and text-to-speech models, each with its state. A model must be
  downloaded before it can be used: choose **Download** and watch the progress bar, or **Cancel** to stop. Downloads
  start at once, are not undone by canceling the dialog, and stop when the Settings dialog closes. A model that has
  tunable settings, such as a voice or a speaker number, shows editors for them below the lists.

Models are downloaded over HTTPS into a store that is shared with other DEMA Consulting applications that use the speech
library (see *Where CanvasPilot Keeps Its Files*). A downloaded model is checked before it is installed, and an
interrupted download leaves no half-installed model.

# About CanvasPilot

Choose **Help > About CanvasPilot** to see the application name, the version, the copyright notice, and the
third-party libraries that CanvasPilot uses. Include the version when you report a problem.

# Where CanvasPilot Keeps Its Files

| What | Location |
| --- | --- |
| Settings and recent projects | `%APPDATA%\CanvasPilot\settings.json` |
| Log files | `%LOCALAPPDATA%\CanvasPilot\logs` |
| Pictures prepared for dragging out (temporary) | `%LOCALAPPDATA%\CanvasPilot\drag` |
| Downloaded speech models | `%LOCALAPPDATA%\DemaConsulting.Speech\Models` |
| Your projects | The folder that you chose for each project |

The settings file never contains credentials. The log files stay on your computer and are never sent anywhere; they
roll over when they reach a size limit and old ones are removed, so they cannot fill the disk. To reset all settings,
close CanvasPilot and delete `settings.json`.

# Troubleshooting

**A file is missing from the explorer.** Only files in the root of the project folder are listed, and only types that
CanvasPilot can draw. Move the file into the project folder, or check its extension.

**The preview shows an error.** Read the code, message, and hint. Typical causes are a syntax error in the file, a
referenced file that is missing, and a file type that is not supported. The log has the details.

**An embedded picture is not drawn.** Files that a drawing references must be inside the project folder. Move the
picture into the project folder or one of its sub-folders and refer to it by a relative path.

**A project does not open.** The message names the problem. A project file that was written by a newer version of
CanvasPilot cannot be opened by an older one, and a damaged project file must be repaired or replaced.

**CanvasPilot started with default settings.** A settings file that cannot be read is kept as `settings.json.bad` next
to the original, the problem is written to the log, and CanvasPilot starts with default settings. Your projects are
not affected.

**The Speech page shows no audio devices.** The computer has no audio hardware, or its audio system cannot be used. The
rest of the page still works, and you can choose devices later.

**A model download fails.** The message on the model says why, for example that the network is blocked. Check the
connection and choose Download again. Keep the Settings dialog open while a download runs.

**Copy Image does not paste.** Some programs accept only certain clipboard formats. Export the picture as a PNG file and
insert the file instead.

**Reporting a problem.** Open an issue on the
[CanvasPilot issues page](https://github.com/demaconsulting/CanvasPilot/issues) and attach the newest file from the log
folder, after you have checked that it holds nothing private.

## References

- [CanvasPilot repository](https://github.com/demaconsulting/CanvasPilot)
- [CanvasPilot releases](https://github.com/demaconsulting/CanvasPilot/releases)
