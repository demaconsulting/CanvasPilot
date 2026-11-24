### StatusBar

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`StatusBar` is the bar at the bottom of the main window. It follows the workspace and its preview pane and says what
picture is shown and in what state it is, with words as well as with an icon, and, while a project is open, when its
project file was last saved (when that is known) and which key is held to talk. The bar is always there; with no project
open it says so.

#### Data Model

**StatusBarViewModel** (`AppShellSubsystem/StatusBarViewModel.cs`): the view model. It is created by `MainWindowShell`
over the `ProjectWorkspaceViewModel` (the `StatusBar` property of the shell) and is disposed with it. It owns nothing of
the workspace.

- *State*: `StatusBarState` is `Idle` (no picture is shown), `Rendering`, `Failed`, or `Ready`. It is not stored; it is
  worked out from the preview pane that is followed now: no pane gives `Idle`; a busy pane gives `Rendering`; a pane
  with an error gives `Failed`; a pane with an image gives `Ready`; otherwise `Idle`. `IsIdle`, `IsRendering`,
  `IsFailed`, and `IsReady` are the four flags that the view binds to.
- *Sentence*: `Summary` is "No project open" or "No picture selected" (by `HasProject` of the workspace) while idle, and
  otherwise the status sentence of the preview pane, for example "red.svg: 40 x 20 px, svg", "Rendering a.svg...", or
  "b.svg: render failed".
- *Facts*: `FileName` is the file name of the pane or an empty string. `SizeText` (such as `900 × 500 px`), `FormatText`
  (such as `SVG`), and `RenderTimeText` (such as `Rendered in 84 ms`) are read from the pane only while the state is
  `Ready` and are empty otherwise. `NotesText` is the header of the diagnostics of the pane (such as `1 note`) while the
  state is `Ready` and the render has notes; `HasNotes` tells whether it is not empty.
- *Project*: `HasProject` mirrors the workspace. `SavedText` is "Project file saved" followed by the time of the last
  save of the project file (`ModifiedUtc` of the document of the open project, read from the `IProjectService` that
  the shell gives the bar): the time of day when it was today, and otherwise the date and the time, in the culture and
  the time zone of the user. `HasSavedText` is false, and nothing is shown, while no project is open or the file has no
  saved time (a new project, or a file of an earlier build). `TalkKeyText` is the text of the push-to-talk key in effect
  (`F9` by default), which follows the `PushToTalkSetting` of the shell.

The inputs of the facts are the view model properties `FileName`, `ImageSizeText`, `FormatText`, `RenderTimeText`,
`RenderMilliseconds`, `StatusText`, `HasDiagnostics`, and `DiagnosticsHeader` of the preview pane, which the
*PreviewPane* unit documents.

**The saved text says only what is true.** The application has no dirty tracking, so the bar does not claim that the
work is saved. The project file holds only the metadata of the project (and is written when the project is created or
the style kit changes, and by File ▸ Save), the drawings are files that the copilot writes, and the chat history is
saved apart; none of those is claimed. The time is the `ModifiedUtc` of the document, and the bar follows
`CurrentChanged` of the project service, which the service raises after every save, so a save updates the time. The
service may raise the event on another thread, so the bar announces the change on the synchronization context that it
was made on.

**StatusBarView** (`AppShellSubsystem/StatusBarView.axaml` and `.axaml.cs`): a user control whose data context is the
view model. Its only code is the call that loads the XAML.

- *Layout*: a `Border` named `StatusBar`, 26 pixels high, with the window surface as its background, a one pixel subtle
  line along its top edge, and the small type size in the secondary text color. Inside, a grid has a left column that
  takes the width that is left and a right column that is as wide as its content, with 16 pixels of margin on both
  sides.
- *Left side*: one `Panel`, `PreviewStatus`, holds four groups of which one is visible at a time: `StatusBarReady` (a
  green check icon, the file name trimmed to 320 pixels, the size, the format, the render time, and, when there are
  notes, the notes in the warning color, with a middle dot between the facts), `StatusBarRendering` (a small circular
  progress bar, the word "Rendering...", and the file name), `StatusBarFailed` (a red error icon, the words "Render
  failed", and the file name), and `StatusBarIdle` (the summary sentence in the tertiary text color). The state is told
  by words and by an icon or a progress bar, never by color alone.
- *Right side*: shown only while `HasProject`; the check icon with the "Project file saved 14:32" words
  (`StatusBarProjectSaved`, shown only while `HasSavedText`) and the hint "Hold F9 to talk" (`StatusBarTalkHint`), in
  which the key name is drawn as a key cap.
- *Screen reader*: the `PreviewStatus` panel has the `Summary` sentence as its automation name and the live setting
  `Polite`, so a screen reader is told when the sentence changes without being interrupted.

**AutomationIds** that a tester needs: `StatusBar` (the border inside the view), `PreviewStatus` (the panel, which moved
here from the preview pane view, whose automation name is the summary sentence), `StatusBarReady`, `StatusBarRendering`,
`StatusBarFailed`, `StatusBarIdle`, `StatusBarFile`, `StatusBarSize`, `StatusBarFormat`, `StatusBarRenderTime`,
`StatusBarNotes`, `StatusBarProjectSaved`, and `StatusBarTalkHint`.

#### Key Methods

**Constructor**: refuses a missing workspace and subscribes to the property changes of the workspace, to the changes of
the talk key, and, when there is a project service, to its `CurrentChanged`; then it follows the preview pane that the
workspace holds. The talk key (`PushToTalkSetting`) is optional and defaults to a fixed setting that holds the default
key, the project service is optional (without it nothing is said about saving), and the clock (`TimeProvider`, the
system's by default) tells what day it is for the text of the time; the shell passes its own talk key and project
service.

**FormatSavedTime**: a public static function that writes the time of a save: `ToLocalTime`, then the short time pattern
(`t`) of the culture when the save was on the same local day as now, and otherwise the general short date and time
pattern (`g`).

**FollowPreview**: compares the preview of the workspace with the one that is followed. When they differ it drops the
subscription to the old pane and subscribes to the new one. A pane that is not a `PreviewPaneViewModel` is treated as no
pane.

**OnWorkspacePropertyChanged**: when the workspace announces a change of `Preview` or `HasProject`, follows the pane
again and announces a change of every property (an empty property name), because every part of the bar depends on the
same few facts.

**OnPreviewPropertyChanged**: announces a change of every property whenever the followed pane changes anything.

**OnProjectsChanged**: when the project service announces that the open project changed or was saved, announces
`SavedText` and `HasSavedText` on the synchronization context that the bar was made on (directly when there was none),
because the service may raise the event on another thread.

**OnTalkKeyChanged**: when the text of the talk key changes, announces `TalkKeyText`.

**Dispose**: unsubscribes from the workspace, the talk key, the project service, and the pane and drops the pane. After
it nothing is announced.

#### Error Handling

The view model has no operation that can fail. A missing workspace is refused with an `ArgumentNullException`. A render
that fails is not an error of the bar: it is one of its states, shown with the words "Render failed" and the danger
color, and the sentence of the pane is passed to the screen reader.

**Known limitations**: The time is that of the last save of the project file, which holds only the metadata of the
project; the bar cannot tell whether the drawings or the chat have unsaved changes, and says nothing about them. The
sentence of the marking tools is not part of the bar; it is read out from the overlay of the picture.

#### Dependencies

- **ProjectWorkspace** — `ProjectWorkspaceViewModel` supplies `HasProject`, the `Preview` pane, and their change
  notifications.
- **PreviewPane** — `PreviewPaneViewModel` supplies the busy, error, and image flags, the file name, the size, the
  format, the render time, the sentence, and the diagnostics header.
- **ProjectService** (through `IProjectService`) — the open project, whose document says when the project file was saved
  (`ModifiedUtc`), and `CurrentChanged`, which the service raises after the project is created, opened, closed, or
  saved.
- **PushToTalkBinding** (the `PushToTalkSetting` of the `Speech` subsystem, which `MainWindowShell` passes) — the text
  of the push-to-talk key in effect, which the hint shows and which follows the settings.
- **ThemeResources** — the surface, line, text, and state colors, the type size, and the check and error icons.
- **CommunityToolkit.Mvvm** — `ObservableObject`, the base of the view model.

#### Callers

- **MainWindowShell** creates the view model over its workspace, its talk key, and its project service, exposes it as
  its `StatusBar` property, and disposes it.
- **MainWindowView** places the `StatusBarView` at the bottom of the window with the view model as its data context.
