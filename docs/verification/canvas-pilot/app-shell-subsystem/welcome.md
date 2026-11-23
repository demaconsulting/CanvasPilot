### Welcome

#### Verification Approach

`Welcome` is verified at two levels. Unit tests in `WelcomeTests.cs` test the words of a card (`RecentProjectItem`) with
a fixed present time, count the pictures of a project (`ProjectPictureCounter`) over real files in a temporary folder
with a fake image filter, and drive the view model over a real main window shell, real recent projects, and real
settings on temporary folders; for the notice about the AI provider, a real chat panel and provider pill are built over
a hand-written controlled agent whose readiness the test chooses. Headless UI tests in `WelcomeViewTests.cs` show the
real `MainWindowView` over a real composition on temporary folders, find controls by automation identifier, and click,
press keys, and read what the user sees. The window tests in `MainWindowViewTests.cs` check that the welcome screen
comes back when a project is closed and that the workspace is not built before a project is open. The pictures on the
cards are drawn by the thumbnail source and are not asserted by these tests (they are seen in the screenshot of the
welcome screen); the loader that gives them to the cards has no test of its own. The real file pickers cannot be shown
headless and are not part of these tests.

#### Test Environment

Unit tests use the standard test environment with temporary folders. UI tests run under the Avalonia headless platform
with the Skia renderer, serialized with test parallelization disabled; no display is required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- The time of opening is told as `Opened just now`, `Opened 5 min ago`, `Opened 1 hour ago`, `Opened 2 hours ago`,
  `Opened yesterday`, `Opened 3 days ago`, `Opened last week`, `Opened 3 weeks ago`, `Opened last month`, and a date for
  a time further back, and as `Opened earlier` for no time; the number of pictures as `no pictures yet`, `1 picture`,
  and `3 pictures`.
- A card has the detail line `Opened 2 hours ago · 3 pictures`, the name for a screen reader
  `Release, opened 2 hours ago, 3 pictures`, the folder of its project, and no picture until it is given one.
- The pictures of a project are counted in its folders without the attachments, the style kits, the hidden folders, and
  the files that are not pictures; the first is the first by name; a missing folder has none.
- With eight recent projects the screen shows the six most recent in order and says `6 of 8`; with none it has no card
  and asks for a first project.
- Removing a card takes the project off the list and leaves its files.
- The notice shows only while the provider needs something or cannot be used, with the provider name and the sentence of
  the check, and is hidden while checking and when ready; its button runs the settings command.
- A window with no project shows the welcome screen and not the workspace, with the two start buttons, the three steps,
  and the empty card; the buttons run the commands of the menu; the cards list the projects newest first with their
  words; a click on a card opens the project and shows the workspace; the workspace and the chat column do not exist
  before a project is open.
- The right arrow moves the focus to the next card, and Delete removes the focused card from the list and not from the
  disk.

#### Test Scenarios

**RecentProjectItem_OpenedTextFor_TellsTheTimeInWords**: For ten spans from no time ago to a hundred days ago, and for
no time at all, the words are as listed in the acceptance criteria. This scenario is tested by
`RecentProjectItem_OpenedTextFor_TellsTheTimeInWords`.

**RecentProjectItem_PictureCountTextFor_UsesThePlural**: Zero, one, and three pictures are told as `no pictures yet`, `1
picture`, and `3 pictures`. This scenario is tested by `RecentProjectItem_PictureCountTextFor_UsesThePlural`.

**RecentProjectItem_New_GivesDetailLineAccessibleNameAndFolder**: A card for a project opened 2 hours ago that holds 3
pictures has the detail line, the accessible name, and the folder, and no picture. This scenario is tested by
`RecentProjectItem_New_GivesDetailLineAccessibleNameAndFolder`.

**ProjectPictureCounter_Count_CountsPicturesAndLeavesOutAttachmentsStylesAndHiddenFolders**: A project with two pictures
at the top, one in a folder, a text file, and pictures in `attachments`, `styles`, and a hidden folder is counted as
three, with `a.svg` first. This scenario is tested by
`ProjectPictureCounter_Count_CountsPicturesAndLeavesOutAttachmentsStylesAndHiddenFolders`.

**ProjectPictureCounter_Count_MissingFolder_HasNoPictures**: A folder that does not exist has no pictures and no first
picture, and does not fail. This scenario is tested by `ProjectPictureCounter_Count_MissingFolder_HasNoPictures`.

**WelcomeViewModel_Cards_ShowTheSixMostRecentAndCountTheRest**: A shell with no recent project has no card and has
`HasNoRecent`; after eight projects are added it has six cards, `p8` to `p3`, the count text `6 of 8`, and eight recent
projects in the shell. This scenario is tested by `WelcomeViewModel_Cards_ShowTheSixMostRecentAndCountTheRest`.

**WelcomeViewModel_Card_RemoveCommand_RemovesTheProjectFromTheListOnly**: The remove command of the only card empties
the cards and leaves the project file. This scenario is tested by
`WelcomeViewModel_Card_RemoveCommand_RemovesTheProjectFromTheListOnly`.

**WelcomeViewModel_ProviderBanner_FollowsTheCheckOfTheProvider**: With a provider pill over a copilot that is not signed
in, no notice shows while checking; after the check it shows with `Copilot is not ready` and the sentence of the check;
after a ready answer it is hidden; the change is announced and the settings command is the one of the shell. This
scenario is tested by `WelcomeViewModel_ProviderBanner_FollowsTheCheckOfTheProvider`.

**WelcomeViewModel_Constructor_NullShell_Throws**: A missing shell is refused. This scenario is tested by
`WelcomeViewModel_Constructor_NullShell_Throws`.

**WelcomeView_NoProject_ShowsStartButtonsStepsAndEmptyCard**: A window with no project shows the welcome host and not
the workspace host, the buttons `New project` and `Open project`, the empty card, no list, and the three steps. This
scenario is tested by `WelcomeView_NoProject_ShowsStartButtonsStepsAndEmptyCard`.

**WelcomeView_StartButtons_OpenTheNewProjectDialogAndTheFilePicker**: The buttons run the new project and open project
commands of the shell; running the first opens the New Project dialog, which is cancelled and creates nothing. This
scenario is tested by `WelcomeView_StartButtons_OpenTheNewProjectDialogAndTheFilePicker`.

**WelcomeView_RecentProjects_ListCardsAndOpenAProject**: With two remembered projects the window shows two cards, the
newest first, with the name, the folder, and a line that starts with `Opened ` and ends with `2 pictures`; the names for
a screen reader start with the project names; a click on the first card opens its project, hides the welcome host, and
shows the workspace host. This scenario is tested by `WelcomeView_RecentProjects_ListCardsAndOpenAProject`.

**WelcomeView_Keyboard_ArrowsMoveBetweenCardsAndDeleteRemovesFromList**: With three cards and the focus on the first,
the right arrow moves the focus to the second; Delete removes the second project from the list, which then holds the
other two, and its file stays. This scenario is tested by
`WelcomeView_Keyboard_ArrowsMoveBetweenCardsAndDeleteRemovesFromList`.

**WelcomeView_ProviderNotUsable_ShowsNoticeWhoseButtonOpensSettings**: In a window over an application with no AI
provider the notice is visible with the title `Copilot is not ready`, and its `Open Settings` button opens the settings
dialog. This scenario is tested by `WelcomeView_ProviderNotUsable_ShowsNoticeWhoseButtonOpensSettings`.

**MainWindowView_RecentProject_Open_ListsFilesAndCloseProjectResetsWindow**: After a project is opened from the recent
list and closed, the welcome host is shown again and there is no project. This scenario is tested by
`MainWindowView_RecentProject_Open_ListsFilesAndCloseProjectResetsWindow`.

**MainWindowView_Workspace_ChatColumnVisibleOnlyWithProject_ThreeColumns**: Before a project is open the chat host and
the chat splitter do not exist in the window; after a project is opened they do. This scenario is tested by
`MainWindowView_Workspace_ChatColumnVisibleOnlyWithProject_ThreeColumns`.

The requirements are verified by these scenarios as follows: *ShowWhileNoProject* by the no-project, the
recent-projects, and the close scenarios; *DeferWorkspace* by the workspace scenario; *IntroduceApplication* by the
no-project scenario; *OfferStartButtons* by the no-project and the start-buttons scenarios; *ListRecentProjects* by the
cards, the card words, the recent-projects, and the null-shell scenarios; *TellOpeningTimeInWords* by the time scenario;
*CountPictures* by the plural and the two counter scenarios; *OpenProjectFromCard* by the recent-projects scenario;
*MoveBetweenCardsByKeyboard* by the keyboard scenario; *RemoveCardFromList* by the remove and the keyboard scenarios;
*InviteFirstProject* by the no-project and the cards scenarios; and *NoticeAiProviderProblem* by the banner and the
provider scenarios.
