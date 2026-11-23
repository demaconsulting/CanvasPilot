### Welcome

![AppShell Structure](AppShellSubsystemView.svg)

#### Purpose

`Welcome` is the screen that fills the body of the main window while no project is open. It says what the application
is, offers the two ways to start (a new project and an open project), lists the most recent projects as cards that open
them, and, while the AI provider needs something or cannot be used, shows a notice with a button that opens the
settings. The workspace is not built until a project is open.

#### Data Model

**WelcomeViewModel** (`AppShellSubsystem/WelcomeViewModel.cs`): the view model. `MainWindowShell` creates it in its
constructor (the `Welcome` property) and disposes it. It owns nothing of the shell: it follows the recent projects
and the provider pill of the shell.

- *Cards*: `Cards` is a read-only collection of the first `MaximumCards` (six) items of the recent projects list of the
  shell, in the order of the list, which is the most recent opening first. It is filled again whenever the list of the
  shell changes. `HasRecent` and `HasNoRecent` tell whether there is any card; `CountText` is `{shown} of {all}` (such
  as `6 of 8`) when the list holds more projects than the cards show, and an empty string otherwise.
- *Commands*: `NewProjectCommand`, `OpenProjectCommand`, and `OpenSettingsCommand` are the commands of the shell, so the
  buttons do what the menu items do. `OpenSettingsCommand` opens the settings on the AI provider page.
- *Notice*: the view model follows the provider pill of the shell (and the pill that replaces it). `ShowProviderBanner`
  is true while the pill needs attention or is in error, and false while it is checking or ready or when there is no
  pill. `ProviderBannerTitle` is `{provider} is not ready` and `ProviderBannerText` is the tooltip sentence of the pill,
  which is the sentence of the check.
- *Sizes*: `CardPictureWidth` and `CardPictureHeight` (568 by 320 pixels) are twice the size of the picture on a card
  (284 by 160), so that the picture is sharp on screens that have twice the pixels.

**RecentProjectItem** (`AppShellSubsystem/RecentProjectItem.cs`): one recent project, as the Recent Projects menu and
the card show it. It was moved out of `MainWindowShell.cs` and is observable. The shell creates one for each file of the
recent list.

- *Identity*: `Name`; `Header`, the name with each underscore doubled so that a menu shows it as typed; `FilePath`;
  `FolderPath`, the folder of the project file, which the card shows as the place of the project.
- *Words*: `OpenedText` (such as `Opened 2 hours ago`), `PictureCountText` (such as `3 pictures`), `DetailText` (the two
  joined by a middle dot), and `AccessibleName` (`{name}, opened 2 hours ago, 3 pictures`), which is the automation name
  of the card. They are worked out once, in the constructor, from `LastOpened`, `PictureCount`, and a present time that
  may be passed in (the tests pass a fixed one).
- *Commands*: `OpenCommand` opens the project; `RemoveCommand` removes it from the list (null when there is none).
- *Picture*: `Thumbnail`, `HasThumbnail`, and `HasNoThumbnail` (the card shows an icon and the count in place of a
  picture). `SetThumbnail` gives the card a bitmap and disposes the one it had; the item owns the bitmap and disposes it
  with itself.

*The words for the time of opening* (`OpenedTextFor(opened, now)`): no time gives `Opened earlier`. Otherwise the time
is put in the offset of the present and the span to the present decides: under a minute `Opened just now`; under an hour
`Opened N min ago`; under 24 hours on the same calendar day `Opened N hour(s) ago`; then by the number of calendar days:
`Opened today`, `Opened yesterday`, `Opened N days ago` (under 7), `Opened last week` (under 14), `Opened N weeks ago`
(under 30), `Opened last month` (under 60), and after that `Opened ` and the date as `MMM d, yyyy`.

*The words for the number of pictures* (`PictureCountTextFor(count)`): none (or fewer) gives `no pictures yet`, one
`1 picture`, and more `N pictures`.

**ProjectPictureCounter** (`AppShellSubsystem/ProjectPictureCounter.cs`, internal): `Count(projectFolder, filter)` walks
the project folder and returns the number of pictures and the path of the main picture. A picture is a file that the
explorer's image filter accepts. Files are visited in a folder in file name order (ignoring case), then the folders
below it in the same order. A folder that starts with a dot is skipped, and at the top of the project the folders
`attachments` and `styles` are skipped, because they hold the files that the application keeps and not the pictures of
the user. The walk goes at most four folders deep and stops counting at 999, so a large project cannot make the screen
slow. The main picture, which the card shows, is the picture that was changed last (by its last write time, the first by
name when the times are equal) among the pictures at the top of the project folder, because the main picture of a
project is the one drawn last and it composes the pictures of the parts, which are written before it. A project with no
picture at the top shows the picture that was changed last in its folders.

**RecentThumbnailLoader** (`AppShellSubsystem/RecentThumbnailLoader.cs`, internal): draws the picture of each card in
the background and gives it to the card on the user interface thread. `Load(item, picture, projectFolder)` asks the
thumbnail source (the sized `CreateAsync` of `IThumbnailSource`, whose cache key includes the size) for a bitmap of
`CardPictureWidth` by `CardPictureHeight`, remembering the current generation. `Restart` cancels the generation, which
the shell does when it replaces the cards; a bitmap that arrives for a cancelled generation is disposed, not given to a
card that no longer exists. A picture that cannot be drawn is logged as a warning and the card keeps its icon. The shell
creates the loader only when it is given a thumbnail source.

**The shell's part**: `MainWindowShell.RefreshRecentProjects` reads the recent list (which drops files that no longer
exist), and builds the items. Only the first six, which the screen shows, have their pictures counted and drawn; the
others are created with a count of zero and no picture. The time of opening comes from
`AppSettings.RecentProjectOpened`, which the *RecentProjects* unit records when a project is added. The list is ordered
by opening, as before, and is not reordered by time.

**WelcomeView** (`AppShellSubsystem/WelcomeView.axaml` and `.axaml.cs`): a user control, `WelcomeView`, hosted in the
`WelcomeHost` of the window.

- *Backdrop*: a grid with the welcome backdrop token as its background, three soft glow ellipses (violet, cyan, and
  pink, at the glow opacity token, so they are faint in the light variant) and a tile of dots in the tertiary text
  color. All are tokens and none takes a click.
- *Body*: a `ScrollViewer` (vertical scrolling only) holds a grid of three columns, `*`, 48, and `*`, at most 1360 wide,
  with margins of 48 and 32, centered vertically.
- *Left column* (at most 660 wide): the brand mark and the name with the brand gradient on `Pilot`; the tagline
  (`WelcomeTagline`, "Describe a picture." and "Your copilot draws it."); a sentence about what the application makes;
  the buttons `WelcomeNewProjectButton` (the gradient `PrimaryButton`) and `WelcomeOpenProjectButton`
  (`SecondaryButton`), each 192 by 50, and the hint of the keys (`WelcomeShortcutHint`, `Cmd` in place of `Ctrl` on
  macOS); the three steps of "How it works", Describe, Mark, and Share (`WelcomeStepDescribe`, `WelcomeStepMark`,
  `WelcomeStepShare`); and the notice about the AI provider (`WelcomeProviderBanner`) with its title, text, and the
  amber `Open Settings` button (`WelcomeOpenSettingsButton`), visible while `ShowProviderBanner`.
- *Right column* (at most 640 wide): the heading `WelcomeRecentTitle` with the count text `WelcomeRecentCount`; the list
  of cards `WelcomeRecentList`, a wrapping panel, visible while `HasRecent`; and the empty card `WelcomeEmptyCard`
  (icon, "No recent projects yet", a sentence, and `WelcomeEmptyNewProjectButton`), visible while `HasNoRecent`.
- *A card* (`WelcomeRecentCard`): a `Button` of the `CardButton` theme, 300 wide, whose command is `OpenCommand`, whose
  automation name is `AccessibleName`, and whose tooltip is the file path. It holds a picture area 160 high on the stage
  surface (the picture, filled and cropped to the area, or the image icon with the picture count) and the name, the
  folder (cut at the front when it is too long), and the detail line. A key binding makes Delete run `RemoveCommand`,
  and the context menu has `Remove from list` (`WelcomeRecentRemove`) with the same command.

**Keyboard**: the cards are buttons, so Tab, Enter, and Space work as on any button. In the code behind of
`WelcomeView`, a key handler on the list moves the focus with the arrow keys: left and right to the card before and
after, up and down to the card in the row above and below. The number of cards in a row is the number of cards that
start at the height of the first one; a move that would leave the list does nothing. The focus is given with the
directional navigation method, so the focus ring shows.

**Hosting**: `MainWindowView` holds the workspace and the welcome screen as siblings: the workspace host is visible
while a project is open and the welcome host while it is not. The workspace view is created when its host first becomes
visible, so no explorer, preview, or chat view exists before the first project is open, and `WorkspacePlaceholder` and
`WorkspaceHint` exist only while a project is open.

**AutomationIds**: `WelcomeView`, `WelcomeHost`, `WelcomeNewProjectButton`, `WelcomeOpenProjectButton`,
`WelcomeShortcutHint`, `WelcomeTagline`, `WelcomeStepDescribe`, `WelcomeStepMark`, `WelcomeStepShare`,
`WelcomeRecentTitle`, `WelcomeRecentCount`, `WelcomeRecentList`, `WelcomeRecentCard`, `WelcomeRecentRemove`,
`WelcomeEmptyCard`, `WelcomeEmptyNewProjectButton`, `WelcomeProviderBanner`, `WelcomeProviderBannerTitle`,
`WelcomeProviderBannerText`, and `WelcomeOpenSettingsButton`.

**Not done**: the status bar does not tell a user with no project to open or create one; the project file does not
record which picture was shown last, so the card cannot show it.

#### Key Methods

**WelcomeViewModel constructor**: refuses a missing shell, subscribes to the change notifications of the recent list and
of the shell, follows the provider pill, and builds the cards.

**RebuildCards**: empties the cards and fills them with the first six items of the shell's list, then announces
`HasRecent`, `HasNoRecent`, and `CountText`. The items themselves are owned by the shell and are not disposed here.

**FollowPill**: when the shell has another provider pill than the one followed, unsubscribes from the old pill,
subscribes to the new one, and announces the notice again. **OnPillPropertyChanged** announces the notice
(`ShowProviderBanner`, `ProviderBannerTitle`, `ProviderBannerText`) whenever the pill announces a change, because the
pill announces every change as one.

**Dispose**: unsubscribes from the list, the shell, and the pill.

**RecentProjectItem.OpenedTextFor** and **PictureCountTextFor**: the pure functions above. **SetThumbnail**: swaps the
bitmap, announces `Thumbnail`, `HasThumbnail`, and `HasNoThumbnail`, and then disposes the previous bitmap.

**ProjectPictureCounter.Count**: the walk above, recursive over folders with a local function, returning
`(Count, First)`.

**RecentThumbnailLoader.Load**, **Restart**, and **Dispose**: as above; `Dispose` cancels the current generation.

**WelcomeView.OnCardKeyDown**: the arrow key handler above. It marks the event handled so that the arrow keys do not
scroll the page.

#### Error Handling

- A missing shell is refused with an `ArgumentNullException`.
- A project folder that cannot be read (`IOException` or `UnauthorizedAccessException`) counts as holding no pictures
  from that folder on; the card still opens the project, and a missing folder gives a count of zero.
- A picture that cannot be drawn (`IOException` or `InvalidOperationException`) is logged as a warning, and the card
  keeps its icon. A drawing that is cancelled, because the cards were replaced, is silent, and its bitmap is disposed.
- A project that is opened from a card fails or succeeds as any project that is opened from the menu does: the shell
  reports a missing or damaged file in a message dialog.
- A card with no `RemoveCommand` ignores the Delete key (the key binding has a null command).

#### Dependencies

- **MainWindowShell** — the recent list, the provider pill, and the new, open, and settings commands; it also creates
  the `RecentProjectItem` objects and the `RecentThumbnailLoader` and owns the thumbnail source.
- **ProviderPill** — the state, the provider name, and the sentence of the check, for the notice.
- **ProjectExplorer** — `IImageSourceFilter`, which tells which files are pictures, and `IThumbnailSource`, which draws
  a picture of a given size (implemented by the thumbnail provider of the preview subsystem).
- **RecentProjects** — records the time of each opening, which the cards tell in words.
- **LoggingSubsystem** — `RollingFileLogger`, to log a picture that could not be drawn.
- **ThemeResources** — the backdrop, glow, brand gradient, surface, text, and state tokens, the icons, and the
  `PrimaryButton`, `SecondaryButton`, and `CardButton` control themes.
- **Avalonia** — `Bitmap`, the focus navigation, and the visual tree used by the arrow key handler.
- **CommunityToolkit.Mvvm** — `ObservableObject` and `IAsyncRelayCommand`.

#### Callers

- **MainWindowShell** creates and disposes the view model and the items, and uses `ProjectPictureCounter` and
  `RecentThumbnailLoader` when it refreshes the recent projects.
- **MainWindowView** hosts `WelcomeView` in `WelcomeHost` and uses the recent items for the Recent Projects menu.
