# Introduction

CanvasPilot helps you create pictures from image source files such as SVG drawings, Mermaid diagrams, Vega-Lite
charts, and OpenSCAD models. You keep the files of a drawing together in a project folder, see each file's picture
as you work, and save the picture as a PNG file or copy it for use in email, presentations, and other documents.

![The workflow of CanvasPilot: create or open a project, add image sources, preview them, and export or copy the
picture](workflow.svg)

## Purpose

CanvasPilot is an AI-powered desktop application that helps users create drawings and diagrams with the
CanvasNet library. This version lets you manage projects, browse and preview their image files, export pictures, and
set the AI provider and speech preferences. It also has a chat panel in which you compose messages: you type or dictate
text, attach files, folders, and pictures, and mark places on the picture that you are talking about. You can send the
message and see exactly what it contains. The AI assistant that will answer the messages is not available yet, so a
sent message stays in the chat panel and nothing is sent to a provider.

## Scope

This user guide covers:

- Installing CanvasPilot on Windows
- Creating, opening, saving, and closing projects
- The project explorer, the preview, and its notes and errors
- Exporting a picture as a PNG file, copying it to the clipboard, and dragging it into another program
- Chatting: writing and dictating a message, attaching files, folders, and pictures, marking the picture, and sending
  the message and viewing its payload
- The Settings dialog, with the AI Provider and Speech pages
- Where CanvasPilot keeps its settings, logs, speech models, and attachments
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
embedded models in sub-folders. CanvasPilot also uses a hidden `.canvaspilot` folder inside the project for the
pictures that you paste into a message while you are still writing it, and an `attachments` folder for the files that
you send with a message (see *Chatting*). The explorer never lists either of them.

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
need it instead of keeping the dragged file. The picture that is dragged is the picture as drawn, without any marks
(see *Marking the Picture*). Not every program accepts a dropped picture in the same way; if a program
rejects it, use **Image > Export as PNG...** or **Image > Copy Image** instead.

*Not yet checked:* the drag has been tested only inside CanvasPilot's own tests, which stand in for the operating
system. It has not yet been tried against Windows Explorer, PowerPoint, Outlook, or Paint, so whether each of them takes
the file, the image data, or both is not confirmed. If a program does not take the picture, please report it (see
*Troubleshooting*).

# Chatting

The chat panel is where you compose messages about your drawing. A message can hold text that you type or dictate,
files, folders, and pictures that you attach, and numbered marks that point at places on the picture. You can send the
message and see exactly what it contains. **The assistant that will answer is not part of this version**: a sent message
is kept in the panel and nothing leaves your computer.

## The Chat Panel

The chat panel is the column at the right of the window, beside the preview. It is shown while a project is open and
hidden when there is none. Drag the splitter between the preview and the panel to change its width; it starts at 380
pixels. From top to bottom the panel has:

- the **transcript**, the messages that you sent in this project, oldest first. While it is empty it says "Messages you
  send appear here."
- the **message box**, in which you write the next message. Without an open project it says "Open a project to write a
  message." and cannot be edited. A status line below it says what is going on, for example what was left out of a drop.
- the **Hold to talk** button and the **Send** button.

The transcript lives only in memory. It is cleared when you close the project or CanvasPilot.

## Writing a Message

Click in the message box and type. Enter starts a new line; a message is sent only with the **Send** button. Select,
copy, cut, and paste work as in any text box, and a long message wraps. Text that you paste is plain text, and
characters that cannot be shown are removed.

CanvasPilot remembers what you did, so that you can step back and forth:

- **Undo** with Ctrl+Z, or with **Edit > Undo**.
- **Redo** with Ctrl+Y or Ctrl+Shift+Z, or with **Edit > Redo**.

The Edit menu acts on the part of the window that has the focus, so it works while you are in the message box and is
unavailable when the focus is elsewhere or there is nothing to undo or redo. Every action is one step: each typed
character, each deleted character, a block of dictated words, the whole of one drop or paste however many items it
holds, adding a mark, and deleting an attachment or a mark. Redo repeats the same steps. Sending a message clears the
steps, so a sent message cannot be undone. The message is not saved: if you close the project or CanvasPilot before
sending it, it is gone.

## Dictating a Message

You can speak instead of typing. Before the first time, open **Tools > Settings...** and, on the Speech page, download
a speech-to-text model, choose it, and choose your microphone (an empty choice uses the Windows default microphone).
See *Speech*.

Then dictate:

1. Click in the message box where the words are to go.
2. Press and hold the **Hold to talk** button, or hold the **F8** key while the CanvasPilot window is active.
3. Speak. The button says "Starting... keep holding" and then "Listening... let go to finish", and the words that
   CanvasPilot has recognized so far appear above the message box while you hold.
4. Let go. CanvasPilot listens for a moment longer so that your last word is not cut off, and then adds the words to
   the message at the caret as one block, with a slightly different background so that you can see what was dictated.

One Ctrl+Z removes the whole dictated block. If nothing was recognized, nothing is added and the status line says so.
Letting go of the button anywhere on the screen, switching to another window, or closing the panel also ends
the dictation, so the microphone is never left open. The microphone is used only while you hold the button or key, and
CanvasPilot keeps neither the audio nor the words, apart from the message. You cannot send a message while the button is
held.

When dictation cannot work, the panel says why and what to do instead of listening: no speech-to-text model is chosen
or it is not downloaded, no microphone is available, or the speech runtime is missing from the installation. Windows can
also block desktop applications from using the microphone; if the panel says that the microphone cannot be used, turn on
**Let desktop apps access your microphone** under **Privacy & security > Microphone** in the Windows settings. See
*Troubleshooting*.

## Attaching Files, Folders, and Images

You can add things to a message by dropping them into the message box (for example from Windows Explorer or a web
browser) or by pasting them with Ctrl+V, including a screenshot that you copied. Each file, folder, or picture becomes a
small **chip** in the text, at the place where you dropped it or at the caret when you paste. A picture shows a small
preview. Delete a chip with its **x** button, or put the caret next to it and press Backspace or Delete; Ctrl+Z brings
it back. Text that you drop or paste becomes ordinary text.

What you attach is only ever treated as data. CanvasPilot looks at what a path is and how large a file is, but it never
opens or runs a file, so even a program or a script is simply attached like any other file. Paths that are not safe to
refer to, such as device names, are left out, and the status line says which and why.

Nothing is copied while you are writing. When you **send** the message:

- a file or folder from outside the project is copied into the `attachments` folder of the project, with `(2)` and so on
  added to a name that is already taken, and nothing is ever overwritten. Your original is not changed or deleted.
- a file or folder that is already inside the project is referred to where it is.
- a pasted screenshot, which CanvasPilot saved in the hidden `.canvaspilot\attachments` folder while you were writing,
  is moved into `attachments`. Screenshots that no chip refers to any more are deleted.

The `attachments` folder is not listed in the explorer, which shows only the files in the root of the project. There are
limits: one drop accepts at most 200 items, one message copies at most 500 files, 500 folders, and 200 MB, and a pasted
picture can be at most 50 MB. A message that goes over a limit is not sent, and the status line names the item that
caused it.

## Marking the Picture

Marks let you point at the place that you mean. With a picture shown, the tool bar above it has five buttons:

- **Select** is no marking tool; the picture can be dragged out.
- **Rectangle**, **Ellipse**, **Freehand**, and **Arrow** draw that shape.

Choose a tool and drag on the picture. A click without a drag makes no mark, and Escape throws a shape away. Marks are
drawn in the pixels of the picture, so they stay on the right place however you zoom. With the keyboard, give the
picture layer the focus, move the cursor with the arrow keys, and press Enter or Space to start and to finish a shape.

When you finish a shape, an optional **Note** box appears: type a short note (up to 200 characters) and choose **Add
mark**, or choose **Skip note**. The mark gets the next number, 1, 2, 3, and so on in the order you make them, and is
drawn with its number. At the same time a chip, for example "Mark 1", is added to the message at the caret, so you can
write "the arrow in Mark 1 should point to the other box" and the reader knows which mark you mean. The mark is part of
the message exactly as long as its chip is in it: deleting the chip hides the mark, and undoing the deletion shows it
again. Marks are never made by voice.

A mark belongs to the exact picture that it was drawn on. When you change the file and the picture is drawn again, the
marks are not drawn on the new picture, because they might no longer point at the right place, but they stay in the
message and the tool bar says how many belong to an earlier picture. The message that you send holds the earlier
picture that the marks were drawn on.

While a marking tool is chosen, the picture cannot be dragged out. Choose **Select** to drag it again.

## Sending a Message and Viewing the Payload

Choose **Send** when the message is ready. Send works while the message has text or an attachment and a project is open.
CanvasPilot copies the attachments into the project, builds the payload of the message, adds the message to the
transcript with its words and chips in the order that you wrote them, and empties the message box and the marks. If
something goes wrong, the message stays as it was and the status line says what to fix.

The **payload** is everything that the assistant will receive for the message. To see it, choose **View payload** on a
message in the transcript. A window titled "What the assistant would receive" opens and shows, read-only:

- the text of the message, with `[Mark n]` and `[Attachment n: name]` where the marks and attachments were, and the same
  message in order with typed and dictated words kept apart,
- each attachment, with where you had it and its path in the project,
- for each picture that you marked: the file and fingerprints that say exactly which picture it was, the picture with
  the marks drawn and numbered on it, the area around each mark, the geometry and note of each mark in the pixels of the
  picture, and the parts of the source file that lie under each mark, such as the nodes of a Mermaid diagram or the
  marks of a Vega-Lite chart. For a type of file that does not report where things were drawn, such as an SVG drawing or
  a raster picture, the payload says that the source elements are not available and why.

Choose **Close** when you are done. Nothing in the window can be changed.

## Dragging the Marked Picture Into Another Program

You can still drag the shown picture out of the preview into another program while you are writing a message, as
described in *Drag the Picture Into Another Program*, as long as **Select** is chosen. The dragged picture is the
picture as drawn, **without the marks**; the picture with the marks drawn on it is only in the payload.

This has not been verified against specific programs: the behavior has been tested with the application's automated
tests, but not by dropping the picture into Windows Explorer, PowerPoint, Outlook, or Paint on a real machine. If a
program does not accept the picture, use **Image > Export as PNG...** or **Image > Copy Image**, and please report which
program it was.

# Settings

Choose **Tools > Settings...** to open the Settings dialog. The page list at the left shows **AI Provider** and
**Speech**. **OK** saves the changes from all pages and closes the dialog, **Apply** saves them and keeps the dialog
open, and **Cancel** closes it without saving. OK and Apply are unavailable while an entry is invalid, and the page
shows what is wrong.

## AI Provider

The AI Provider page stores your preferences for the assistant. This version only stores them; the assistant that
would use them is not available yet, so the chat does not use them, and nothing is sent to a provider.

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

The Speech page stores which audio devices and speech models CanvasPilot is to use. Dictating a message uses the
**speech-to-text model** and the **microphone** that you choose here (see *Dictating a Message*); the text-to-speech
choices are stored but not used yet.

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
| Pictures that you pasted into a message that is not sent yet | `.canvaspilot\attachments` in the project |
| Files and folders that you sent with a message | `attachments` in the project |
| Downloaded speech models | `%LOCALAPPDATA%\DemaConsulting.Speech\Models` |
| Your projects | The folder that you chose for each project |

The settings file never contains credentials. The log files stay on your computer and are never sent anywhere; they
roll over when they reach a size limit and old ones are removed, so they cannot fill the disk. To reset all settings,
close CanvasPilot and delete `settings.json`. CanvasPilot never stores what you say or the audio from the microphone;
the words of a dictation exist only in the message that you are writing. A message that you have not sent is not
saved, so it is lost when you close the project or CanvasPilot, and the transcript of sent messages is lost the same
way.

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

**The chat panel is not there.** The chat panel is on the right of the preview and is shown only while a project is
open. Open or create a project. Without one the message box says "Open a project to write a message."

**Send is not available.** Send works only while the message has some text or an attachment, no talk button is held,
and the previous message is not still being sent. Let go of the Hold to talk button, or write something.

**A message was not sent.** The status line under the message says why, and the message stays as you wrote it. The usual
cause is a limit: one message copies at most 500 files, 500 folders, and 200 MB into the project, and a pasted picture
can be at most 50 MB. Remove the item that the status names, or attach a smaller part, and send again.

**An attachment was left out.** When you drop or paste items, the status line lists what was left out and why, for
example a path that is not complete, a device name, or more than 200 items in one drop. Everything else was added.

**Dictation says it cannot listen.** The message on the panel names the reason and what to do. No speech-to-text model
is chosen, or the chosen one is not downloaded: choose and download one on the Speech page. No microphone: connect one
and choose it on the Speech page. Windows can also block desktop applications from the microphone: open the Windows
**Settings**, go to **Privacy & security > Microphone**, and turn on **Let desktop apps access your microphone**. If the
speech runtime is reported missing from the installation, reinstall CanvasPilot with the package from the releases page.

**Dictation is slow to start the first time.** The speech model is loaded the first time it is needed, which takes a
moment. CanvasPilot prepares it when the chat panel is shown. After you change the model on the Speech page the next
dictation loads it again.

**My marks are gone from the picture.** Marks belong to the picture that they were drawn on. When the file changes and
the picture is drawn again, the marks are not shown on the new picture, and the marking tool bar says how many belong to
an earlier picture. Marks are also hidden while their chip is not in the message, for example after you deleted the chip
or undid it; redo or Ctrl+Y brings them back.

**Drag-out does not work while marking.** Dragging the picture out is turned off while a marking tool is chosen. Choose
**Select** on the marking tool bar first.

**Reporting a problem.** Open an issue on the
[CanvasPilot issues page](https://github.com/demaconsulting/CanvasPilot/issues) and attach the newest file from the log
folder, after you have checked that it holds nothing private.

## References

- [CanvasPilot repository](https://github.com/demaconsulting/CanvasPilot)
- [CanvasPilot releases](https://github.com/demaconsulting/CanvasPilot/releases)
