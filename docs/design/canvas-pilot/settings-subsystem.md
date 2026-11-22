## Settings

![Settings Structure](SettingsSubsystemView.svg)

### Overview

The `Settings` subsystem stores the user's settings and lets the user change them. Its boundary starts with settings
values supplied by other subsystems and by the user in the settings dialog, and ends with one versioned JSON file in the
per-user application data folder. It contains three units: `SettingsService`, which owns the settings records and the
file; `SettingsDialog`, the modal window that hosts settings pages; and `AiProviderSettingsPage`, the page for the AI
provider choices. Settings pages for other subsystems, such as speech, are supplied to the dialog by the application, so
the `Settings` subsystem does not depend on them. The same file keeps the layout of the main window (size, place, state,
and the widths of the explorer and chat columns), the appearance (the theme choice and the options of the explorer), and
the time at which each recent project was last opened; the shell reads them when it builds and restores the window. The
Agent subsystem reads the AI provider choices through `ISettingsService`, and checks them and lists the models of the
provider through the `IProviderCheck` and `IProviderModelSource` interfaces that this subsystem owns and that the Agent
subsystem implements; this subsystem depends on neither.

### Interfaces

**Settings Access API**: The in-process interface through which other subsystems read and change settings.

- *Type*: In-process .NET API (`ISettingsService`).
- *Role*: Provider.
- *Contract*: `Current` returns the settings in effect; `Load` reads the file; `Save` replaces the settings
  and writes the file; `Update` applies a transformation to the latest settings and writes the result;
  `Changed` is raised after any of these changes the settings. The settings are the immutable `AppSettings`
  record with `AiProviderSettings`, `SpeechSettings`, and `StyleSettings` sections (the shared style kit folders and the
  default style kit).
- *Constraints*: `Save` and `Update` report a failed write as `IOException` and leave the settings in effect
  unchanged. `Load` never throws for a bad file.

**Settings Page Contract**: The in-process interface through which other subsystems contribute pages.

- *Type*: In-process .NET API (`ISettingsPage`).
- *Role*: Provider.
- *Contract*: A page has a `Title` and an `IsValid` flag, loads its working state from `AppSettings`,
  returns an updated `AppSettings` from `Apply`, and is told once through `OnClosed` when the dialog closes.
  A page view model is named `...ViewModel`, so the view locator finds its `...View`.
- *Constraints*: `Apply` throws `InvalidOperationException` when the page is not valid.

**Settings File**: The local persistent store.

- *Type*: File system.
- *Role*: Consumer.
- *Contract*: One UTF-8 JSON file, `settings.json`, in `%APPDATA%\CanvasPilot` (the platform's application
  data folder). A corrupt file is renamed `settings.json.bad`.
- *Constraints*: File paths are resolved with the `Utilities` subsystem. Problems are reported through the
  `Logging` subsystem.

**Settings Dialog**: The user interface.

- *Type*: Avalonia window (`SettingsDialogView`) shown modally by its caller.
- *Role*: Provider.
- *Contract*: A page list on the left, the selected page on the right, and OK, Cancel, and Apply buttons.
- *Constraints*: OK and Apply are disabled while any page is invalid.

### Design

1. The application composes one `SettingsService` for the per-user folder (or for a test folder) and calls
   `Load` at startup.
2. To show the dialog, the caller builds a `SettingsDialogViewModel` from the service and the pages to host
   (always including `AiProviderSettingsPage`) and shows a `SettingsDialogView` for it.
3. Each page copies the settings in effect into its working state when the dialog opens. Nothing is stored
   until OK or Apply: the dialog then merges every page into the latest `Current` settings in page order and
   calls `Save`.
4. `SettingsService` normalizes the settings, writes a temporary file, and replaces `settings.json` with it,
   then raises `Changed`.
5. When the dialog closes by any route, each page is told through `OnClosed`.

`AiProviderSettingsPage` and the `SettingsDialog` are bound to their views through the application's view
locator, which maps a view model `XxxViewModel` to the view `XxxView` in the same namespace.
