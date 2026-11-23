### FollowUpSuggestions

#### Verification Approach

`FollowUpSuggestions` and `ProjectDrawingProbe` are verified with unit tests in
`test/DemaConsulting.CanvasPilot.Tests/ChatSubsystem/FollowUpSuggestionsTests.cs`. The planner is a pure function, so
its test calls it directly with every combination of its facts that matters and nothing is replaced. The probe is
internal; one test calls it directly over real temporary folders, and the other tests verify it through the real
`ChatPanelViewModel`, which is the unit's only caller and decides when the search runs, together with the real
`CanvasNetImageSourceFilter` over real temporary project folders (a folder with a drawing in a sub-folder, a folder
whose only picture is in `attachments`, and an empty one), so that what the test sees is what the user is offered.
Around the panel only what has no place in a panel test is replaced: the AI copilot (the controlled `IAgentService` of
`ChatTurnTestSupport.cs`, which ends a turn and reports a written file when the test says so), the clock of the turns
(the manual `ManualTimeProvider`), the source of the shown picture (a fake that the test sets and clears), the dictation
service (a fake), the composer of mark pictures (a substitute), and the builder of sent messages (a small one in the
test class that gives a real payload with the text of the message, which the copilot needs). The tests check what the
panel offers (`Suggestions`, `HasSuggestions`, `ShowSuggestions`) and what it announces.

#### Test Environment

The standard test runner. No display, network, model, or special permission is needed, and no real time passes. A
temporary folder holds the projects and is removed after each test.

#### Acceptance Criteria

- All tests of the unit pass with zero failures.
- A project with no drawing is offered the starter prompts, in order; a project with a drawing is offered the three
  refinements, and the export after them only while a picture is shown; nothing is offered while the copilot works,
  whatever else is true.
- A new panel in an empty project offers the starter prompts and does not show them above the message before there is a
  conversation.
- While a turn runs nothing is offered, and what is offered after it follows what the turn did: the starter prompts when
  it only talked or wrote a file that is not a drawing, the refinements when it wrote a drawing; every change is
  announced.
- A project that already holds a drawing, also in a folder inside it, is offered the refinements when it is opened; a
  picture that is only in the `attachments` folder is not a drawing; each project is judged by its own folder, and a
  drawing that the copilot wrote does not follow the user into another project.
- A picture that is shown gives the project a drawing and adds the export, and the prompts go back to the starters when
  the picture goes away.

#### Test Scenarios

**FollowUpSuggestions_Plan_FollowsDrawingPictureAndBusy**: With no drawing and not busy the planner gives the starter
prompts, which include the flowchart and the title card; with a drawing, no picture shown, and not busy it gives the
refinements; with a drawing and a picture shown it gives the refinements and then the export; and while busy it gives
nothing, with a drawing and a picture and also with none. This scenario is tested by
`FollowUpSuggestions_Plan_FollowsDrawingPictureAndBusy`.

**ChatPanelViewModel_EmptyProject_OffersStarterPrompts**: A panel made in an open, empty project offers the starter
prompts and has suggestions, but does not show them above the message, because there is no conversation yet. This
scenario is tested by `ChatPanelViewModel_EmptyProject_OffersStarterPrompts`.

**ChatPanelViewModel_TurnWritesDrawing_SuggestionsHideWhileBusyThenBecomeRefinements**: After a message that the copilot
only answers, the starter prompts show above the message; while the turn runs none are offered; the same panel then
sends a message during which the copilot reports that it wrote `box.svg`, offers nothing while that turn runs, and when
it has ended offers the refinements without the export, announcing `Suggestions` and `ShowSuggestions`. This scenario is
tested by `ChatPanelViewModel_TurnWritesDrawing_SuggestionsHideWhileBusyThenBecomeRefinements`.

**ChatPanelViewModel_TurnWritesNotes_StillOffersStarterPrompts**: A turn during which the copilot reports that it wrote
`notes.txt` leaves the starter prompts, because a text file is not a drawing. This scenario is tested by
`ChatPanelViewModel_TurnWritesNotes_StillOffersStarterPrompts`.

**ChatPanelViewModel_OpenProject_WithDrawingOffersRefinementsAndEachProjectIsJudgedAlone**: A project with `flow.svg` in
a sub-folder is offered the refinements when it is opened; a project whose only picture is `attachments/photo.svg` is
offered the starter prompts; after the copilot writes `a.svg` in that project it is offered the refinements; and an
empty project that is opened next is offered the starter prompts again, so the drawing that was written does not follow
the user. This scenario is tested by
`ChatPanelViewModel_OpenProject_WithDrawingOffersRefinementsAndEachProjectIsJudgedAlone`.

**ChatPanelViewModel_PictureShown_OffersRefinementsWithExport**: With a picture shown in an empty project the panel
offers the refinements and the export, and when the picture goes away it offers the starter prompts again. This scenario
is tested by `ChatPanelViewModel_PictureShown_OffersRefinementsWithExport`.

**ProjectDrawingProbe_HasDrawing_FindsDrawingsAndLeavesOutWhatIsNotTheirs**: Folders that each hold one file are
searched with the filter of the application: a drawing at the top of a project and a drawing two folders inside another
are found; a folder with a text file, and with a drawing in each of `attachments`, `styles`, and a hidden folder at its
top, holds none; a drawing five folders deep is not looked for; a folder that does not exist holds none. This scenario
is tested by `ProjectDrawingProbe_HasDrawing_FindsDrawingsAndLeavesOutWhatIsNotTheirs`.

**Not covered by an automated scenario of its own**: the treatment of a folder that cannot be read (the search ends
there and finds no drawing) is a rule of `ProjectDrawingProbe` that no test reaches, because a folder cannot be made
unreadable portably. It is small and reads as code in the review of the unit.
