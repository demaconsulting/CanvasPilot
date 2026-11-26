# Introduction

CanvasPilot helps you create pictures from image source files such as SVG drawings, Mermaid diagrams, Vega-Lite
charts, and OpenSCAD models. You keep the files of a drawing together in a project folder, see each file's picture
as you work, and save the picture as a PNG file or copy it for use in email, presentations, and other documents.

![The workflow of CanvasPilot in five steps: open a project, describe the picture by typing or speaking and drop in
files, the AI copilot drafts it with CanvasNet, a live preview shows the result, and you export or share the
PNG; a return arrow from the preview to the description step is for marking problems or asking for
changes](workflow.svg)

## Purpose

CanvasPilot is an AI-powered desktop application that helps users create drawings and diagrams with the CanvasNet
library. You manage projects, browse and preview their image files, and export pictures. In the conversation pane you
compose messages: you type or dictate text, attach files, folders, and pictures, and mark places on the picture that you
are talking about. When you send a message, the AI copilot answers it: it looks up how to draw, writes or changes the
files of the project, draws them, looks at the result, and fixes what it finds. The copilot works with a local Ollama
server or with your installed GitHub Copilot CLI (see *Setting Up an AI Provider*). The copilot can only use
CanvasPilot's own tools, which are confined to the project folder, and it overwrites files directly, with no backup, so
keep your projects under source control. The Help menu opens the drawing knowledge, a guide to the image formats and to
CanvasPilot that you can read card by card and that the copilot uses as well. Style kits let you keep a look (colors,
fonts, sizes, and example images) for a project or a team, export pictures at sizes that fit slides, email, and paper,
and check a picture against the colors of the style.

## Scope

This user guide covers:

- Installing CanvasPilot on Windows
- The parts of the window: the title bar with the menu and the style kit and AI provider pills, the explorer, the stage,
  the conversation, and the status bar, and how the window remembers its layout
- Creating, opening, saving, and closing projects, and the welcome screen
- The project explorer (folders, thumbnails, adding files by drag and drop), the stage with its zoom and marking tools,
  and its notes and errors
- Exporting a picture as a PNG file at its own size or at a size of your style kit or of the application, copying it to
  the clipboard, and dragging it into another program
- Chatting: writing and dictating a message, attaching files, folders, and pictures, marking the picture, and sending
  the message and viewing its payload
- Keyboard use and accessibility
- Setting up an AI provider: Ollama and GitHub Copilot, the readiness messages, the connection check, and choosing a
  model and its reasoning level
- Working with the copilot: asking, marks and attachments, what the copilot can do in the project, Stop, the chat
  history, the files that the copilot writes, and what to do about errors
- Reading the drawing knowledge: opening the browser, the tree of cards, searching, following related cards, and
  copying examples
- Style kits: choosing the style of a project, the Style Kits window, where kits are kept and which copy wins, making,
  editing, importing, and exporting kits, exporting at sizes, and checking a picture against the style
- The Settings dialog, with the AI Provider, Speech, Style Kits, and Appearance pages, and the dark and light themes
- Where CanvasPilot keeps its settings, logs, chat history, speech models, and attachments
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

The name of the downloaded file carries the full version, including a prerelease label such as `0.2.0-beta.3`. Windows
Installer versions can only be numbers, so *Installed apps* (Add/Remove Programs) shows the numeric part, for example
`0.2.0` for `0.2.0-beta.3`.

# The CanvasPilot Window

CanvasPilot has one main window. While no project is open it shows the welcome screen (see *The Welcome Screen* in
*Projects*). While a project is open it shows the workspace, which has three panes between a title bar and a status bar:
the explorer at the left, the stage with your picture in the middle, and the conversation with the copilot at the
right.

![The main window of CanvasPilot in the dark theme, open on the project Release Overview. The title bar holds the
menu, the project name, the style kit pill, and the AI provider pill. At the left the explorer lists the project's
folders and pictures with small thumbnails. In the middle the picture overview.svg sits on the stage with the floating
marking tools above it, two numbered marks on the picture, and the zoom pill below it. At the right the conversation
shows messages, tool chips, suggested prompts, and the message box, and the status bar at the bottom names the file, its
size, and its format.](main-window.png)

Everything that the new look adds is a faster way to a command that the menu already has, so you can work with the
mouse, the keyboard, or both. The parts, from the top left, are:

- **Title bar**: The CanvasPilot mark, the menu (**File**, **Edit**, **Image**, **Tools**, and **Help**, each with its
  keys and access keys), the name of the open project in bold followed by CanvasPilot, and two pills at the right
  (see below). The window buttons of the system stay at the far right end: minimize, maximize or restore, and close.
  CanvasPilot has no full-screen mode, so there is no full-screen button and F11 does nothing; a window in full screen
  has no title bar and no buttons, and the way back from it is hard to find. The pills always end before the window
  buttons, in the normal and in the maximized window, so they are never covered by them.
- **Explorer**: The files and folders of the project (see *The Project Explorer*).
- **Stage**: The picture, with the marking tools above it and the zoom pill below it (see *The Stage*).
- **Conversation**: The chat with the copilot and the message box (see *Chatting*).
- **Status bar**: One line along the bottom of the window (see *The Status Bar*).

## The Style Kit Pill and the AI Provider Pill

Two pills at the right of the title bar show what the project and the copilot use at a glance:

- The **style kit pill** shows up to three colors of the style kit of the project and its name. Choose it to pick
  another kit, or **(No style)**, from the list, or **Manage style kits...** at the end of the list to open the Style
  Kits window (see *Style Kits*). It is shown only while a project is open.
- The **AI provider pill** shows a dot, the provider in bold (**Copilot** or **Ollama**), and the model. The dot always
  comes with words, so you never have to rely on its color: a green dot with the model name means that the copilot can
  be used; a hollow dot with "checking" means that CanvasPilot is checking; an amber dot with a few words such as
  "sign-in needed", "CLI not found", or "Ollama not running" means that something needs your attention; a red dot with
  "unavailable" means that the provider cannot be used. Pause the pointer on the pill to read the full sentence, which
  is the same sentence as in the banner of the conversation and as the answer of **Check connection**. For Copilot that
  can be used it ends with the account that Copilot signed in as, for example "Signed in as octocat on github.com
  (Copilot CLI sign-in)." Choose the pill, or focus it and press Enter, to open the
  settings on the AI Provider page (see *Setting Up an AI Provider*).

## The Status Bar

The status bar along the bottom is always there. At the left it describes the picture that is shown: a check mark, the
name of the file, its size in pixels (for example `900 × 500 px`), its format, and how long drawing it took (for example
`Rendered in 84 ms`), and the number of notes when the drawing reported some. While a file is drawn it says
"Rendering..." with a spinner, and when a file cannot be drawn it says "Render failed" with an error icon. With no
project it says "No project open", and with a project and no file chosen it says "No picture selected". At the right,
while a project is open, it shows when the project file was saved, for example "Project file saved 14:32" (the time of
day for today, otherwise the date and the time), and reminds you which key is held to talk, for example "Hold F9 to
talk" (see *Dictating a Message*). The saved time is the time at which the project file was last written. It is not
shown for a project that has never been saved, because CanvasPilot cannot tell whether your drawings or the conversation
have changes that are not saved, and so it never says that your work is saved. The
status bar speaks its sentence to screen readers when it changes.

## Window Size and Layout

CanvasPilot opens at 1440 × 900 pixels the first time, and the window can be no smaller than 960 × 600. It remembers the
size, the position, and whether the window was maximized, and the widths of the explorer and the conversation, and it
restores them the next time. If the screens have changed, for example when you disconnect a monitor, the window is moved
back onto a screen that exists.

Drag a splitter, the thin line between two panes, to change the widths. The explorer can be 180 to 420 pixels wide and
the conversation 320 to 640 pixels, and the stage keeps at least 360 pixels. When the window gets narrow the explorer
gives up room first. To move a splitter with the keyboard, focus it and press the left or right arrow key, which moves
it by 16 pixels. The two splitters are named "Resize the explorer" and "Resize the chat" for screen readers.

The button at the right end of the conversation header hides the whole conversation to give the stage the room. A button
at the right edge of the stage brings it back. CanvasPilot does not remember that the conversation was hidden.

# Projects

A project is a folder. It contains a project file named `{name}.canvaspilot` and the image source files of your
drawing. Keep the files you want to preview in the project folder, and use sub-folders to arrange them, together with
supporting files such as data or embedded models; the explorer shows the folders as a tree. CanvasPilot also uses a
hidden `.canvaspilot` folder inside the project for the pictures that you paste into a message while you are still
writing it, and an `attachments` folder for the files that you send with a message (see *Chatting*). The explorer never
lists the hidden folder, and shows `attachments` like any other folder.

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
file no longer exists is removed from the list after a message. The welcome screen lists them too.

## The Welcome Screen

While no project is open, the window shows the welcome screen instead of the three panes. At the left it has the name
and the tagline, the buttons **New project** and **Open project** (the same as **File > New Project...** and **File >
Open Project...**, with Ctrl+N and Ctrl+O), and a short **How it works** in three steps: *Describe* what you need by
typing or by holding the talk key (F9 unless you chose another) and speaking, *Mark* the picture to show what to change,
and *Share* the result as a PNG or by copying it into email or a slide.

![The welcome screen of CanvasPilot in the dark theme. At the left are the name, the tagline "Describe a picture. Your
copilot draws it.", the buttons New project and Open project with their keys Ctrl+N and Ctrl+O, and the three steps
Describe, Mark, and Share. At the right the heading Recent projects stands over four cards, each with a picture of the
project, its name and folder, when it was opened, and how many pictures it holds. The title bar shows the AI provider
pill, and the status bar says "No project open".](welcome.png)

At the right the welcome screen shows up to six **recent projects** as cards, most recent first. A card has a small
picture of the main drawing of the project (the picture at the top of the project folder that was changed last, which is
usually the one that puts the others together), the name and folder of the project, when you opened it, and how many
pictures it holds. Click a card, or move to it with the arrow keys and press Enter, to open the project. To take a
project off the list, focus its card and press Delete, or right-click it and choose **Remove from list**; the project
itself is not touched. With no recent project the screen shows a card that invites you to make one.

When the AI provider needs something, an amber notice with the button **Open Settings** appears under the steps, and
it goes away when the provider is ready (see *Setting Up an AI Provider*).

## Save and Close a Project

**File > Save Project** (Ctrl+S) writes the project file. The image source files are ordinary files that you edit with
the tool of your choice, so saving updates only the project file. **File > Close Project** closes the project and
clears the explorer and the stage and shows the welcome screen. **File > Exit** closes CanvasPilot.

The title bar shows the name of the open project followed by CanvasPilot. The project file is written when you choose
Save. While a project is open, the status bar shows when the project file was last saved ("Project file saved" and the
time) once it has been saved; it does not check whether there are unsaved changes, and it says nothing about saving for
a project that has never been saved.

# Working with Images

## The Project Explorer

When a project is open, the left pane, headed **EXPLORER**, shows the project as a tree. It lists the folders of the
project and, in them, the image source files that CanvasPilot can draw, which include SVG drawings, Mermaid diagrams
(`.mmd`), Vega-Lite charts (`.vl.json` and `.vl`), OpenSCAD models (`.scad`), pages of PDF and PowerPoint documents, and
the other formats that the bundled CanvasNet library draws, and also the pictures, PDF files, and style files that those
sources use. Folders come first, then files, each in alphabetical order. A small badge in front of a name shows the kind
of file, and the name always carries its extension, so the badge never has to be told apart by color. Hidden files and
folders, the hidden `.canvaspilot` folder, and the file types that CanvasPilot cannot draw are not listed. A very large
project is cut off at 5,000 entries and 12 levels of folders.

Folders open and close with the arrow in front of them, or with the right and left arrow keys. A small picture of the
drawing, a **thumbnail**, is shown at the right of each file that can be drawn. Thumbnails are made in the background
for the rows that are in view, and made again when the file changes. You can turn them off on the Appearance page of the
settings (see *Appearance Page*).

Select a file to preview it. The row of the selected file has a tint and a bar at its left, and its name turns bold. A
folder can be selected as well, which empties the preview. Use the up and down arrow keys to move through the rows, and
the right and left arrow keys to open and close folders.

The tree follows the folder: files that you add, change, rename, or delete with other tools appear in it after a moment.
Right-click a file, or select it and press F2 or Delete, to **Rename...** or **Delete...** it. Deleting asks you to
confirm first. Renaming and deleting work on pictures; folders cannot be renamed or deleted in CanvasPilot.

The header of the explorer has three icon buttons, each with a tooltip:

- **New file** asks for a name that ends in a type that CanvasPilot can draw, makes the file, and selects it. A new SVG,
  Mermaid, or Vega-Lite file holds a small blank drawing, and any other new file is empty. The file goes into the folder
  that is selected, into the folder of the file that is selected, or into the project folder.
- **New folder** asks for a name and makes the folder in the same place. A name cannot start with a period or hold the
  characters that Windows does not allow.
- **Refresh** reads the folder again at once.

At the foot of the explorer a dashed box says **Drop pictures or PDFs here**. Drag pictures, PDF files, or drawing files
from Windows Explorer, a web browser, or another program onto it, and CanvasPilot copies them into the `attachments`
folder of the project. Drop them on a folder row instead and they are copied into that folder, and drop them on a file
row and they are copied into the folder of that file. The row you are over gets a dashed outline. A name that is already
taken gets `(2)` and so on added, nothing is overwritten, and your originals are not changed. Text and other kinds of
file are refused, and a message says so. To attach a file to a message instead, drop it in the message box (see
*Attaching Files, Folders, and Images*).

If the folder cannot be read, the explorer shows the problem in red with an icon. CanvasPilot remembers which folders
you opened only until you close the project.

## The Stage

Select a file to see its picture on the stage, the dark area in the middle. The picture sits in the middle of the stage
on a soft shadow, over a faint grid of dots, with the chrome kept to a minimum. CanvasPilot draws the file in the
background, so the window stays responsive while a large file is drawn, and a thin moving line along the top of the
stage and the word "Rendering..." in the status bar show while it runs. When you select another file, or the file
changes, the picture is drawn again and a result that is no longer wanted is discarded. The picture is also redrawn when
a file that it uses changes. The status bar tells the file, size, format, and drawing time of what you see.

A file that references other files, such as an SVG that embeds a picture, can read them only from inside the project
folder. A file outside it is left out of the picture and reported as a note.

### Zoom

The **zoom pill**, at the bottom right of the stage, has these controls, from the left:

- **Fit to window** (a toggle) scales the picture to the stage, so that all of it is visible. It is on when you open a
  picture, and while it is on the button has a tinted background, an outline, and a colored icon.
- **1:1** shows the picture at its own size.
- **Zoom out** (the minus) and **Zoom in** (the plus) step through 25, 50, 75, 100, 150, 200, 300, and 400 percent. From
  the fitted size they go to the next step below or above it.
- The **percentage** shows the zoom now. Choose it to fit the picture again.

A picture that is larger than the stage gets scroll bars, and the mouse wheel scrolls it. You can also drag it with the
middle mouse button held down. The same commands have keys while the stage has the focus:

| Command | Keys |
| --- | --- |
| Zoom in | Ctrl+plus, or Ctrl+mouse wheel forward |
| Zoom out | Ctrl+minus, or Ctrl+mouse wheel backward |
| Fit to window | Ctrl+0 |
| Actual size (1:1) | Ctrl+1 |

Marks are drawn in the pixels of the picture, so they stay on the same place at every zoom.

### The Marking Tools

The floating bar at the top of the stage holds the tools that mark the picture (see *Marking the Picture*). Its buttons
show icons, and each has a tooltip with its name and key.

## Notes and Errors

Notes that the drawing reported, for example that a feature is not supported or that an embedded file was denied, are
counted in the status bar, which turns that count amber. A drawer in the lower left corner of the stage lists them with
their numbers; open it with its warning header, and the picture is still shown when there are notes.

When a file cannot be drawn, a card in the middle of the stage shows an error with a short code, a message, and, where
one exists, a hint about what to fix, and the status bar says "Render failed". An unfinished file that you are editing
often produces such an error; correct the file and the stage updates.

## Export and Copy

With a picture shown, the **Image** menu offers:

- **Export as PNG...** asks first how the picture is exported (see *Exporting at a Size* in *Style Kits*). *As rendered*
  is the first choice and is selected, so pressing **Export...** at once saves the picture, with its transparency, as a
  PNG file at the size it is shown. Then it asks where to save the file.
- **Copy Image** puts the picture on the clipboard, so you can paste it into email, a presentation, or another
  program.
- **Check Against Style** compares the picture with the colors of the style kit of the project (see *Checking a
  Picture Against the Style* in *Style Kits*).

The three items are disabled while no picture is shown.

The two most common actions are also on the picture itself. Right-click the picture, or the stage around it, to open a
menu with:

- **Copy image**, which does the same as **Image > Copy Image**;
- **Export as PNG...**, which does the same as **Image > Export as PNG...**.

For example, right-click the picture, choose **Copy image**, and paste it into an Outlook message or a PowerPoint slide.
From the keyboard, give the stage the focus (press Tab until it is focused, or click the empty stage) and press
**Shift+F10** or the **Menu** key; the menu opens at the bottom of the stage. Screen readers read the items as "Copy
image" and "Export as PNG". Like the Image menu, both items are disabled while no picture is shown, also when the file
could not be drawn. Then the stage cannot take the keyboard focus either, so the menu does not open from the keyboard;
there is nothing to copy or export then, and a right-click still opens it with both items disabled. The menu does not
open while a marking tool is chosen, so a right-click never interrupts a mark; choose **Select** (or press V) first.

## Drag the Picture Into Another Program

You can also drag the picture straight out of the stage. Press the left mouse button on the picture, move the pointer
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

The conversation pane is where you talk with the AI copilot about your drawing. A message can hold text that you type or
dictate, files, folders, and pictures that you attach, and numbered marks that point at places on the picture. When you
send the message, the copilot answers it (see *Working with the copilot*), and you can see exactly what the message
contains. Until you have set up an AI provider that works (see *Setting Up an AI Provider*), the panel says so in a
banner, and a sent message stays in the panel with an explanation.

## The Conversation Pane

The conversation is the column at the right of the window, beside the stage. It is shown while a project is open and
hidden when there is none. Drag the splitter between the stage and the conversation to change its width, or hide it with
the button at the right end of the header (see *Window Size and Layout*); it starts at 400 pixels. From top to bottom it
has:

- the **header**, with the title *Conversation*, the **More conversation actions** button (three dots), which opens a
  menu with **Clear chat history** (see *Chat History*), and the **Hide the conversation** button.
- a **banner**, when the AI provider that you chose cannot be used or can be used only with a warning. It says what is
  wrong and what to do, with the buttons **Open Settings** and **Check again**. The banner is hidden when everything is
  fine.
- the **transcript**, the messages that you sent in this project and the copilot's answers, oldest first (see *The
  Transcript*). While it is empty it asks "What would you like to draw?" and offers three example prompts.
- up to four **suggested prompts**, small buttons just above the message box, while there is a conversation and the
  copilot is idle (see *The Example Prompts and the Suggestions*).
- the **message box**, a rounded card in which you write the next message (see *Writing a Message*). Without an open
  project it cannot be edited. A status line below it says what is going on, for example what was left out of a drop.
- under the message box, the **Attach files** button, the **Hold to talk** microphone button with the key cap of the
  talk key (F9 unless you chose another), the hint "Enter to send", and the **Send** button (an arrow). While the
  copilot works, three pulsing dots with what the copilot is doing and how long the turn has lasted (for example
  "Waiting for the model... 1:25") and a **Stop** button appear above the message box, and Send waits (see *Following a
  Turn*).

The transcript of a project is kept as text on your computer, so it comes back when you open the project again; see
*Chat History* for what is kept and where.

### The Transcript

The transcript reads like a conversation. Your messages are bubbles at the right, with the time under them, and the
chips of your marks and attachments inside: the chips are in rows, and your words are in the lines below or between
them. Pause the pointer on a message, or move the focus to it, to show **View payload** under it (see *Sending a Message
and Viewing the Payload*). The copilot's answers are plain text under its name and a round mark, without a border; the
answer appears as it is written, as plain text first and as formatted text once it is whole, and a label such as
*Stopped* or *Failed* shows when an answer did not end. For each tool that the copilot uses, a small rounded **chip**
appears under its name, with a spinner while it works, then a green check, and the time it took when that was a tenth of
a second or more, for example "Rendered overview.svg · 0.8 s". A tool that did not work shows a red cross and the
reason. A notice in the middle says what went wrong, with **Open Settings** when the settings can fix it. The messages
from an earlier session are shown a little paler under a divider that says "Earlier".

### The Example Prompts and the Suggestions

An empty conversation shows three example prompts for starting a drawing (*Draw a flowchart of our release process*,
*Make a title card for a launch announcement*, and *Chart something from a small table of numbers*) and a tip, "Drop a
picture, or hold F9 and speak", which names the talk key that is in effect. Choose one to write it into the message box
at the caret, ready for you to change and send.

Once there is a conversation and the copilot is idle, the suggested prompts above the message box follow what the
project has:

- While the project has **no drawing** (no drawing source in the project folder, not counting the `attachments` and
  `styles` folders and the hidden folders, and none that the copilot has written), they are the same three ways to
  start. "Add a legend" would mean nothing there.
- Once the project has a **drawing**, because a picture is shown, the copilot wrote one, or the folder already holds
  one, they are ways to change it: *Make the title larger*, *Add a legend*, and *Use my style kit*, and *Export as PNG*
  too while a picture is shown.
- While the copilot **works**, none are offered.

The suggestions are judged again when you open another project, when a picture is shown or goes away, and when a turn
ends. Nothing is sent until you send it.

## Writing a Message

Click in the message box and type. The box says "Describe a change, or mark the picture..." while it is empty. **Enter**
sends the message, and **Shift+Enter** starts a new line; the **Send** button, an arrow at the right below the box, does
the same as Enter. Send is dimmed while the message is empty. Select, copy, cut, and paste work as in any text box, and
a long message wraps and grows to a limit, after which the box scrolls. Text that you paste is plain text, and
characters that cannot be shown are removed. The box has a glow in the focus color while you are writing in it.

CanvasPilot remembers what you did in the message that you are writing, so that you can step back and forth. Undo and
redo apply only to that message (the text, the attachments, and the marks in it), never to the files of your project:
they cannot take back what the copilot wrote. Undoing a copilot turn, which restores the files that it changed and tells
the copilot, is planned and is not available yet. On macOS, use the Command key where this guide says Ctrl:

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
2. Press and hold the **Hold to talk** microphone button, or hold the **talk key** while the CanvasPilot window is
   active. The talk key is **F9** until you choose another (see *Choosing the Talk Key*). The status bar and the key cap
   beside the button remind you of the key.
3. Speak. The button turns red and pulses while you hold it, its name changes to "Starting... keep holding" and then
   "Listening... let go to finish", and the words that CanvasPilot has recognized so far appear above the message box
   while you hold.
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

### Choosing the Talk Key

The key that you hold to talk is **F9** by default, and you can choose another on the Speech page of the settings (see
*Speech*). A single key or a chord of Ctrl, Alt, or Shift with a key works, for example `F9` or `Ctrl+Shift+Space`. The
status bar, the welcome screen, the tip of an empty conversation, and the key cap of the microphone button always name
the key that is in effect. Letting go of any key of a chord ends the dictation.

The default used to be F8. It was changed because on many laptops F8 is a brightness, media, or screen-capture key
that the keyboard or its driver takes before the window sees it, so holding it did nothing. No key is free on every
computer, so CanvasPilot refuses the keys that would take something away from you or that the system owns, and says why
beside the box. It refuses:

- a modifier alone (Ctrl, Alt, Shift, or the Windows key) and the keys of editing and moving (Esc, Enter, Tab,
  Backspace, Delete, Insert, Home, End, Page Up, Page Down, the arrow keys, and the like);
- a letter, a digit, or the space bar without Ctrl or Alt, because it would type;
- **Ctrl+Space**, which belongs to input methods and to tools such as PowerToys; **Alt+Space**, which opens the system
  menu; Alt with a letter or a digit alone, which opens a menu; and **Ctrl+Alt**, which is AltGr on many keyboards;
- every chord with the **Windows key**, which the system and tools such as the Xbox Game Bar use for their own
  shortcuts;
- the shortcuts of editing and of CanvasPilot (Ctrl+A, C, V, X, Y, Z, N, O, S, P, F, W, and Q) and Alt+F4; and
- the function keys **F1** (help), **F2** (which changes the note of a mark in the message and renames a file in the
  explorer), **F8** (the old default), **F10** (menu), **F11** (full screen in many programs), and **F12** (the
  debugger on many computers).

The other function keys, F3 to F7, F9, and F13 to F24, work alone or with Ctrl, Alt, or Shift. A talk key that is
refused but was saved by an earlier version, such as F8 or F2, is not used: CanvasPilot uses F9 instead until you
choose another key. CanvasPilot cannot know
which other programs on your computer have registered a global shortcut, so if the key does nothing, choose another
(see *Troubleshooting*).

## Attaching Files, Folders, and Images

You can add things to a message by dropping them into the message box (for example from Windows Explorer or a web
browser), by choosing the **Attach files** paperclip below the message box and picking files, or by pasting them with
Ctrl+V, including a screenshot that you copied. Each file, folder, or picture becomes a small **chip**. The chips of the
files that you pick with the button, like the chips of your marks, go to a row of their own at the top of the message
box, above your words, and the row wraps when there are many; a chip that you drop at a place in the text stays in the
text, at the place where you dropped it, and a chip that you paste is put at the caret. A picture shows a small preview.
Delete a chip with its cross, which is named "Remove" and the label of the chip, or put the caret next to it and press
Backspace or Delete; Ctrl+Z brings it back. Text that you drop or paste becomes ordinary text. Dropping on the message
box is different from dropping on the explorer, which adds the files to the project without a message (see *The Project
Explorer*).

What you attach is only ever treated as data. CanvasPilot looks at what a path is and how large a file is, but it never
opens or runs a file, so even a program or a script is simply attached like any other file. Paths that are not safe to
refer to, such as device names, are left out, and the status line says which and why.

Nothing is copied while you are writing. When you **send** the message:

- a file or folder from outside the project is copied into the `attachments` folder of the project, with `(2)` and so on
  added to a name that is already taken, and nothing is ever overwritten. Your original is not changed or deleted.
- a file or folder that is already inside the project is referred to where it is.
- a pasted screenshot, which CanvasPilot saved in the hidden `.canvaspilot\attachments` folder while you were writing,
  is moved into `attachments`. Screenshots that no chip refers to any more are deleted.

The `attachments` folder shows in the explorer like any other folder, with the files you sent. There are
limits: one drop accepts at most 200 items, one message copies at most 500 files, 500 folders, and 200 MB, and a pasted
picture can be at most 50 MB. A message that goes over a limit is not sent, and the status line names the item that
caused it.

## Marking the Picture

Marks let you point at the place that you mean. With a picture shown, the floating bar at the top of the stage has five
tool buttons and a button that clears the marks. Each shows an icon and has a tooltip with its name and key:

- **Select** (V) is no marking tool; the picture can be dragged out, and a double-click on a mark changes its note.
- **Rectangle** (R), **Ellipse** (E), **Freehand** (F), and **Arrow** (A) draw that shape.
- **Clear marks** takes every mark, and its chip, out of the message. It is available while there is a mark.

The tool that is chosen has a filled button, and the tool keys work while the focus is on the stage and not in a text
box. Choose a tool and drag on the picture. A click without a drag makes no mark, and Escape throws a shape away. Marks
are drawn in the pixels of the picture, so they stay on the right place however you zoom. With the keyboard, give the
picture layer the focus, move the cursor with the arrow keys, and press Enter or Space to start and to finish a shape.

When you finish a shape, a small card with an optional **Note** box appears under the bar: type a short note (up to 200
characters) and choose **Add mark**, or choose **Skip note**. The mark gets the next number, 1, 2, 3, and so on in the
order you make them, and is drawn with its number. At the same time a chip, for example "Mark 1", is added to the row of
chips at the top of the message box, above your words, so you can write "the arrow in Mark 1 should point to the other
box" and the reader knows which mark you mean. The mark is part of the message exactly as long as its chip is in it:
deleting the chip hides the mark, and undoing the deletion shows it again. Marks are never made by voice.

A mark's note shows beside its number on the picture, on one line that ends with "…" when the note is long. Rest the
pointer on the mark, its number, its note, or its chip in the message to see the whole note; a screen reader reads the
note with the chip as well. To change a note before
you send the message, double-click the mark (with **Select** chosen) or its chip, choose **Edit note…** from the chip's
right-click menu, or press F2 with the cursor right next to the chip in the message. Changing a note from its chip needs
a picture to be shown, because the note card opens over the picture; when none is shown, or while a shape is being
drawn, the conversation says so and what to do. The note card opens with the note:
change it and press Enter or choose **Save note**, or press Escape or choose **Cancel** to keep the note as it was; the
mark stays either way. Ctrl+Z in the message does not undo a changed note. A message that was sent cannot be changed,
so neither can the notes of its marks.

A mark belongs to the exact picture that it was drawn on. When you change the file and the picture is drawn again, the
marks are not drawn on the new picture, because they might no longer point at the right place, but they stay in the
message and the marking bar says how many belong to an earlier picture. The message that you send holds the earlier
picture that the marks were drawn on.

While a marking tool is chosen, the picture cannot be dragged out, and right-clicking it opens no menu. Choose
**Select** to drag it again or to use its menu.

## Sending a Message and Viewing the Payload

Press **Enter** or choose **Send** when the message is ready. Send works while the message has text or an attachment and
a project is open, and while the copilot is not still working on an earlier message. CanvasPilot copies the attachments
into the project, builds the payload of the message, adds the message to the transcript with its words and chips in the
order that you wrote them, empties the message box and the marks, and gives the message to the copilot. If something
goes wrong, the message stays as it was and the status line says what to fix.

Under each of your messages in the transcript, **View payload** shows when the pointer is over the message or the focus
is in it.
The **payload** is everything that the copilot receives for the message. To see it, choose **View payload** on a
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

You can still drag the shown picture out of the stage into another program while you are writing a message, as
described in *Drag the Picture Into Another Program*, as long as **Select** is chosen. The dragged picture is the
picture as drawn, **without the marks**; the picture with the marks drawn on it is only in the payload.

This has not been verified against specific programs: the behavior has been tested with the application's automated
tests, but not by dropping the picture into Windows Explorer, PowerPoint, Outlook, or Paint on a real machine. If a
program does not accept the picture, use **Image > Export as PNG...** or **Image > Copy Image**, and please report which
program it was.

# Setting Up an AI Provider

The copilot needs an AI provider. CanvasPilot supports two: a local **Ollama** server, which is covered by automated
tests (live checks against a running server are pending), and **GitHub Copilot**, which runs through the GitHub Copilot
CLI that you installed. Choose one in **Tools > Settings... > AI Provider** (see *Settings*), then choose **Check
connection**. The same check runs in the background when the conversation pane is shown, and the conversation pane shows
its result in the banner, so you see a problem before you send a message.

## Setting Up Ollama

Ollama runs language models on your own computer or on a server that you run. Nothing needs a GitHub account.

1. Install [Ollama](https://ollama.com) and start it.
2. Pull a model that can call tools. For the copilot to see the marks and pictures of your messages, choose a
   **vision-capable** model; with a model that cannot see pictures the copilot works only from your words and the files
   (for example `ollama pull gemma3`).
3. In the AI Provider page choose **Ollama** and keep the **Ollama endpoint** (default `http://localhost:11434`) or
   enter the address of your server. The page then lists the models that the server has; choose yours (see *Choosing a
   Model*). If the list cannot be had, type the exact **Model name** (as `ollama list` shows it). A model is required
   for Ollama.
4. Keep the **Context window** at 32,768 tokens or more. The default for new settings is 32,768. Drawings with pictures
   and tools use a lot of context, and CanvasPilot warns when the window is below 16,384 tokens, or when the model
   itself supports fewer. A window that is larger than the model was trained for is refused (see below). A value that
   an earlier version stored is kept as it is, so check it.
5. Choose **Check connection**.

The check says what it found, in this order:

- **The address is not valid**: "The Ollama address in Settings is not a valid http or https address". Fix the address.
- **No model name**: "Enter the name of an Ollama model in Settings". Choose or type one.
- **Ollama does not answer**: "Ollama did not answer at {address}; start Ollama or fix the address in Settings". Start
  the server or correct the address, then check again.
- **The model is missing**: "Ollama does not have the model {name}; run `ollama pull {name}`". Run that command.
- **The model cannot be used**: the explanation of the AI library, for example "Model 'x' does not support tool
  calling, which this application requires." (the copilot draws by calling tools, so choose another model), or "Model
  'x' supports at most 8,192 tokens; a 32,768-token window is not supported." (lower the **Context window** or choose a
  model with a longer one), or that the model does not offer the reasoning level that you chose.
- **The context window is small**: a warning, not an error, that names the size and recommends 32,768. The copilot
  still works, but long or picture-heavy requests may fail.
- **The model cannot see pictures**: a warning, not an error: "The model {name} does not accept pictures, so it cannot
  see the drawings and marks; choose a model with vision for the best results." The copilot works from your words and
  the files.
- **Ready**: "Ollama is running with the model {name}." When the server could not describe its models, the sentence adds
  that pictures work only with a vision-capable model.

*Not yet checked:* the automated tests of CanvasPilot use a stand-in for the Ollama server. A run with a real server and
real models has to be done by a person before a release (see the verification of the system).

## Setting Up GitHub Copilot

**GitHub Copilot runs through the GitHub Copilot command-line tool (the Copilot CLI) that you installed.** CanvasPilot
never ships or downloads that tool. It needs version 1.0.113 or later, which CanvasPilot finds automatically (for
example a `winget install GitHub.Copilot` installation) or which you choose in Settings. CanvasPilot starts one Copilot
runtime when it is first needed, and the **Check connection** button and the chat share it.

**Signing in.** CanvasPilot has no token field and no sign-in screen. The Copilot runtime signs in by itself, from the
first of these that works, in this order:

1. a token in the environment that CanvasPilot is started in: `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`, or `GITHUB_TOKEN`;
2. the sign-in of the Copilot CLI itself: run `copilot` in a terminal and type `/login`, then follow the steps. This is
   the usual way, and it needs no `gh` and no variable;
3. the GitHub CLI signed in with `gh auth login` (`gh` on the `PATH`).

Whichever is used, the account must have GitHub Copilot. If your account is on a GitHub Enterprise Cloud host, sign in
to that host with the Copilot CLI (`copilot login --host https://{name}.ghe.com`); CanvasPilot uses the host of the
account that signed in and has no host setting. If you change the account in the Copilot CLI, or set or clear a token
variable, close and start CanvasPilot again, because the Copilot runtime keeps the account that it started with. A token
variable that holds a token that your host does not accept stops the search, so the sign-in of the Copilot CLI is not
tried; clear the variable and start CanvasPilot again.

When you choose **GitHub Copilot** and **Check connection**, CanvasPilot checks four things in order and shows the first
that fails:

1. **The Copilot CLI is found.** CanvasPilot looks for the real `copilot.exe` (see *Which Copilot CLI Is Used*). If it
   cannot find one: "The GitHub Copilot CLI was not found. Install it with `winget install GitHub.Copilot` or set its
   path in Settings." When only the launcher of an editor was found, the message says so.
2. **Its version is 1.0.113 or later.** CanvasPilot reads the version with `copilot --no-auto-update --version`, which
   is the version that is built into the installed program. Otherwise: "Copilot CLI {version} is older than 1.0.113.
   Run `winget upgrade GitHub.Copilot`."
3. **The runtime starts on that CLI and signs in.** The start of the runtime decides the sign-in, so there is no
   separate check of credentials; the first start can take a few seconds longer than before. Then the check says what it
   found, for example "GitHub Copilot CLI 1.0.114 is ready. Signed in as octocat on github.com (Copilot CLI sign-in)."
   (with the version that was found, the account, the host, and how it signed in: "Copilot CLI sign-in", "token
   variable", or "GitHub CLI sign-in"; never the token itself). Otherwise it shows one of these:
   - The CLI is too old: the message says to upgrade the Copilot CLI itself and lists the commands. Running `copilot
     update` alone may not change the version that runs, so check the version again afterward.
   - Not signed in: "Copilot is not signed in for CanvasPilot. Run `copilot`, then type `/login` to sign in (or sign in
     with `gh auth login`, or set the COPILOT_GITHUB_TOKEN, GH_TOKEN, or GITHUB_TOKEN environment variable), then
     choose Check connection." After it, "What was found:" lists what CanvasPilot's AI library looked at for each way of
     signing in, so you can see why none worked. After you sign in, choose **Check connection** again; you do not need
     to restart CanvasPilot to fix a missing sign-in.
   - The runtime could not be started: "The Copilot runtime could not be started. Choose Check connection to try
     again." or the explanation of the AI library.
   - The path in Settings cannot be used: "The Copilot CLI path in Settings cannot be used: ..." followed by the reason,
     for example a relative path or a `.cmd` or `.bat` file.
4. **The model and the reasoning level are valid.** When you chose a model, the runtime says whether your account has it
   and what it can do. A model that is not offered to you, that is switched off for your account, that cannot call
   tools, or that does not offer the reasoning level that you chose is refused with the explanation of the AI library,
   for example "Model 'x' does not support reasoning effort 'bogus'. Supported: low, medium, high." (see *Choosing a
   Model*). A model that cannot see pictures is a warning and the copilot still works. With no model chosen the check
   ends with "GitHub Copilot CLI {version} is ready." and the account sentence; with one it ends with "... is ready with
   the model {id}." and the account sentence.

CanvasPilot never stores, shows, or logs a token. It does not run the GitHub CLI itself, and there is no field for a
token in the settings.

The model of the AI Provider page is chosen from the list of the models of your account (see *Choosing a Model*). Leave
it empty to use the default model of the Copilot runtime. The runtime keeps its own data, such as its sessions, in
`%LOCALAPPDATA%\CanvasPilot\copilot`, outside every project.

## Choosing a Model

On the AI Provider page, the **Model** list shows the models that the provider offers: for GitHub Copilot the models of
your account, which come from the running Copilot runtime, and for Ollama the models that your server has pulled, which
come from the server at the **Ollama endpoint**. The list loads when you open the page, when you choose another
provider, when you change the endpoint or the Copilot path (after you stop typing), and when you choose **Refresh
models**, which asks the provider again (for example after you pulled a model or were given access to one). It is always
the list of the provider you chose; if you choose the other provider, the model and the reasoning level start empty
again.

Do not change the Copilot CLI path in the settings while the copilot is answering. Listing the models, **Refresh
models**, and **Check connection** with another path restart the Copilot runtime, which ends the answer that is
running; send the message again. If you cancel the dialog, the next message uses the saved path.

- **Every model is listed, with what it lacks.** An entry says in a few words when a model has no tool calling, no
  picture input, or is unavailable (for example "gpt-x - no pictures"). A model that the provider says cannot be used
  (switched off for your account, or an embedding model) is shown dimmed and cannot be chosen; the warning under the
  list says why.
- **What the chosen model can do.** Under the list you see, for the chosen model: **Tool calling**, **Pictures**,
  **Reasoning levels**, **Context sizes** when there are several, and the largest **Context window**. The copilot needs
  tool calling for every drawing, so a model without it is warned about, and the check refuses it. Pictures are wanted
  but not required: without them the copilot cannot see your marks or its own drawings, and works from your words and
  the files.
- **The default model.** The first entry, "Provider default model", leaves the choice to the provider (GitHub Copilot
  uses the default model of its runtime). Ollama needs a model.
- **Typing a model.** The **Model name** box below the list always works. Type the id of a model that is not in the list
  (for example one that you pulled a moment ago), or any model while no list is shown. A name that is not listed is
  tried as you typed it, and the first message shows whether it works. Choosing an entry of the list fills the box, and
  typing a name that is in the list chooses it (a name without a tag, such as `gemma3`, matches `gemma3:latest`).
- **When the list cannot be had.** The page says why next to **Refresh models**, for example that Ollama did not answer
  or that Copilot is not signed in, and the box still takes your typed name. What you saved earlier (the model and the
  reasoning level) is kept, so a server that is down for a minute does not erase a setting.

### The Reasoning Level

**Reasoning level** is a choice among the levels that the chosen model offers: the names are the provider's own, for
example `none`, `low`, `medium`, `high`, `xhigh`, or `max`, and they differ from model to model. The box is enabled only
while the chosen model offers levels; otherwise it stays on "Provider default" and a note says why (no model chosen, the
model has no levels, or the levels of the model are not known because no list could be had). "Provider default" leaves
the level to the provider. A higher level lets the model think longer before it answers, which may help with a
complicated drawing and costs time.

When GitHub Copilot is the provider, a **large model** (one that counts three or more premium requests for a message, or
that the provider lists as powerful or high-priced) is chosen, and the level is `high`, `xhigh`, or `max` (or the
provider chooses one of them), the page shows a warning under the level: the choice is slow, a big drawing can take
minutes, each message counts that many premium requests, and stopping a turn does not give the request back. Choose a
lower level for everyday work. Nothing is shown for other models or levels, so that the warning keeps its meaning.

If you choose another model and the level that you had is not offered by it, the level goes back to "Provider default"
and the page says so. The level you choose is saved with the other settings and takes effect with the next message,
which starts the conversation again, as a change of the model does.

### The Context Size

Some GitHub Copilot models can run with more than one **context size**, which is how much of the conversation (your
messages, the pictures, the files that the copilot read, and its own answers) the model can hold at once. For example
Claude Opus 5.5 offers a standard size (the list shows "Standard (328K tokens)") and a long one ("Long context (1M
tokens)", one million tokens). When you choose such a model, a **Context size** box appears under the reasoning level,
with the sizes that the model offers and their windows as GitHub Copilot lists them. Choose **Long context** to give the
model the one-million-token window. The box is not shown for a model that has only one size, for a model that is typed
but not in the list (the page cannot know its sizes; a size that you saved is kept), or for Ollama (whose window is the
**Context window** entry).

A larger size is **billed and served separately** by GitHub Copilot, so a message can cost more premium requests and
take longer, and a long conversation can use much more of it; the page says so while a size other than the standard one
is chosen. CanvasPilot cannot tell you the price, so look at the line under each answer (see *What a Turn Costs*), which
names the size, for example "..., model x15 premium, long context, 3:16". The tooltip of the provider pill in the title
bar names the model and the size that are in use ("... ready with the model claude-opus-5.5 at the Long context size
(1,000,000 tokens)"). If you choose another model and the size that you had is not offered by it, the size goes back to
the standard one and the page says so. The size is saved with the other settings and takes effect with the next message,
which starts the conversation again, as a change of the model does.

### Models That Cannot Work

The banner of the conversation pane, and **Check connection**, tell you before the first message when the choice cannot
work: a model that cannot call tools, that is not offered to your account, or that does not offer the chosen level is
refused with the explanation of the AI library, which names what is supported, and the copilot does not start. A model
that cannot see pictures only warns. Choose another model or level in Settings, then **Check connection**.

### Which Copilot CLI Is Used

The tool that is found must be the real Copilot CLI, not a launcher. Editors such as Visual Studio Code put a small
`copilot.exe` on the `PATH` that only forwards to the real tool, and package managers add `.cmd` or script launchers.
CanvasPilot does not take any of them. It searches, in this order:

1. the winget package folder `%LOCALAPPDATA%\Microsoft\WinGet\Packages\GitHub.Copilot_*`,
2. the winget links folder `%LOCALAPPDATA%\Microsoft\WinGet\Links`, and
3. every folder of the `PATH`,

and it takes the first `copilot.exe` that is not a launcher: a file that is not an `.exe`, that lies in the folder of an
editor's shim (for example `copilot-shim`, `.vscode`, or `Microsoft VS Code`), or that is far smaller than the real tool
is skipped. Then it runs the file with `--no-auto-update --version` and checks the version.

If you keep the tool somewhere else, enter its full path in **GitHub Copilot command-line tool path**. A path that you
enter wins over the search and is the only candidate: it must be an existing `.exe`, CanvasPilot checks its version, and
if it fails the message says why and CanvasPilot does not fall back to the search. Leave the field empty to search.

*Verified:* short real turns with Copilot CLI 1.0.114 listed the project files and rendered a picture, with the runtime
signed in through the GitHub CLI and through the sign-in of the Copilot CLI (`/login`, on a GitHub Enterprise Cloud
host, with no GitHub CLI and no token variable). Other setups, such as a token variable and other versions, have not
been checked on a real machine.

## Check Connection and the Banner

**Check connection** is on the AI Provider page. It checks the entries as they are in the dialog, before you choose OK
or Apply, and it always looks again: it does not reuse the earlier search for the Copilot CLI. The button is unavailable
while an entry is invalid (the page marks it in red) or a check is running, and the result is removed when you change an
entry. For GitHub Copilot that can be used, the result also says which account the copilot uses, for example "GitHub
Copilot CLI 1.0.114 is ready. Signed in as octocat on github.com (Copilot CLI sign-in)." The pill in the title bar shows
the same sentence when you pause the pointer on it. If that is not the account that you expect, see the topics about
signing in in *Troubleshooting*. The conversation pane shows the result of the check for the saved settings in its
**banner**, with **Open
Settings** and **Check again**. After you change the provider settings, the banner checks again by itself.

# Working with the Copilot

This chapter describes what happens when you send a message and the AI provider works. It applies to both providers.

## Asking

Describe the picture that you want in the message box, or what to change in a picture that you have. Say what the
picture is for and what it should contain, and name the file if you mean a file that exists. Then choose **Send**. The
copilot is told how to draw with each format (the drawing knowledge), which style your project uses, and which files the
project has, so you do not have to explain those.

While the copilot works, the panel shows three pulsing dots with what the copilot is doing and how long the turn has
lasted, and a **Stop** button (see *Following a Turn*). Its answer appears as it is written, as plain text first and as
formatted text when the answer is whole. For each tool
that the copilot uses, one chip appears in the transcript, for example "Writing logo.svg" and "Rendering logo.svg" with
a spinner, and when it is done the same chip in the past tense with a green check ("Wrote logo.svg", "Rendered logo.svg
· 0.8 s"). You can read your own message and the payload at any time; sending another message waits until the copilot
has finished or you press Stop.

A good loop is: ask for a first draft, look at the stage, mark what is wrong (see below), and ask for the change.

## Following a Turn

A turn can take minutes: a large drawing is written in one piece, and a large model with a high reasoning level thinks
for a long time before it writes. So that the window never looks dead, the busy row above the message box says two
things, and only what is true:

- **What the copilot is doing**: "The copilot is working..." at the start, "Thinking...", "Waiting for the model..."
  while a model call runs and no tool does, "Writing the answer...", and, when the provider reports it, "The model is
  writing..." and the file that is being written with the size so far, for example "Writing `cover.svg`... 9.8 KB",
  which is shown on the chip of the tool and then becomes "Wrote `cover.svg`" when the tool is done.
- **How long the turn has lasted**, as minutes and seconds ("1:25"), counted every second while the turn runs. This is
  the one thing that is true for every model, so it is always shown.

When nothing has been heard from the copilot for 30 seconds and no tool is running, a hint appears: "Large drawings can
take a few minutes, and some models show nothing until the drawing is complete. Stop cancels the turn, but the provider
may still count the request."

**Why a model can seem silent.** Some models, in particular the Claude models on the Copilot runtime that was used to
test this version, do not send a drawing piece by piece. They send nothing for 45 seconds to two minutes and then the
whole answer at once, so no size can be shown during that time; only the elapsed time and the words above carry the
progress. An OpenAI model streams its drawing as it writes it, and for such a model the size of the file grows while
it is written once the library that connects CanvasPilot to Copilot passes those pieces on (the version that was used
for this guide does not yet). Nothing in the window is made up to fill the silence. If a model is too slow for you,
choose a smaller model or a lower reasoning level (see *Choosing a Model*).

## What a Turn Costs

When a turn ends, one quiet line of small text under the last answer says what it used, and only what the provider
reported, for example "180.3k in, 22.8k out, 5.9k reasoning, model x15 premium, 3:16": the tokens that were sent to the
model, the tokens that it wrote, the tokens that it spent on thinking, the multiplier of the model, and how long the
turn took. When you chose a context size for a Copilot model, the line names it (for example "long context"), because a
larger size is billed and served separately (see *The Context Size*). **GitHub Copilot counts each message that you send
as a number of premium requests equal to the multiplier of the model** (a model that is marked x15 costs fifteen),
however many steps the turn needed. Ollama reports tokens and no multiplier, so its line has tokens and the time only. A
turn that you stopped says "stopped, the request may still count", because the provider counts a request when the model
starts and **a Stop does not give it back**. When a turn produced no figures and was not stopped, there is no line.

The AI Provider page warns you before you spend it: when a large model is chosen with the reasoning level `high`,
`xhigh`, or `max`, the page says that the choice is slow and costly (see *The Reasoning Level*).

## Marks and Attachments

The copilot receives the same message that **View payload** shows. For each picture that you marked, it gets the picture
with the numbered marks drawn on it, close-ups of the area around each mark (at most ten), the note of each mark, and,
when the file reports where things were drawn, the parts of the source file under each mark. A mark chip in your text,
such as "Mark 2", tells it which mark you mean. A model that cannot see pictures cannot use the pictures; choose a
vision-capable model for work with marks (see *Setting Up Ollama*).

The attachments that you send are copied into the `attachments` folder of the project, and the copilot is told their
paths. It can read a text file there, and it can look at a picture or a drawing with its drawing tool. It can also read
the other files of the project, but never a file outside it.

## What the Copilot Can Do in Your Project

The copilot has only these tools, and every one works inside the project folder and nowhere else:

- **Look up CanvasNet help** (`canvasnet_help`): reads the cards of the drawing knowledge.
- **Read and check the style** (`style_get` and `style_check`): reads the style of the project and checks a picture of
  the project against the colors of the style.
- **List files** (`project_list`): lists the files and folders of the project, without hidden files, and links.
- **Read a file** (`project_read`): reads a text file of the project (a drawing source, `.csv`, `.json`, or `.txt`).
- **Write a file** (`project_write`): creates or replaces a drawing source or a small `.csv`, `.json`, or `.txt` file.
- **Draw a picture** (`render_image`): draws a file with CanvasNet and looks at the result, so that it can see its own
  work and correct it. The picture is made in memory and is not saved.

It cannot run programs, reach the network, read or change anything outside the project folder, or open your other files.
It also cannot write to the hidden `.canvaspilot` folder, to the project file, to the style kits in the `styles` folder,
or to any file or folder whose name starts with a dot (such as `.vscode` or `.git`). The *Drawing Knowledge* and *Style
Kits* chapters describe what it is given from them. CanvasPilot has no setting that gives the copilot more tools: the
option for unrestricted tools is not offered.

## Stop

Choose **Stop** while the copilot works, at any point: while it thinks, while it waits for the model, and in the middle
of a tool. The busy row says "Stopping..." at once and Stop cannot be pressed twice. The copilot then ends, and what it
has written so far is kept in the transcript, marked **Stopped**, with the note "The copilot was stopped." Files that it
has already written stay as they are. Send works again afterwards, and your next message is answered in the same
conversation; the copilot still knows what was said before.

Two things stay true when you stop. The provider may still count the request (see *What a Turn Costs*), and a turn that
the provider could not stop quickly is given up after five seconds, so the window never waits for it. If the
conversation was left in a state that the provider cannot use, the next message starts it again once, with the words of
the conversation so far, and the log says so ("Session recovered"); the same message is not tried a third time, and a
failure that has nothing to do with a Stop is shown to you and not hidden.

## Chat History

CanvasPilot keeps the **text** of your conversation, so that it is there when you open the project again:

- It saves your messages (with the labels of your marks and attachments), the copilot's answers, and a line for each
  tool that it used, after every turn, including a stopped or failed one. It never saves pictures or the payload.
  Messages that you have not sent are not saved.
- When you open the project, the earlier messages are shown as text with the note "Saved text from an earlier session;
  pictures are not kept." and the copilot is given the words of the conversation with your next message, so that it can
  go on. Only the most recent part is kept: up to 200 rows and 2 MB per project.
- The history is **not** stored in the project. It is below `%LOCALAPPDATA%\CanvasPilot\history` on this computer, in a
  folder named from the full path of the project file, so it is not copied when you copy the project, not committed to
  source control, and not shared with anyone. **If you move or rename the project folder or file, the project starts
  with an empty history**; the old history stays behind (the file `project.json` in its folder names the project it
  belonged to).
- **Clear chat history** (in the menu of the three-dot **More conversation actions** button at the top of the panel)
  deletes the saved history of this project from this
  computer, empties the transcript, and makes the copilot forget the conversation. It does not ask first, and it cannot
  be undone. The panel says "Chat history cleared."

## Files the Copilot Writes

**The copilot overwrites the files of your project directly. CanvasPilot keeps no backup and does not ask before the
copilot replaces a file.** The transcript shows what it did ("created" or "replaced", with the sizes), and the explorer
selects the file it wrote, but there is no way to undo it in CanvasPilot (Edit > Undo applies only to the message you
are writing; undoing a copilot turn is planned). Keep the project under source control (for example Git) or copy the
project folder before you ask the copilot to change files that you care about, and check the result before you rely on
it. The copilot is told to read a file before it replaces it and to keep what you did not ask it to change, but a model
can be wrong.

Only drawing sources and small data files are written (at most 256 KiB each), only inside the project folder, and only
files that the copilot is asked to create or change. CanvasPilot adds nothing else to your project because of the
copilot: no history file, no backup, no copy of its own data. The data of the Copilot runtime (its sessions) is kept in
`%LOCALAPPDATA%\CanvasPilot\copilot`, outside every project, and is
deleted when a conversation ends. A project folder that contains one of those folders is refused. Pictures that you
pasted into a message that is not sent yet are in `.canvaspilot\attachments` of the project, as before; consider listing
`.canvaspilot/` in the `.gitignore` of the project.

## Errors and What to Do

A problem appears in the transcript as an error notice with a sentence that says what to do and, when the settings can
fix it, a button **Open Settings**. Your message stays in the transcript, and you can send it again after the fix.

- **The provider cannot be used**: the notice is the sentence of the readiness check (see *Setting Up an AI Provider*).
  Open the settings, fix the entry, and choose **Check connection**.
- **The connection was lost or could not be made**: for Ollama, "Check that Ollama is running and that the address in
  Settings is right, then send the message again."
- **The model was not found**: "Run `ollama pull {model}` or choose another model in Settings."
- **The model settings cannot work**: the notice is the explanation of the provider (for example that the model cannot
  call tools, is not available to your account, or does not offer the reasoning level), and no run starts. Choose
  another model or level in Settings (see *Choosing a Model*).
- **The server refused the request**: check the address and any proxy in front of the server.
- **The copilot is still working**: "Press Stop to cancel it, then send again."
- **Open a project first**: the copilot works on the files of a project.
- **The project is inside CanvasPilot's own data folder**: open a project outside `%LOCALAPPDATA%\CanvasPilot`.
- **A tool failed**: the line of the tool says why (for example that a path is outside the project), and the copilot
  usually corrects itself in the next step.
- **An unexpected failure**: "Send the message again. If it keeps failing, look at the application log." Choose **Help >
  Open Log Folder** to open the folder of the log files. The log records, for each turn, its number, the provider, the
  model and the reasoning level, how the session with the provider was opened, each tool with its outcome, size, and
  time, how the turn ended and who ended it (you, a reset, a change of the project, or the end of the program), how
  long it took and how long the provider was silent at the longest, and the tokens used. It never records the words of
  your messages or of the answers, the pictures, the arguments of a tool, or the contents of files.

# Drawing Knowledge

CanvasPilot carries a guide to drawing with the formats that CanvasNet draws and to working in a CanvasPilot project. It
is a tree of short **cards**: one about each image format (SVG, Mermaid, Vega-Lite, OpenSCAD, and the others), about
style and composing pictures, and about CanvasPilot itself, such as where files go in a project, how to make the picture
that you use, and what the marks and attachments of a message mean. The cards of CanvasNet come with the CanvasNet
library that is installed with CanvasPilot, so they describe exactly what the application can draw. The few cards that
are only about the program that hosts CanvasNet, such as the options of a render in a program's code, are left out,
because CanvasPilot sets those itself; the cards about embedding pages of documents, pictures, and files, and about
setting a view or a parameter in a link, are kept. You can read the cards yourself, and the AI copilot uses the same
cards: it is given their index and looks up a card when it needs it. Opening the browser sends nothing anywhere.

## Opening the Browser

Choose **Help > Drawing Knowledge...** to open the **Drawing Knowledge** window. It opens beside the main window and
does not block it, so you can read a card while you work on a picture. Choosing the command again brings the window
that is already open to the front. The window closes when you choose **Close** or press Escape, and with the main
window. If the cards cannot be read, a message says so and the log has the details.

## The Tree and the Cards

The tree at the left lists the cards. Cards that have cards below them open and close with the arrow, and pausing the
pointer over a card shows its summary. Choose a card to read it in the pane at the right, which shows:

- the title and the one-line summary of the card, its tags, and the other names that it is known by,
- the text of the card, with headings, lists, bold, italic, and code text, and blocks of code that you can select,
- **Cards below**, the cards that the card is the parent of,
- **See also**, the cards that are related to it,
- **Examples**, each with a name, a short description, and its text.

The line between the tree and the card can be dragged to make either wider. Before you choose a card the pane tells you
to choose one or to search.

## Searching the Cards

Type two or more letters into the box above the tree to search. The hits replace the tree, best first, each with its
title and summary, and choosing a hit shows its card. The search looks at the names, other names, titles, tags, and
summaries of the cards and the names of their examples, not at the whole text of a card, so the card that is about a
thing comes before the cards that only mention it. When no card fits, the list says "No card matches those words."
Clear the box, or type fewer than two letters, to see the tree again. The card that you are reading stays as it is
while you type.

## Following Cards and Copying Examples

Choose a card under **Cards below** or **See also** to go to it. The tree opens the cards above it and selects it, so
you always see where you are, however you reached the card.

Each example has a **Copy** button. Choose it to put the text of the example on the clipboard, then paste it into a
new file in your project and change it. A line under the cards says "Copied example" with the name of the example when
it worked, or "Could not reach the clipboard." when another program is holding the clipboard; try again in a moment.
Nothing is written to your project by the browser.

# Style Kits

A **style kit** describes how your pictures should look, so every picture in a project, or every picture of a team,
has the same colors, type, and sizes. A kit holds:

- a **palette** of named colors (every kit has a *background*, a *text*, and a *primary* color, and may have more, such
  as *secondary*, *accent*, and *surface*, and colors of its own),
- **fonts** for headings, body text, and monospaced text, a **line width**, a **corner radius**, and a **spacing**,
- a **Mermaid theme** and a **Vega-Lite configuration** that you can copy into a Mermaid or chart file,
- **advice**: notes and a list of things to do and not to do,
- **export sizes**, such as a slide size, each with an optional resolution and transparency,
- **example images** with captions that show the style in use.

A kit is a plain folder, so it can be kept in a project, shared in a team folder, sent as a zip file, and edited by
hand: the folder is named after the kit's id and holds a `style.json` file and an `examples` folder.

CanvasPilot comes with three kits: **Clean** (a light, neutral look), **Slides Dark** (light text on a dark background,
with sizes for slides), and **DEMA** (the look of the CanvasPilot pictures). Their files cannot be changed, but you can
duplicate any kit and change the copy.

## Choosing the Style of a Project

While a project is open, the **style kit pill** at the right of the title bar shows the kit of the project, with up to
three of its colors, or a dashed circle for no style. Choose it to open the list, then choose a kit, or **(No style)**
for none. Each kit is listed with the place it comes from, for example *Clean (Built-in)*. The list ends with **Manage
style kits...**, which opens the Style Kits window. The
choice is saved in the project file, so everyone who opens the project, on any computer that has the kit, sees the same
style. The tooltip of the pill shows the description of the kit and its folder.

- A **new** project starts with the **default kit** that you choose on the Style Kits page of the settings (see *Style
  Kits Page* under *Settings*), or with no style when none is chosen. Changing the default later does not change
  projects that exist, and a project that is only opened keeps what its file says. A project made with an older version
  has no style until you choose one.
- When the project names a kit that cannot be found on this computer, for example one that lived in a team folder that
  you cannot reach, the list shows the id with *(not found)* and the project behaves as if it had no style until you
  choose another kit, or until the kit is back (choose **Tools > Style Kits...** and **Refresh**).

## Where Kits Are Kept, and Which Copy Wins

CanvasPilot looks for kits in four places. When the same id is in more than one place, the most local copy is the one
that is used, from the top of this table down:

| Place | Where | Can you change it? |
| --- | --- | --- |
| This project | The `styles` folder inside the project folder, one folder per kit | Yes |
| My kits | `%APPDATA%\CanvasPilot\styles`, one folder per kit | Yes |
| Shared | The shared folders of the Style Kits page; a higher folder wins | No, read only |
| Built-in | The `StyleKits` folder that is installed next to `CanvasPilot.exe` | No, they are read only |

A kit that is hidden by a more local copy is still listed, dimmed, with the place of the copy that hides it. This lets a
project freeze its style: **Copy into project** makes a copy of a kit in the project's `styles` folder, and from then on
the project uses its own copy even if the team changes the shared kit. The `styles` folder is an ordinary visible
folder: the explorer lists it like any other folder, and you can keep it with the project, add it to version control, or
edit its files with other tools (choose **Refresh** in the Style Kits window to read the files again). A kit that cannot
be used, for example because a color in its `style.json` is not written as `#RRGGBB`, is not offered; the window names
its folder and says which property is wrong.

## The Style Kits Window

Choose **Tools > Style Kits...** to open the **Style Kits** window. It stays open beside the main window, so you can
keep it while you work, and choosing the command again brings it to the front. At the left the kits are listed in
groups, **This project** (while a project is open), **My kits**, **Shared**, and **Built-in**, each kit with a strip of
its colors. A group says when it is empty, and a shared folder that cannot be reached is named in its group, for example
because the network is down, while the other groups still work. A kit marked *in use* is the style of the open project.
The window also opens from the end of the style kit pill's list (**Manage style kits...**).

![The Style Kits window in the dark theme. At the left the kits are listed in the groups This project, My kits, Shared,
and Built-in, each kit with its description, a strip of its colors, and its place; the chosen kit, Team Blue, has a
check mark, and DEMA has an "in use" label. At the right the details of Team Blue: its palette, its type and shape, and
its example images. A row of buttons runs along the bottom: Use in this project, New, Duplicate, Edit, Delete, Import,
Export, Copy into project, and Refresh.](style-kits.png)
Choose a kit to see it at the right: its description and place, its palette with each color's role and `#RRGGBB` value,
fonts, line width, corner radius, spacing, notes and advice, export sizes, any warnings about the kit, small pictures of
its example images with their captions, and, under **What an assistant is given**, the text that the copilot
receives about the kit, with its length. The buttons are:

- **Use in this project** makes the chosen kit the style of the open project. It is unavailable while no project is open
  and for a kit that is hidden by a more local copy.
- **New...** asks for a name and makes a kit with a starter palette in the place chosen under *New and imported kits go
  to* (**My kits** or **This project**), then opens it in the editor.
- **Duplicate...** makes a copy of the chosen kit, with its example images, under a new name, in the same chosen place,
  and opens the copy in the editor. This is how you change a built-in or shared kit.
- **Edit...** opens the chosen kit in the editor. It is unavailable for built-in and shared kits.
- **Delete...** deletes the chosen kit and everything in its folder, after you confirm. It is unavailable for built-in
  and shared kits.
- **Import...** reads a kit from a zip file into the chosen place. If that place already has a kit with the same id, you
  choose whether to replace it or to keep both (the new one then gets a number after its id and its name, for example
  `-2` and `(2)`). CanvasPilot refuses an archive that contains anything that is not part of a kit, a name that would
  reach outside the target folder, or too many or too large files, and then writes nothing; the line under the buttons
  says why.
- **Export...** writes the chosen kit and its example images to a zip file.
- **Copy into project** copies the chosen kit into the `styles` folder of the open project.
- **Refresh** reads all places again, for example after you changed a file with another tool.

The line under the buttons says what the last action did or why it could not be done.

## Creating and Editing a Kit

The editor opens as a window over the Style Kits window, with these pages:

- **General**: the name and the description. The id names the kit's folder and cannot be changed; duplicate the kit to
  give a copy another id.
- **Palette**: one row per color with its role, a swatch, and the `#RRGGBB` value, which you can type or set with the
  **RGB...** button, which opens three sliders. A *background*, a *text*, and a *primary* color are required. Add other
  colors with **Add color**. A hint shows when the text and background colors do not contrast enough to read.
- **Type and shape**: fonts as lists of families as in CSS (for example `sans-serif`), the line width, the corner
  radius, and the spacing, in pixels. The generic families (`sans-serif`, `serif`, `monospace`) and the common names
  such as Arial and Segoe UI are drawn with fonts that come with CanvasNet on every computer. A font that does not come
  with CanvasNet, such as `Inter`, gives a warning: only SVG and OpenSCAD files draw it, and only on a computer that has
  the font, while Mermaid, PlantUML, and Vega-Lite always draw their text with the bundled fonts.
- **Mermaid and Vega-Lite**: a Mermaid theme with its variables and a Vega-Lite configuration (a JSON object).
  CanvasPilot cannot give a theme or a configuration to a render, so these are starting points and advice for the files
  that you or the assistant write: a Mermaid file takes the theme in its front matter, and a chart takes the
  configuration in its `config` property.
- **Guidance**: notes, and the lists of what to do and not to do.
- **Sizes**: the export sizes: a name, a width, a height, a resolution (DPI), and whether the margin is transparent.
  Give a width, a height, or both (both fit the picture inside the box).
- **Examples**: example images, added with **Add files...** and removed with **Remove**, each with a caption. The files
  are copied into the kit when you save, and a file that you removed is deleted from the kit then.

The editor checks the kit as you type with the same rules as the kit's file. **Save** is unavailable while a message in
red is shown, and each message names the property that is wrong; warnings do not stop you from saving. **Cancel** closes
the editor and writes nothing. Saving writes the kit to its own folder, so a project's kit changes only when you edit
the copy in the project's `styles` folder.

## Exporting at a Size

**Image > Export as PNG...** first opens the **Export PNG** dialog, which lists, with the resulting size of each:

1. **As rendered**, selected at first: the picture exactly as it is shown, with its transparency. Choosing it gives the
   same file that earlier versions wrote.
2. The **export sizes of the style kit** of the project, if it has any.
3. The **sizes of the application**: *PowerPoint 16:9* (1920 x 1080), *Email (800 wide)* (800 pixels wide, as high as
   the picture needs), and *A4 portrait @150 DPI* (1240 x 1754, with a resolution of 150 DPI written into the file). A
   size of the kit with the same name replaces the size of the application.

Choose a size and **Export...**, then choose the file. For a size CanvasPilot draws the source file again at that size,
so lines and text stay sharp and nothing is stretched. A width and a height define a box in which the picture is fitted
with its proportions kept, and the margin is filled with the background color of the kit (white when the project has no
style); a size marked transparent keeps the margin transparent instead. If the file cannot be drawn at that size, a
message says why and no file is written. **Copy Image** and dragging the picture out always use the picture as it is
shown.

## Checking a Picture Against the Style

With a picture shown and a kit chosen for the project, **Image > Check Against Style** compares the colors of the
picture with the palette of the kit and shows the result among the notes under the picture, which opens for it. The
check counts pixels, so it is about how much of the picture is which color:

- a **summary** that says *PASSED* when at most 5 percent of the picture is off the palette, and *FAILED* otherwise,
- the palette colors that the picture uses, with their share,
- the share of **blended edges**, the pixels between two palette colors that smooth the edge of a shape, which are not
  counted as off the palette,
- each **off-palette** color that covers at least 1 percent of the picture, as a warning with its share and the nearest
  palette color, so you know what to change. A color counts as a palette color when it is close to one (the same to the
  eye).

The notes stay until the picture is drawn again. With no picture, the item is unavailable; with no kit, a message tells
you to choose one with the style kit pill first. A picture that uses only the kit's colors passes; Mermaid and chart
defaults, such as grey grid lines and black text, are reported when the file leaves them at the defaults, which is the
reason the examples of the kits set every color.

## What the Copilot Does With a Kit

The AI copilot is given the style of the project's kit as a short block of text (the palette, fonts, shapes, and advice,
and the paths of the example images, which it can look at), and two tools, one to read the style and one to check a
picture of the project against the palette. The text of a kit is given to it as data and not as instructions, and its
length is limited. Nothing about the kit is sent to a provider until you send a message and the provider works (see
*Setting Up an AI Provider*). You can see the text in the Style Kits window under **What an assistant is given**.

## Checks That Only a Person Can Do

*Not yet checked:* CanvasPilot's own tests stand in for the operating system's folder and file pickers and for the color
sliders, so the pickers that choose a shared folder, a zip file to import or export, and example images, and the slider
flyout of a color, have not yet been tried by a person on an installed package. If one does not work for you, please
report it (see *Troubleshooting*); you can still type the full path of a shared folder on the Style Kits page and a
color as `#RRGGBB`.

# Settings

Choose **Tools > Settings...** to open the Settings dialog, or choose the AI provider pill in the title bar, which opens
it on the AI Provider page. The page rail at the left shows **AI Provider**, **Speech**, **Style Kits**, and
**Appearance**, each with an icon and a title, and the heading above the chosen page repeats the title with a sentence
about it. **OK** saves the changes from all pages and closes the dialog, **Apply** saves them and keeps the dialog
open, and **Cancel** closes it without saving. OK and Apply are unavailable while an entry is invalid, and the page
shows what is wrong in a red banner at the foot of the dialog, with an icon and words.

## AI Provider

The AI Provider page chooses the provider that the copilot uses and how to reach it (see *Setting Up an AI Provider* for
what to install and the messages of the check). Changes take effect when you choose OK or Apply, and a conversation
that is running is started again with the new choices when you send the next message. The page shows only the entries
that belong to the provider that you chose: the options of the other provider are hidden, not just dimmed, so that
nothing on the page asks for a value that would not be used.

Shown for both providers:

- **Provider**: GitHub Copilot or Ollama. Ollama is supported and covered by automated tests (live checks are pending);
  GitHub Copilot runs through the Copilot CLI that you installed.
- **Model**: The list of the models that the provider offers, with a note for what each lacks, and below it the
  capabilities of the chosen model and any warning (see *Choosing a Model*). **Provider default model** leaves the
  choice to the provider. **Refresh models** asks the provider again and shows why when there is no list.
- **Model name**: The id of the model, filled by your choice in the list and also typed. For Ollama a model is required
  and the name is the one that `ollama list` shows. For GitHub Copilot, leave it empty to use the provider's default
  model. A name that is not in the list is tried as you typed it.
- **Reasoning level**: The levels that the chosen model offers, or "Provider default". It is enabled only while the
  model offers levels (see *The Reasoning Level*). A level that an earlier version stored under the old setting is read
  as it was (low, medium, or high), and is kept until you choose another.
- **Check connection**: Checks the entries on the page, as they are now, and shows the answer below the button (see
  *Check Connection and the Banner*).

Shown only while **GitHub Copilot** is chosen:

- **Context size**: Shown only when the chosen model offers more than one size, for example the one-million-token size
  of Claude Opus 5.5 (see *The Context Size*).
- **GitHub Copilot command-line tool path**: The full path of the installed `copilot.exe`. Leave it empty to locate the
  tool automatically. A path that you enter wins over the search, and launcher shims, such as the one that VS Code puts
  on the `PATH`, are ignored by the automatic search (see *Which Copilot CLI Is Used*).
- The warning that a high reasoning level on a large model is slow and costly (see *The Reasoning Level*).

Shown only while **Ollama** is chosen:

- **Context window (tokens)**: The size of the context window that is requested for every Ollama request,
  from 512 to 2,000,000 tokens (default 32,768). A value that you or an earlier version stored is kept as it is. Below
  16,384 tokens the page shows a warning, not an error, that says that drawings with pictures and tools may fail. A
  window that is larger than the model was trained for is refused when the conversation starts. GitHub Copilot sets its
  own window, so the entry is not shown for it.
- **Ollama endpoint**: The address of the Ollama server (default `http://localhost:11434`).

Choosing the other provider keeps what you typed in the entries that were hidden, and an entry that is hidden can never
stop you from choosing OK.

There is no option for unrestricted tools: the copilot always has only the tools of CanvasPilot, confined to the
project folder (see *What the Copilot Can Do in Your Project*). A value that an earlier version stored for it is kept in
the settings file and has no effect. There is no field for a password or a token either; the settings file never
contains credentials.

## Speech

The Speech page stores which audio devices and speech models CanvasPilot is to use. Dictating a message uses the
**speech-to-text model** and the **microphone** that you choose here (see *Dictating a Message*); the text-to-speech
choices are stored but not used yet.

- **Push-to-talk key** is the key that you hold to talk, F9 by default (see *Choosing the Talk Key*). Click the box and
  press the key, or the keys, that you want; Esc cancels, and a modifier alone is waited out until you add the key. If
  the key is not safe, a sentence under the box says why and the key stays as it was. **Reset to default (F9)** puts F9
  back. The key is used when you choose OK or Apply. While the box has the focus it takes every key that you press
  except Tab and Shift+Tab, which leave the box as they do everywhere else.
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

## Style Kits Page

The Style Kits page stores where CanvasPilot looks for the kits of your team and which kit new projects start with (see
*Style Kits*).

- **Shared kit folders** lists the folders in which a team keeps style kits, for example `\\server\share\kits`. Type or
  paste a full folder path and choose **Add**, or choose **Browse...** to pick a folder. A folder is listed with whether
  it can be reached now, because a network folder can be offline. **Move up** and **Move down** change the order: when
  the same kit is in more than one folder, the one that is higher in the list is used. **Remove** takes a folder out of
  the list; the folder itself is not touched. The kits in these folders are read only. A path that is not a full path,
  and a folder that is already in the list, is refused.
- **Default kit for new projects** chooses the kit that a project that you create starts with. Projects that exist keep
  the kit they have.

## Appearance Page

The Appearance page chooses how CanvasPilot looks. CanvasPilot has a dark and a light theme, which give the same
layout and the same text sizes with different colors; both keep the contrast between text and its background high.

![The main window of CanvasPilot in the light theme, with the same project and conversation as the dark window shown
earlier: pale panes for the explorer and the conversation, a light gray stage with the picture, the floating marking
tools and the zoom pill, and the same title bar and status bar.](main-window-light.png)

- **Theme** chooses **Follow system** (the default), **Dark**, or **Light**. With **Follow system**, CanvasPilot is dark
  or light as your operating system is, and changes with it while it runs. **Dark** and **Light** keep that look
  whatever the system does. The theme changes in every open window as soon as you choose **OK** or **Apply**.
- **Show a small picture beside each file** turns the thumbnails of the explorer on or off (on by default).
- **Comfortable rows (taller, with more room between files)** makes the rows of the explorer 32 pixels high instead of
  26 pixels (off by default). The rows change as soon as you choose **OK** or **Apply**.

# Keyboard and Accessibility

Everything that the window offers with a pointer can be reached with the keyboard, and the menu is always there as the
slower path to a command that has a button.

Press **Tab** to move through the main parts of the window: the menu, the pills, the explorer, the stage with its tools,
the conversation, and the message box (Shift+Tab goes back). The control that has the focus shows a ring around it. The
keys that CanvasPilot has, besides the usual keys of the system, are:

| Where | Keys | What they do |
| --- | --- | --- |
| Anywhere | Alt and the underlined letter | Open a menu: File, Edit, Image, Tools, or Help |
| Anywhere | Ctrl+N, Ctrl+O, Ctrl+S | New project, open project, save project |
| Message box | Enter, Shift+Enter | Send the message, start a new line |
| Message box | Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z | Undo and redo |
| Message box | F2 | Change the note of the mark whose chip is right next to the cursor |
| Note card | Enter | Add the mark with its note, or save a changed note |
| Note card of a changed note | Escape | Close the card and keep the note as it was |
| Anywhere in the window | Talk key (F9 by default; hold) | Talk, and let go to add the words to the message |
| Explorer | Up and down arrows | Move through the rows |
| Explorer | Right and left arrows | Open and close a folder |
| Explorer | F2, Delete | Rename and delete the selected picture |
| Stage | Ctrl+plus, Ctrl+minus, Ctrl+0, Ctrl+1 | Zoom in, zoom out, fit to window, actual size |
| Stage | Ctrl+mouse wheel | Zoom in and out |
| Stage | V, R, E, F, A | Choose Select, Rectangle, Ellipse, Freehand, or Arrow |
| Stage | Shift+F10, Menu key | Open the menu of the picture: Copy image, Export as PNG... |
| Marking | Arrow keys, then Enter or Space | Move the cursor on the picture, start and finish a shape |
| Marking | Escape | Throw a shape away |
| Splitter | Left and right arrows | Move the splitter by 16 pixels |
| Welcome screen | Arrows, Enter, Delete | Move between recent projects, open one, remove it from the list |

CanvasPilot is built to be usable without color and with a screen reader:

- Every button that shows only an icon has a name and a tooltip that say what it does, for example **Attach files** and
  **Zoom in**, and none is smaller than 28 × 28 pixels.
- Color is never the only sign. The AI provider pill and the status bar use words next to their dots and icons, marks
  have numbers, the tool chips use a check, a spinner, or a cross, and the chosen file has a bar as well as a tint.
- The colors are chosen so that the contrast between text and its background is at least 4.5 to 1, and between outlines
  and the focus ring and what is around them at least 3 to 1, in both themes.
- The status bar, the sentence about the marks, and each new row of the conversation are set up to be announced politely
  by a screen reader when they change.
- The tree of the explorer, the lists, the text boxes, and the splitters have names. The message box is named "Message"
  and tells a screen reader the text of the message, with each chip as its label in brackets and the note of a mark
  after its label, for example "[Mark 1: make it red]"; the pages of the settings
  are named by their titles ("AI Provider", "Speech", and so on), and the style pill by the name of the chosen kit or
  "(No style)".
- Every drop-down list tells a screen reader its chosen entry, and every list its rows, by the words that you see, for
  example "(No style)" for the default kit on the **Style Kits** page, the name of a speech model, or the name of an
  export size.

Pulsing and moving parts of the window, such as the dots while the copilot works and the busy line at the top of the
stage, do not yet stop when your system asks for less motion. The window has not yet been checked with a screen reader
or at a display scale of 125 or 150 percent by a person; if something does not read or fit, please report it (see
*Troubleshooting*).

## Known Limitations

These parts of the redesigned window are not built yet:

- The folders that you expanded in the explorer, and whether you collapsed the conversation, are not remembered after
  you close CanvasPilot; the window opens with the folders closed and the conversation shown.
- The conversation has no day dividers and no **Jump to latest** chip.
- A transparent picture is shown on the plain stage color, not on a checkerboard, and you cannot pan the picture by
  holding the Space key and dragging.
- The sentence about the marks is the tooltip of the marking toolbar and is read out by a screen reader, but it is not
  shown in the status bar.
- The size of a file that the copilot is writing is shown only when the library that connects CanvasPilot to Copilot
  passes on the pieces of the drawing, and not for models that send the drawing whole; the elapsed time is always shown
  (see *Following a Turn*).

# About CanvasPilot

Choose **Help > About CanvasPilot** to see the application name, the version, the copyright notice, and the
third-party libraries and material that CanvasPilot uses. Include the version when you report a problem.

The line icons of the window are glyphs of the Fluent System Icons by Microsoft Corporation, used under the MIT License
(see <https://github.com/microsoft/fluentui-system-icons>). Their notice is listed in the About dialog and in the file
`THIRD-PARTY-NOTICES.md` of the repository.

# Where CanvasPilot Keeps Its Files

| What | Location |
| --- | --- |
| Settings and recent projects | `%APPDATA%\CanvasPilot\settings.json` |
| Log files (choose **Help > Open Log Folder** to open it) | `%LOCALAPPDATA%\CanvasPilot\logs` |
| Chat history of each project (text only) | `%LOCALAPPDATA%\CanvasPilot\history\{key}` |
| Copilot runtime data (its sessions) | `%LOCALAPPDATA%\CanvasPilot\copilot` |
| Pictures prepared for dragging out (temporary) | `%LOCALAPPDATA%\CanvasPilot\drag` |
| Pictures that you pasted into a message that is not sent yet | `.canvaspilot\attachments` in the project |
| Files and folders that you sent with a message | `attachments` in the project |
| Files that the copilot created or replaced | Anywhere in the project folder |
| Style kits of the project | `styles` in the project |
| Your own style kits | `%APPDATA%\CanvasPilot\styles` |
| Style kits that come with CanvasPilot (read only) | `StyleKits` next to `CanvasPilot.exe` in the installation |
| Downloaded speech models | `%LOCALAPPDATA%\DemaConsulting.Speech\Models` |
| Your projects | The folder that you chose for each project |

The settings file never contains credentials. The log files stay on your computer and are never sent anywhere; they
roll over when they reach a size limit and old ones are removed, so they cannot fill the disk. They record what
happened (provider, model, tool names, sizes, and durations), but never the words of your messages or of the answers,
pictures, or the contents of your files. To reset all settings, close CanvasPilot and delete `settings.json`. The same
file holds the theme and the explorer choices of the Appearance page, and the size, position, and splitter widths of the
window.
CanvasPilot never stores what you say or the audio from the microphone; the words of a dictation exist only in the
message that you are writing.

A message that you have not sent is not saved, so it is lost when you close the project or CanvasPilot. The **text** of
the messages that you sent and of the copilot's answers is saved on this computer, in the history folder above, and
comes back when you open the project again; pictures and payloads are not saved. The history folder is named from the
full path of the project file, so a project that you move or rename starts with an empty history and the old folder is
left behind (its `project.json` says which project it belonged to; you can delete it). The history is never stored in
the project and is not copied with it. **Clear chat history** deletes it. The only things that CanvasPilot puts in your
project folder are the files that you and the copilot make, the `attachments` folder, and the hidden `.canvaspilot`
folder with the draft pictures of unsent messages; if you keep projects in source control, add `.canvaspilot/` to its
ignore list.

# Troubleshooting

**A file is missing from the explorer.** The explorer lists folders and the types of file that CanvasPilot can draw, and
no hidden files or folders. Open the folders that hold it, check its extension, and choose **Refresh** in the header of
the explorer. A file or folder whose name starts with a period is never listed.

**A file that I dropped on the explorer is not there.** Only pictures, PDF files, and drawing files are accepted; the
message says what was refused. Dropped files are copied into the `attachments` folder, or into the folder that you
dropped them on.

**The stage shows an error.** Read the code, message, and hint. Typical causes are a syntax error in the file, a
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

**The conversation pane is not there.** The conversation is on the right of the stage and is shown only while a project
is open. Open or create a project. If you hid it with the button in its header, choose the button at the right edge of
the stage to bring it back.

**Send is not available.** Send works only while the message has some text or an attachment, no talk button is held,
the previous message is not still being sent, and the copilot is not still working on the previous message. Enter sends
only under the same conditions. Let go of the Hold to talk button, write something, or choose **Stop** while the copilot
works.

**Enter sends my message when I wanted a new line.** Press Shift+Enter for a new line; Enter alone sends.

**Holding the talk key does nothing.** Look at the key cap beside the microphone button, or at the status bar, for the
key that is in effect: it is F9 unless you chose another. Hold it while the CanvasPilot window is active; the key
works only in the window. On many keyboards the function keys do something else (brightness, volume, or the like)
unless Fn is held or the Fn lock is on, and some programs register a shortcut for the whole computer that takes a key
before CanvasPilot sees it. Choose another key on the Speech page (see *Choosing the Talk Key*), or use the microphone
button. If the button works and the key does not, it is the key.

**The copilot has shown nothing for minutes.** Look at the elapsed time in the busy row: while it counts, CanvasPilot is
waiting and the turn is alive. Some models send a drawing only when it is whole, so a large drawing can take two or
three minutes with no other sign (see *Following a Turn*). Choose **Stop** if you do not want to wait; the provider may
still count the request. For everyday work choose a lower reasoning level or a smaller model.

**I stopped the copilot and the next message did not work as expected.** The next message goes to the same conversation.
If it cannot be used, CanvasPilot replaces it once and writes "Session recovered" to the log. If you see an error
notice instead, send the message again; the notice and the log say what happened (choose **Help > Open Log Folder**).

**Where is the log?** Choose **Help > Open Log Folder**, which works with or without a project and shows the folder in
your file manager (see *Where CanvasPilot Keeps Its Files*). Attach the newest file to a report of a problem after you
have checked that it holds nothing private (see *Reporting a problem*).

**A tool chip is red.** The tool did not work, and the chip says why, for example that a path is outside the project.
The copilot usually corrects itself in the next step (see *Errors and What to Do*).

**The banner says the GitHub Copilot CLI was not found.** Install it with `winget install GitHub.Copilot`, or enter the
full path of the installed `copilot.exe` in the settings. If the message says that only the launcher of an editor was
found, the real tool is missing: the launcher that Visual Studio Code puts on the `PATH` is not accepted (see *Which
Copilot CLI Is Used*).

**The banner says Copilot CLI is older than 1.0.113.** Run `winget upgrade GitHub.Copilot`, then choose **Check
connection**.

**The banner says Copilot is not signed in for CanvasPilot.** Sign in with the Copilot CLI: run `copilot` in a terminal,
type `/login`, and follow the steps, then choose **Check connection** (you do not need to restart CanvasPilot).
Instead you can sign in with `gh auth login` (with `gh` on the `PATH`), or set the environment variable
`COPILOT_GITHUB_TOKEN` (`GH_TOKEN` or `GITHUB_TOKEN` also work) and then close and start CanvasPilot again. The words
after "What was found:" tell what CanvasPilot's AI library looked at for each way of signing in and why it did not
work. CanvasPilot takes the sign-in in this order: a token variable, the sign-in of the Copilot CLI, then the GitHub
CLI.

**Check connection shows the wrong account, or a new account is not used.** The sentence after "is ready" names the
account, the host, and how Copilot signed in, for example "Signed in as octocat on github.com (Copilot CLI sign-in)." If
you changed the account in the Copilot CLI (`/login`) or in the GitHub CLI, close and start CanvasPilot again, because
the Copilot runtime keeps the account that it started with. A token variable wins over the sign-in of the Copilot CLI,
so if the sentence says "(token variable)" and you wanted the other account, clear `COPILOT_GITHUB_TOKEN`, `GH_TOKEN`,
and `GITHUB_TOKEN` in the environment that starts CanvasPilot.

**The banner says Copilot is not signed in, although I signed in with the Copilot CLI, and a token variable is set.** A
token variable that holds a token that your GitHub host does not accept (for example a token for another host) is
refused and stops the search, so the sign-in of the Copilot CLI is not tried. Clear the variable, and close and start
CanvasPilot again.

**The banner says the Copilot CLI is too old.** The Copilot runtime needs version 1.0.113 or later. Upgrade the Copilot
CLI itself with the commands that the banner lists (for example `winget upgrade GitHub.Copilot`), then choose **Check
connection**. `copilot update` alone may not change the version that runs.

**The banner says the Copilot runtime could not be started.** Choose **Check connection** to try again. If it keeps
failing, check that the Copilot CLI starts on its own, that the path in Settings (if you entered one) is the full path
of the installed `copilot.exe`, and read the explanation that the banner gives.

**The answer stops with a message that the Copilot runtime was stopped.** The Copilot CLI path in the settings was
changed while the copilot was answering, which restarts the runtime. Send the message again, and change the path only
while the copilot is idle.

**The banner says the Copilot CLI path in Settings cannot be used.** A relative path or a `.cmd` or `.bat` file cannot
be used. Enter the full path of the installed `copilot.exe`, or leave the field empty to search.

**The banner says Ollama did not answer.** Start Ollama, or fix the **Ollama endpoint** in the settings, and choose
**Check again**. Check that a firewall or a proxy is not in the way when the server is on another computer.

**The banner says Ollama does not have the model.** Run `ollama pull` with the name that the banner shows, or choose a
model from the list of the AI Provider page, and choose **Check again**.

**The banner says a model does not support tool calling, is not available, or does not support a reasoning level.** The
model settings cannot work, and the copilot does not start until you change them. The sentence is the explanation of the
provider and names what is supported (for example the levels that the model offers). Open the settings, choose another
model or level from the lists (see *Choosing a Model*), and choose **Check connection**. For GitHub Copilot, the list
shows the models of your account; a model that is not in it is not available to you.

**The banner says a model does not accept pictures.** That is a warning, not an error. The copilot works, but it cannot
see your marks, your pictures, or its own drawings. Choose a model with pictures if you need that.

**The model list says that Ollama did not answer, or that Copilot is not signed in.** The list cannot be had, so the
page shows the reason next to **Refresh models** and keeps the **Model name** box for typing. Fix the cause (start
Ollama, run `copilot` and type `/login` or sign in with `gh auth login`, correct the endpoint or the path), then choose
**Refresh models**.

**The banner warns about the context window.** The window that you set (or that the model supports) is below 16,384
tokens, so drawings with pictures and tools may fail. Raise **Context window** to 32,768 on the AI Provider page, or
choose a model with a larger window.

**The copilot does not see my marks or pictures.** The model must accept pictures. Choose such a model in the settings
(see *Choosing a Model*); the list says "no pictures" for the ones that do not. With another model the copilot works
from your words and from the files.

**The copilot answered with an error.** The notice says what happened and what to do (see *Errors and What to Do*).
Your message is still in the transcript; fix the cause, and send it again.

**The copilot replaced a file that I wanted to keep.** CanvasPilot keeps no backup and does not ask before the copilot
replaces a file. Restore the file from source control or from your own copy. To avoid it next time, keep the project
under source control and ask the copilot to create a new file instead of changing the old one.

**My chat history is gone.** The history is kept per project on this computer, named from the path of the project file.
A project that was moved or renamed starts with an empty history, and **Clear chat history** deletes it. A history file
that cannot be read is set aside with the ending `.bad` in its folder, and the history starts again (see *Where
CanvasPilot Keeps Its Files*).

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
moment. CanvasPilot prepares it when the conversation pane is shown. After you change the model on the Speech page the
next dictation loads it again.

**My marks are gone from the picture.** Marks belong to the picture that they were drawn on. When the file changes and
the picture is drawn again, the marks are not shown on the new picture, and the marking bar says how many belong to
an earlier picture. Marks are also hidden while their chip is not in the message, for example after you deleted the chip
or undid it; redo or Ctrl+Y brings them back.

**Drag-out does not work while marking.** Dragging the picture out is turned off while a marking tool is chosen. Choose
**Select** on the marking bar first.

**The Drawing Knowledge window does not open.** A message says that the cards could not be read and the log has the
details. Cards are part of the installation, so reinstall CanvasPilot with the package from the releases page. If the
window is already open it is brought to the front instead of opening a second one.

**The search finds no card.** The search needs two or more letters and looks at the names, titles, tags, and summaries
of the cards, not at their whole text. Try another word for the thing, such as the name of the format, or open the tree
and read the card that is about it.

**A shared folder of style kits is not reachable.** The Style Kits page and the Style Kits window say that the folder
cannot be reached, for example because the network share is offline or you may not read it. The other places still work,
and the shared kits come back when you choose **Refresh** in the Style Kits window once the folder is reachable again.
A project that uses a shared kit shows *(not found)* in the meantime (see the next item). To keep a kit available
without the share, use **Copy into project**.

**The project's style kit is not found.** The style kit pill shows the id with *(not found)* when the project's
file names a kit that is not on this computer: the kit may have been deleted, may live in a folder that is not
reachable, or may have been made by a teammate who did not share it. Choose another kit or **(No style)**; or add the
folder of the kit on the Style Kits page of the settings, or import it from a zip file, and choose **Refresh**.

**A style kit is not listed.** A kit whose `style.json` has an error is not offered. The group in the Style Kits window
that holds its folder names the folder and the property that is wrong, for example a color that is not written as
`#RRGGBB`. Fix the file (or edit the kit), and choose **Refresh**. A kit that is dimmed is hidden by a copy of the same
id in a more local place (see *Where Kits Are Kept, and Which Copy Wins*).

**I cannot edit or delete a style kit.** The kits that come with CanvasPilot and the kits in shared folders are read
only. Choose **Duplicate...** and change the copy, which is kept in *My kits* or in the project.

**An import is refused.** The message under the buttons of the Style Kits window says why: the zip file has no
`style.json`, holds files that are not part of a kit, has a name that would reach outside the kit's folder, is too large
(at most 100 files, 16 MB for one file, and 64 MB in all), or the kit's file has an error. Nothing is written when an
import is refused.

**Export as PNG... asks which size.** This is the Export PNG dialog (see *Exporting at a Size*). Keep *As rendered*
selected to save the picture as it is shown.

**Reporting a problem.** Open an issue on the
[CanvasPilot issues page](https://github.com/demaconsulting/CanvasPilot/issues) and attach the newest file from the log
folder, after you have checked that it holds nothing private.

## References

- [CanvasPilot repository](https://github.com/demaconsulting/CanvasPilot)
- [CanvasPilot releases](https://github.com/demaconsulting/CanvasPilot/releases)
