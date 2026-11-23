### StatusBar

#### Verification Approach

`StatusBar` is verified at two levels. Unit tests in `StatusBarTests.cs` drive the view model over a real workspace, a
real explorer and preview pane on a temporary project folder, and a hand-written fake render service whose results the
test completes (success with a size and a note, or failure), so every state of the bar is reached on demand. Unit tests
in `StatusBarSavedTimeTests.cs` drive the bar over a real project service on a temporary folder with a clock that the
test sets, so the time that the bar shows for a save is exact; `PushToTalkHintsTests.cs` drives a real shell over a real
settings service and checks that the talk key that the bar shows follows the settings. A headless UI test,
`MainWindowView_StatusBar_AlwaysShownAndReportsThePicture` in `MainWindowViewTests.cs` of the main window, shows the
real window with the real `StatusBarView`, finds the controls by automation identifier, and checks what the user sees:
the height, the visibility of each group, the texts, and the automation name of the status panel. The use of a screen
reader (the polite announcement when the sentence changes) is a property of the view and is checked manually with a
screen reader before a release; the tests check the sentence that it would read.

#### Test Environment

Unit tests use the standard test environment with temporary folders. The UI test runs under the Avalonia headless
platform with the Skia renderer, serialized with test parallelization disabled; no display is required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- With no project open the bar is shown, 26 pixels high, idle, with the words "No project open", and without file, size,
  and project facts; the talk key is `F9`.
- With a project open and no file selected the bar is idle with "No picture selected", `HasProject` is true, nothing is
  said about saving (the saved text is empty and hidden), and the change is announced.
- The saved text is empty while no project is open or the project file has no saved time (a new project); after the
  project is saved it is "Project file saved" followed by the time of the save, as the time of day for today and as the
  date and the time for another day, in the culture and the local time zone of the user; the change is announced when
  the project is saved or closed; a bar made without a project service says nothing about saving.
- The talk key that the hint shows is the key in effect: `F9` by default, and the key that the user chose after the
  settings change, announced, and never the old `F8`.
- Selecting a file gives the rendering state with "Rendering a.svg..." and the file name; a successful render gives the
  ready state with the file name, `900 × 500 px`, `SVG`, `Rendered in N ms`, and `1 note` when the render has one note;
  a failed render gives the failed state with "b.svg: render failed", the file name, and no size and no notes.
- Taking the preview pane away makes the bar idle, and putting it back makes it follow it again.
- A disposed bar announces nothing, and a missing workspace is refused.
- In the window the ready group shows the file name, size, format, and render time of a real picture, the failed group
  shows for a broken file and the ready group does not, the saved text is hidden until the user saves the project and
  then shows the "Project file saved" words, and the automation name of the status panel is the sentence of each state.

#### Test Scenarios

**StatusBar_NoProject_IsIdleAndSaysNoProjectIsOpen**: A bar over a workspace with no project is idle with the summary
"No project open", reports that no project is open, and has no file or size text; the key name is `F9`. This scenario is
tested by `StatusBar_NoProject_IsIdleAndSaysNoProjectIsOpen`.

**StatusBar_ProjectOpenNoSelection_SaysNoPictureSelected**: The project is opened; the bar stays idle with the summary
"No picture selected", reports a project, has no saved text (the project was only opened, so nothing is claimed), and
has announced a change of all properties. This scenario is tested by
`StatusBar_ProjectOpenNoSelection_SaysNoPictureSelected`.

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
time, the saved text is hidden while the project has only been opened, and the automation name is
`red.svg: 40 x 20 px, svg`; after the broken file is
selected the failed group shows, the ready group does not, and the name is `broken.svg: render failed`; after the user
runs Save the saved text is visible and starts with "Project file saved". This scenario is
tested by `MainWindowView_StatusBar_AlwaysShownAndReportsThePicture`.

**StatusBar_SavedText_FollowsTheModifiedTimeOfTheOpenProject**: Over a real project service and a clock that the test
sets, the saved text is empty with no project and for a project that was just created (its file has no saved time);
after the project is saved 95 minutes later it is "Project file saved" and the time of that save, and it is empty again
after Close; each change announces the saved text and whether there is one. This scenario is tested by
`StatusBar_SavedText_FollowsTheModifiedTimeOfTheOpenProject`.

**StatusBar_FormatSavedTime_TimeOfDayForTodayAndDateOtherwise**: A save at noon UTC is written, in the `en-US` culture,
as the short time of day when it is seen the same local day, and as the general short date and time when it is seen two
days later, in the local time zone; the two texts differ. This scenario is tested by
`StatusBar_FormatSavedTime_TimeOfDayForTodayAndDateOtherwise`.

**StatusBar_WithoutProjectService_SaysNothingAboutSaving**: A bar made over a workspace only, without a project service,
has an empty saved text and no saved text to show. This scenario is tested by
`StatusBar_WithoutProjectService_SaysNothingAboutSaving`.

**PushToTalkHints_StatusBarAndWelcome_FollowTheSettings**: A shell over a real settings service shows `F9` in the status
bar and on the welcome screen ("Type a request, or hold F9 and speak it. The copilot draws the picture.") and in its
talk key; after the settings take the key `Ctrl+Shift+Space` the status bar and the welcome screen show it, each
announcing the change of its text, and the welcome sentence no longer contains `F8`. This scenario is tested by
`PushToTalkHints_StatusBarAndWelcome_FollowTheSettings`.

The requirements are verified by these scenarios as follows: *AlwaysShown* by the window scenario; *SayNothingToShow* by
the no-project, the no-selection, and the window scenarios; *ReportThePicture* and *ReportRenderingAndFailure* by the
render and the window scenarios; *FollowTheWorkspace* by the no-selection, the replaced-pane, and the disposed-bar
scenarios; *ShowProjectHints* by the no-project, the no-selection, the saved-time, the talk-key, and the window
scenarios; and *AnnounceToScreenReader* by the render and the window scenarios.
