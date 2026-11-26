### StyleSettingsPage

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StyleSettingsPage` is the "Style Kits" page of the settings dialog. It edits two things that the settings keep about
styles: the **shared folders**, in which a team keeps style kits and which are read in the order of the list (a kit in a
folder that is higher in the list wins over the same kit in a folder below it), and the **default kit**, the kit that a
new project starts with. It is a settings page in the sense of `ISettingsPage`: it edits a working copy and merges only
the `Styles` member into the settings when the user applies the dialog. The unit is a view model
(`StyleSettingsPageViewModel`) with a small value for each row of the list (`StyleFolderItem`) and for each choice of
the default kit (`StyleKitChoice`), and a view (`StyleSettingsPageView`) that the view locator finds.

#### Data Model

- `Folders`: the rows of the list, one `StyleFolderItem` for each folder: the path as the user entered it, a status
  text, and whether the path is a full path. The page keeps the paths in one list, which is the truth, and makes the
  rows from it.
- The status text of a row says: that the path is not a full path (so it cannot be saved); that the catalog has not read
  the folder yet (a folder that was just added, which is read after the dialog is applied); that the folder is
  reachable, with the number of kit folders in it that cannot be used when there are some; or that it is not reachable
  now, with the reason the catalog gave, and that it stays in the list and is tried again. The catalog tells this in
  `IStyleKitCatalog.Locations`, and the page matches a row to the place by the full path without a closing separator,
  without regard to case on Windows. The text of a row (`ToString`) is its path, the first line it shows, because a
  screen reader names a row of two lines by the text of its item; the status stays readable as the second line.
- `SelectedFolder`: the row that the buttons act on. `NewFolderText`: the path that the user is typing. `FolderError`
  and `HasFolderError`: why the last folder was not added.
- `DefaultKitChoices`: no style (the id is an empty string), then each kit of the catalog that is not hidden by a kit of
  a more local place, with the place in brackets, and last, only when the settings name a default kit that no kit has,
  that id with `(not found)`. `SelectedDefaultKit`: the choice that is selected, which is the default of the settings
  until the user chooses another. The text of a `StyleKitChoice` (`ToString`) is its label, because a closed drop-down
  list tells a screen reader the text of its chosen item, and the text of a record would print its members; the label of
  a missing kit already says `(not found)`, so the announced words are the shown words.
- `IsValid`: true while every folder in the list is a full path.
- `Title` is `Style Kits`, `Description` the sentence `Where shared style kits are kept, and which kit a new project
  starts with.`, and `IconKey` is `CpIconBraces`, which the settings dialog shows in its page list and under the title
  of the page.

#### Key Methods

**Load**: Takes the folders and the default kit from the settings, clears the typed text and the message, makes the rows
and the choices, and starts to follow the catalog. A folder in the settings that is not a full path is shown, with the
status that says so, and makes the page invalid. The settings service drops such a folder when it normalizes the
settings, so this arises only when a page is loaded with settings that did not come from the service.

**Adding**: `AddTypedFolderCommand` adds the folder that is typed or pasted, which is the way to name a network folder.
`BrowseFolderCommand` asks the folder picker that the window supplies (starting in the selected folder when it exists)
and adds the folder that is chosen; it does nothing when the user cancels or when there is no picker yet. A folder is
added at the end of the list and selected. A text that is not a full path (the rule of the settings: no character that a
path cannot have, and `Path.IsPathFullyQualified`) and a folder that is in the list already, in any spelling that names
the same folder, are not added and a message says why. The typed text stays so that the user can correct it.

**Removing and moving**: `RemoveFolderCommand`, `MoveFolderUpCommand`, and `MoveFolderDownCommand` act on the selected
folder. Move up and down are usable only where there is a place to move to; moving keeps the folder selected, and
removing selects the folder that took its place.

**Apply**: Throws `InvalidOperationException` while a folder is not a full path, which the dialog does not let happen
because it applies only while every page is valid. Otherwise it returns the settings that it was given with the `Styles`
member replaced by the folders in their order and the default kit; every other member is the one that it was given. A
folder that cannot be reached now is kept. A default kit that no kit has is kept, so that saving the settings does not
lose the choice of a kit that is on another machine.

**Following the catalog**: Between `Load` and `OnClosed` the page listens to the catalog and, on the thread that owns
the user interface, makes the rows and the choices again, so the status of the folders and the kits that can be the
default are current when the catalog has read them, which is after the dialog is shown when it is opened early.
`OnClosed` stops listening, and a later `Load` starts again.

**Threading**: The catalog announces a change on the thread that read the kits, a worker thread. The page has an owner
context, the `SynchronizationContext` that its constructor is given, or the one of the thread that makes it when it is
given none; in the application that is the context of the user interface thread, as the page is made there. A change
that arrives on another thread is posted to the owner context, so the rows and the choices change only on the owner
thread. A page that has no owner context (one made on a thread without a context, as in a test) has no thread to post to
and follows the change on the announcing thread. For that case a lock keeps the rebuild that a change causes apart from
`Load` and `OnClosed`: `Load` takes the settings and rebuilds the rows and the choices inside the lock, and a change
that arrives meanwhile waits and rebuilds after it, so two rebuilds never clear and fill the lists at the same time. The
selection of the default kit is taken from the list of choices that the rebuild has just made, which always has the
default (or no style), not from the shown list. The commands of the page are for the owner thread only.

**View**: `StyleSettingsPageView` shows two raised cards, each with a section title. The card `Shared kit folders` has a
sentence about the order, the list of folders (`StyleSharedFolders`, named `Shared kit folders`) with the status under
each path in the hint look, the buttons `StyleFolderUpButton`, `StyleFolderDownButton`, and `StyleFolderRemoveButton`,
the text box `StyleNewFolderTextBox` (named `New shared folder path`, with a placeholder that shows a full path) with
`StyleFolderAddButton` and `StyleFolderBrowseButton`, and the message `StyleFolderError` in the error look. The card
`Default kit for new projects` has the combo box `StyleDefaultKitComboBox` (named after the card) and a hint. All
colors come from the shared styles of the controls, none is written in the view.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing catalog or function that gives the picker, and `Load` and
`Apply` for missing settings. A folder that is refused is a message in `FolderError`, not an exception.

#### Dependencies

- **StyleKitCatalog** (through `IStyleKitCatalog`) - the status of the shared folders, the kits that can be the default,
  and the announcement of a change.
- **StylePicker** unit - the words for the places of kits (`PlaceText`).
- **Settings** subsystem - `ISettingsPage`, `AppSettings`, and `StyleSettings`.
- **Project** subsystem - `IFolderPicker`, the platform folder picker that the main window implements.
- **CommunityToolkit.Mvvm**, **Avalonia** - notification, commands, and the view.
- **.NET** - `SynchronizationContext`, the owner context to which a change of the catalog is posted, and `Lock`, which
  keeps the rebuilds apart when there is no owner context.

#### Callers

- `SettingsDialogViewModel` - hosts the page with the other pages, loads it, applies it, and tells it when the dialog
  closed.
- `ApplicationComposition` - registers the page after the Speech page, with a function that gives the folder picker of
  the main window once the window has attached itself.
