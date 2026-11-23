### StatusBar

#### Verification Approach

`StatusBar` is verified at two levels. Unit tests in `StatusBarTests.cs` drive the view model over a real workspace, a
real explorer and preview pane on a temporary project folder, and a hand-written fake render service whose results the
test completes (success with a size and a note, or failure), so every state of the bar is reached on demand. A headless
UI test, `MainWindowView_StatusBar_AlwaysShownAndReportsThePicture` in `MainWindowViewTests.cs` of the main window,
shows the real window with the real `StatusBarView`, finds the controls by automation identifier, and checks what the
user sees: the height, the visibility of each group, the texts, and the automation name of the status panel. The use of
a screen reader (the polite announcement when the sentence changes) is a property of the view and is checked manually
with a screen reader before a release; the tests check the sentence that it would read.

#### Test Environment

Unit tests use the standard test environment with temporary folders. The UI test runs under the Avalonia headless
platform with the Skia renderer, serialized with test parallelization disabled; no display is required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- With no project open the bar is shown, 26 pixels high, idle, with the words "No project open", and without file, size,
  and project facts; the talk key is `F8`.
- With a project open and no file selected the bar is idle with "No picture selected", `HasProject` is true, the words
  "Project saved" are the saved text, and the change is announced.
- Selecting a file gives the rendering state with "Rendering a.svg..." and the file name; a successful render gives the
  ready state with the file name, `900 × 500 px`, `SVG`, `Rendered in N ms`, and `1 note` when the render has one note;
  a failed render gives the failed state with "b.svg: render failed", the file name, and no size and no notes.
- Taking the preview pane away makes the bar idle, and putting it back makes it follow it again.
- A disposed bar announces nothing, and a missing workspace is refused.
- In the window the ready group shows the file name, size, format, and render time of a real picture, the failed group
  shows for a broken file and the ready group does not, the saved text is visible, and the automation name of the status
  panel is the sentence of each state.

#### Test Scenarios

**StatusBar_NoProject_IsIdleAndSaysNoProjectIsOpen**: A bar over a workspace with no project is idle with the summary
"No project open", reports that no project is open, and has no file or size text; the key name is `F8`. This scenario is
tested by `StatusBar_NoProject_IsIdleAndSaysNoProjectIsOpen`.

**StatusBar_ProjectOpenNoSelection_SaysNoPictureSelected**: The project is opened; the bar stays idle with the summary
"No picture selected", reports a project, has the saved text "Project saved", and has announced a change of all
properties. This scenario is tested by `StatusBar_ProjectOpenNoSelection_SaysNoPictureSelected`.

**StatusBar_RenderSucceedsThenFails_FollowsEveryStateWithItsFacts**: A file is selected and the bar is rendering with
the file name; the render succeeds at 900 by 500 with a warning, and the bar is ready with the file name, size, format,
render time, and one note; another file is selected and its render fails, and the bar is failed with its words and
without the size and the notes. This scenario is tested by
`StatusBar_RenderSucceedsThenFails_FollowsEveryStateWithItsFacts`.

**StatusBar_PreviewReplaced_FollowsTheNewPane**: While a picture is being rendered the pane is taken away and the bar is
idle; the pane is put back and the bar is rendering again. This scenario is tested by
`StatusBar_PreviewReplaced_FollowsTheNewPane`.

**StatusBar_NullWorkspaceThrows_DisposedBarNoLongerFollows**: A disposed bar announces nothing when a project is opened
and a file is selected, and a missing workspace is refused. This scenario is tested by
`StatusBar_NullWorkspaceThrows_DisposedBarNoLongerFollows`.

**MainWindowView_StatusBar_AlwaysShownAndReportsThePicture**: The shown window has the bar at 26 pixels with the idle
group and the words "No project open"; after a project with a picture and a broken file is opened the words are "No
picture selected"; after the picture is selected the ready group shows `red.svg`, `40 × 20 px`, `SVG`, and a render
time, the saved text is visible, and the automation name is `red.svg: 40 x 20 px, svg`; after the broken file is
selected the failed group shows, the ready group does not, and the name is `broken.svg: render failed`. This scenario is
tested by `MainWindowView_StatusBar_AlwaysShownAndReportsThePicture`.

The requirements are verified by these scenarios as follows: *AlwaysShown* by the window scenario; *SayNothingToShow* by
the no-project, the no-selection, and the window scenarios; *ReportThePicture* and *ReportRenderingAndFailure* by the
render and the window scenarios; *FollowTheWorkspace* by the no-selection, the replaced-pane, and the disposed-bar
scenarios; *ShowProjectHints* by the no-project, the no-selection, and the window scenarios; and
*AnnounceToScreenReader* by the render and the window scenarios.
