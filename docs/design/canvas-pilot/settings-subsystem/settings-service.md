### SettingsService

![Settings Structure](SettingsSubsystemView.svg)

#### Purpose

`SettingsService` owns the application settings. It reads and writes the versioned per-user settings file, keeps the
settings in effect, and announces changes. A bad, missing, or newer file never stops the application. The `AppSettings`,
`AiProviderSettings`, `SpeechSettings`, `StyleSettings`, `AppearanceSettings`, and `WindowSettings` records and
`ISettingsService` interface belong to this unit.

#### Data Model

**AppSettings**: `record` — the whole document: `Version` (`int`, currently 1), `RecentProjects`
(`IReadOnlyList<string>`, most recent first), `AiProvider` (`AiProviderSettings`), `Speech`
(`SpeechSettings`), `Styles` (`StyleSettings`), `Appearance` (`AppearanceSettings`), `Window` (`WindowSettings`), and
`RecentProjectOpened` (`IReadOnlyDictionary<string, DateTimeOffset>`, the time at which each listed project was last
opened, by the same path as in `RecentProjects`). Immutable; changed with `with` expressions.

**AiProviderSettings**: `record` — `Provider` (`AiProviderKind`: `GitHubCopilot` or `Ollama`), `ModelName` (`string`,
the id of the model as the provider names it; empty for the provider default), `ContextWindowTokens` (`int`, 512 to
2,000,000, default 32768 through `AiProviderSettings.DefaultContextWindowTokens`; a stored value within bounds, such as
the 8192 that an earlier build used as its default, is kept; `AiProviderSettings.RecommendedMinimumContextTokens`,
16384, is the smallest value that is recommended and is not enforced here), `ReasoningEffort` (`string`, the id of the
reasoning effort as the provider spells it, such as `none`, `low`, `medium`, `high`, `xhigh`, or `max`; empty to leave
the choice to the provider; it is text because the efforts that exist depend on the model), `OllamaEndpoint` (`string`,
default `http://localhost:11434`), `UnrestrictedAgentTools` (`bool`, default `false`), and `CopilotCliPath` (`string`,
empty for automatic discovery).

**SpeechSettings**: `record` — `CaptureDevice`, `PlaybackDevice`, `SttModelId`, and `TtsModelId` (`string`,
empty for the default), `PushToTalkKey` (`string`, the key or chord that is held to dictate, written as `F9` or
`Ctrl+Shift+Space`; empty, the default, means F9), and `ModelParameters` (model identifier, then parameter identifier,
then the value as an invariant-culture string).

**Talk key**: `PushToTalkKey` is an additive part of the document: an earlier build ignores it and a file of an earlier
build has none, so the version of the file stays 1 and the default F9 is in effect. The rule that decides what is a
safe key is `PushToTalkBinding.Normalize` of the `Speech` subsystem (see *PushToTalkBinding Design*): the service does
not repeat it. The old default F8, which many laptops never deliver to the window, is not a safe key any more, so a file
that holds it is read as the default and is rewritten with an empty value by the next save, and a user who had left the
default therefore gets F9. The default is stored as nothing, so that it can follow a later release.

**StyleSettings**: `record` — `SharedKitFolders` (`IReadOnlyList<string>`, the full paths of the folders in which a team
shares style kits, in order of precedence with an earlier folder winning) and `DefaultKitId` (`string`, the id of the
kit that a new project starts with, empty for none). `StyleSettings.IsValidKitId` is the rule of a kit id (1 to 48
lower-case letters, digits, and dashes that start with a letter or a digit), kept here as a plain check because the
settings subsystem does not depend on the style subsystem.

**AppearanceSettings**: `record` — `Theme` (`ThemeChoice`: `FollowSystem`, `Dark`, or `Light`; default `FollowSystem`),
`ShowThumbnails` (`bool`, default `true`), and `ComfortableRows` (`bool`, default `false`).

**WindowSettings**: `record` — the layout of the main window: `Width` and `Height` (`int`, default 1440 by 900, at least
960 by 600 and at most 16384), `Left` and `Top` (`int?`, the remembered place of the window, `null` to let the system
place it; a value beyond 32767 in either direction is forgotten), `State` (`SavedWindowState`: `Normal` or `Maximized`),
`ExplorerWidth` (`int`, default 264, 180 to 420), and `ChatWidth` (`int`, default 400, 320 to 640). The bounds are
constants of the record. The place is not tested against the screens here, because the settings do not know them; the
window clamps it when it restores itself.

**Recent project times**: `RecentProjectOpened` is an additive part of the document: an earlier build ignores it and a
file of an earlier build has none, so the version of the file stays 1. A project that is listed but has no time is
shown without one.

**File format**: a camel-case JSON object with the same names, for example
`{"version":1,"recentProjects":[],"aiProvider":{"provider":"GitHubCopilot",...},"speech":{...}}`. Enumerations
are written as names. Reading is case-insensitive, skips comments, allows trailing commas, ignores unknown
properties, and treats an unrecognized or non-text enumeration value as the first member.

**Reasoning effort and the old reasoning level**: builds before the model list stored `aiProvider.reasoningLevel`, an
enumeration of `Auto`, `Low`, `Medium`, and `High` that nothing acted on (the page showed it disabled). The version of
the file stays 1, because the change adds a key and an earlier build ignores it. When a file has no `reasoningEffort`
(or an empty one), the reader takes the old key: `low`, `medium`, and `high`, in any letter case, become the effort of
the same name; `auto`, any other text, and a value that is not text give no effort. A `reasoningEffort` that the file
states wins over the old key. The old key is never written again, so the next save drops it. A build from before this
change that reads a newer file ignores `reasoningEffort` and shows no level.

**SettingsFolder / SettingsFilePath**: `string` — the folder given to the constructor and `settings.json`
inside it, resolved with `PathHelpers.SafePathCombine`.

**DefaultSettingsFolder**: `string` (static) — `%APPDATA%\CanvasPilot` on Windows, using the per-user
application data folder reported by the platform. When the platform reports none, the user profile folder is
used, and the current directory when no user profile is reported either.

**Current**: `AppSettings` — the settings in effect; defaults until `Load` is called.

#### Key Methods

**Load**: Reads the settings file and makes the result the settings in effect.

- *Parameters*: none.
- *Returns*: `AppSettings` — the settings now in effect.
- *Preconditions*: none; the file may be absent.
- *Postconditions*: `Current` holds the normalized settings, or defaults when the file is missing,
  unreadable, empty, or corrupt; `Changed` has been raised.

Algorithm: when the file does not exist, use defaults. Read the text; an `IOException` or
`UnauthorizedAccessException` is logged and defaults are used. Deserialize; a `JsonException` or a null result
marks the file corrupt, so it is moved to `settings.json.bad` (replacing any earlier `.bad` file), an error is
logged, and defaults are used. When the file's version is newer than the supported version, log a warning and
use the known values. Migrate the old reasoning level (see the file format), then normalize the result.

**Normalize**: Makes parsed or caller-supplied settings safe to use. Sets the version to the current version; replaces
null sections, strings, and dictionary entries with defaults; drops blank recent-project entries; replaces a context
window outside its bounds and an endpoint that is not an absolute `http` or `https` URL with their defaults; trims text
values (the model name and the reasoning effort among them); keeps the shared style kit folders that are full paths,
each once (ignoring case, in the first spelling) and in their order, drops a relative, blank, or invalid entry with a
warning in the log, and keeps a folder that does not exist because a network folder may be back later; and replaces a
default style kit id that no kit can have with none. For the window layout it clamps the size and the widths of the
columns to their bounds (a value outside is brought to the nearest bound, not reset), forgets a place that lies beyond
32767, and treats an unknown state as `Normal`; an unknown theme choice becomes `FollowSystem`; it replaces the talk key
by its normal form (`PushToTalkBinding.Normalize`: a safe key or chord is written in the order `Ctrl+Alt+Shift` and the
key, and a missing value, a key that is not safe, or the old default F8 becomes the empty string, which means the
default); and it keeps the times of opening only for paths that are in the list of recent projects (ignoring case).
Because `Save` and `Update` normalize too, a talk key that a caller writes is cleaned in the same way before it is
stored and announced, so the window never receives a key that would take away typing.

**Save**: Replaces the settings in effect and writes them.

- *Parameters*: `AppSettings settings` — the new settings.
- *Returns*: `void`.
- *Preconditions*: `settings` is not null.
- *Postconditions*: The settings are normalized, stored in `settings.json`, in effect, and announced through
  `Changed`; or an `IOException` is thrown and nothing changed.

Algorithm: normalize; create the folder; write the JSON (UTF-8, no byte order mark, indented) to a uniquely
named temporary file in the same folder; move the temporary file over `settings.json`; update `Current`. On
failure, delete the temporary file, log an error, and throw `IOException` (an
`UnauthorizedAccessException` is wrapped).

**Update**: Applies a function to `Current` and saves the result under the same lock, so concurrent updates
do not lose each other's changes. Returns the settings now in effect.

**CreateDefault**: Creates a service for `DefaultSettingsFolder`.

#### Error Handling

The constructor throws `ArgumentException` for a null or whitespace folder. `Save` and `Update` throw
`ArgumentNullException` for a null argument and `IOException` when the file cannot be written. `Load` never
throws for a bad file; it logs and uses defaults. Failures to write a log entry are ignored so that a logging
problem never blocks access to settings. A save never leaves a partly written `settings.json`, because the
file is replaced only after the temporary file has been written completely.

#### Dependencies

- **PathHelpers** — resolves the settings file and temporary file paths inside the settings folder.
- **PushToTalkBinding** (of the `Speech` subsystem) — the rule that cleans the stored talk key when the settings are
  normalized.
- **RollingFileLogger** — optional; records missing, unreadable, corrupt, and newer files and failed saves.
- **System.Text.Json** and **System.IO** — serialization and file system primitives.

#### Callers

- `SettingsDialog` — reads `Current` and calls `Save`.
- The application composition root — creates the service and calls `Load`.
- Other subsystems that persist or read settings, such as the recent projects list, use `Current`, `Update`,
  and `Changed`. The `PushToTalkSetting` of the `Speech` subsystem reads the talk key from `Current` and follows
  `Changed`.
