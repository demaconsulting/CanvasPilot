### SettingsService

![Settings Structure](SettingsSubsystemView.svg)

#### Purpose

`SettingsService` owns the application settings. It reads and writes the versioned per-user settings file, keeps the
settings in effect, and announces changes. A bad, missing, or newer file never stops the application. The `AppSettings`,
`AiProviderSettings`, `SpeechSettings`, and `StyleSettings` records and `ISettingsService` interface belong to this
unit.

#### Data Model

**AppSettings**: `record` — the whole document: `Version` (`int`, currently 1), `RecentProjects`
(`IReadOnlyList<string>`, most recent first), `AiProvider` (`AiProviderSettings`), `Speech`
(`SpeechSettings`), and `Styles` (`StyleSettings`). Immutable; changed with `with` expressions.

**AiProviderSettings**: `record` — `Provider` (`AiProviderKind`: `GitHubCopilot` or `Ollama`), `ModelName` (`string`,
empty for the provider default), `ContextWindowTokens` (`int`, 512 to 2,000,000, default 32768 through
`AiProviderSettings.DefaultContextWindowTokens`; a stored value within bounds, such as the 8192 that an earlier build
used as its default, is kept; `AiProviderSettings.RecommendedMinimumContextTokens`, 16384, is the smallest value that is
recommended and is not enforced here), `ReasoningLevel` (`Auto`, `Low`, `Medium`, `High`), `OllamaEndpoint` (`string`,
default `http://localhost:11434`), `UnrestrictedAgentTools` (`bool`, default `false`), and `CopilotCliPath` (`string`,
empty for automatic discovery).

**SpeechSettings**: `record` — `CaptureDevice`, `PlaybackDevice`, `SttModelId`, and `TtsModelId` (`string`,
empty for the default), and `ModelParameters` (model identifier, then parameter identifier, then the value
as an invariant-culture string).

**StyleSettings**: `record` — `SharedKitFolders` (`IReadOnlyList<string>`, the full paths of the folders in which a team
shares style kits, in order of precedence with an earlier folder winning) and `DefaultKitId` (`string`, the id of the
kit that a new project starts with, empty for none). `StyleSettings.IsValidKitId` is the rule of a kit id (1 to 48
lower-case letters, digits, and dashes that start with a letter or a digit), kept here as a plain check because the
settings subsystem does not depend on the style subsystem.

**File format**: a camel-case JSON object with the same names, for example
`{"version":1,"recentProjects":[],"aiProvider":{"provider":"GitHubCopilot",...},"speech":{...}}`. Enumerations
are written as names. Reading is case-insensitive, skips comments, allows trailing commas, ignores unknown
properties, and treats an unrecognized or non-text enumeration value as the first member.

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
use the known values. Normalize the result.

**Normalize**: Makes parsed or caller-supplied settings safe to use. Sets the version to the current version; replaces
null sections, strings, and dictionary entries with defaults; drops blank recent-project entries; replaces a context
window outside its bounds and an endpoint that is not an absolute `http` or `https` URL with their defaults; trims text
values; keeps the shared style kit folders that are full paths, each once (ignoring case, in the first spelling) and in
their order, drops a relative, blank, or invalid entry with a warning in the log, and keeps a folder that does not exist
because a network folder may be back later; and replaces a default style kit id that no kit can have with none.

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
- **RollingFileLogger** — optional; records missing, unreadable, corrupt, and newer files and failed saves.
- **System.Text.Json** and **System.IO** — serialization and file system primitives.

#### Callers

- `SettingsDialog` — reads `Current` and calls `Save`.
- The application composition root — creates the service and calls `Load`.
- Other subsystems that persist or read settings, such as the recent projects list, use `Current`, `Update`,
  and `Changed`.
