### StatusBar

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`StatusBar` is the bar at the bottom of the main window. It follows the workspace and its preview pane and says what
picture is shown and in what state it is, with words as well as with an icon, and, while a project is open, that the
project is saved and which key is held to talk. The bar is always there; with no project open it says so.

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
- *Project*: `HasProject` mirrors the workspace. `ProjectSavedText` is the constant "Project saved" and `TalkKeyText` is
  the name of the push-to-talk key of `MainWindowShell`, `F8`.

The inputs of the facts are the view model properties `FileName`, `ImageSizeText`, `FormatText`, `RenderTimeText`,
`RenderMilliseconds`, `StatusText`, `HasDiagnostics`, and `DiagnosticsHeader` of the preview pane, which the
*PreviewPane* unit documents.

**"Project saved" is static text.** The application has no dirty tracking: no state of the project says that there are
unsaved changes. The words are therefore shown whenever a project is open and are not bound to any state of the project.
If dirty tracking is ever added, the words are to be bound to it.

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
- *Right side*: shown only while `HasProject`; the check icon with the "Project saved" words (`StatusBarProjectSaved`)
  and the hint "Hold F8 to talk" (`StatusBarTalkHint`), in which the key name is drawn as a key cap.
- *Screen reader*: the `PreviewStatus` panel has the `Summary` sentence as its automation name and the live setting
  `Polite`, so a screen reader is told when the sentence changes without being interrupted.

**AutomationIds** that a tester needs: `StatusBar` (the border inside the view), `PreviewStatus` (the panel, which moved
here from the preview pane view, whose automation name is the summary sentence), `StatusBarReady`, `StatusBarRendering`,
`StatusBarFailed`, `StatusBarIdle`, `StatusBarFile`, `StatusBarSize`, `StatusBarFormat`, `StatusBarRenderTime`,
`StatusBarNotes`, `StatusBarProjectSaved`, and `StatusBarTalkHint`.

#### Key Methods

**Constructor**: refuses a missing workspace, subscribes to the property changes of the workspace, and follows the
preview pane that the workspace holds.

**FollowPreview**: compares the preview of the workspace with the one that is followed. When they differ it drops the
subscription to the old pane and subscribes to the new one. A pane that is not a `PreviewPaneViewModel` is treated as no
pane.

**OnWorkspacePropertyChanged**: when the workspace announces a change of `Preview` or `HasProject`, follows the pane
again and announces a change of every property (an empty property name), because every part of the bar depends on the
same few facts.

**OnPreviewPropertyChanged**: announces a change of every property whenever the followed pane changes anything.

**Dispose**: unsubscribes from the workspace and from the pane and drops the pane. After it nothing is announced.

#### Error Handling

The view model has no operation that can fail. A missing workspace is refused with an `ArgumentNullException`. A render
that fails is not an error of the bar: it is one of its states, shown with the words "Render failed" and the danger
color, and the sentence of the pane is passed to the screen reader.

**Known limitations**: The text "Project saved" is fixed while a project is open. The application keeps no record of
unsaved changes, so the bar cannot tell when the project has some. The sentence of the marking tools is not part of the
bar; it is read out from the overlay of the picture.

#### Dependencies

- **ProjectWorkspace** — `ProjectWorkspaceViewModel` supplies `HasProject`, the `Preview` pane, and their change
  notifications.
- **PreviewPane** — `PreviewPaneViewModel` supplies the busy, error, and image flags, the file name, the size, the
  format, the render time, the sentence, and the diagnostics header.
- **MainWindowShell** — the name of the push-to-talk key, which the hint shows.
- **ThemeResources** — the surface, line, text, and state colors, the type size, and the check and error icons.
- **CommunityToolkit.Mvvm** — `ObservableObject`, the base of the view model.

#### Callers

- **MainWindowShell** creates the view model, exposes it as its `StatusBar` property, and disposes it.
- **MainWindowView** places the `StatusBarView` at the bottom of the window with the view model as its data context.
