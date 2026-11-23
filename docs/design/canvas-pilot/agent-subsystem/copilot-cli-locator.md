### CopilotCliLocator

![Agent Structure](AgentSubsystemView.svg)

#### Purpose

`CopilotCliLocator` finds the GitHub Copilot CLI that the user installed, tells it from the launchers (shims) that
editors put on the `PATH`, checks that it reports a version that works, and says in one sentence what it found and what
to do when it cannot be used. CanvasPilot never ships or downloads the CLI, so this is the only way the application
learns which CLI exists. The unit also holds the supporting types that make it testable without a machine that has the
CLI: `IProcessRunner` with its real `ProcessRunner`, `ICliFileSystem` with `CliFileSystem`, and `IEnvironmentReader`
with `SystemEnvironmentReader`. The readiness checks of the subsystem do not use them: the locator reads its own
environment and runs its own programs, and the sign-in is decided by the runtime start, not by a check of credentials.

#### Data Model

**CopilotCliLocator**: `sealed class`. The constructor takes the three seams (`IProcessRunner`, `ICliFileSystem`,
`IEnvironmentReader`), each defaulting to the real implementation. The locator keeps one cached answer and the setting
it was found for. Constants of the rule: `MinimumCliVersion` (1.0.113), `MinimumCliBytes` (20 MiB),
`VersionProbeTimeout` (10 seconds), `ShimDirectoryNames` (`copilot-shim`, `.vscode`, `.vscode-insiders`,
`github.copilot-chat`), and `ShimDirectoryPrefix` (`Microsoft VS Code`).

**CopilotCliInfo**: `record` — `Status`, `Path` (the CLI, or the candidate that failed; `null` when none), `Version`
(`null` when none was read), `Rejected` (the candidates not used, in order), `Message` (one sentence, never empty), and
`IsFound`.

**CopilotCliStatus**: `enum` — `Found`, `NotFound` (nothing usable, or the path of the settings does not exist),
`NotAnExecutable` (the path of the settings is not an `.exe`), `TooOld`, and `Unreadable`.

**RejectedCliCandidate / CopilotCliRejection**: `record` — `Path` (after links were followed), `Reason`, and `Detail`
(one short sentence of facts: a size or a version, never program output). Reasons: `Missing`, `NotExe`, `Shim`,
`TooSmall`, `Unreadable`, `TooOld`.

**IProcessRunner / ProcessResult**: `RunAsync(path, arguments, timeout, cancellationToken)` returns
`ProcessResult(Started, ExitCode, Output, TimedOut)`; the output is the standard output followed by the standard error.
A program that cannot be started, or that ran too long, is a result, not an exception.

**ICliFileSystem**: `FileExists`, `GetLength` (0 when unreadable), `ResolveLinkTarget` (the final target, or the path
itself when it is not a link), and `EnumerateDirectories(parent, pattern)`. **IEnvironmentReader**: `GetVariable(name)`.

#### The Rule (R-LOC)

1. **The settings win.** When `AiProviderSettings.CopilotCliPath` is not empty (after trimming white space and quotes)
   it is the only candidate. It must be a full path to an existing file with the extension `.exe`; the shim, folder, and
   size filters are **not** applied, because the path is the explicit choice of the user, but the version check is. A
   path that fails is reported as `NotAnExecutable`, `NotFound`, `Unreadable`, or `TooOld`; there is **no fallback** to
   automatic detection.
2. **Automatic candidates, in order**, each followed through `ResolveLinkTarget` to the file that really runs and listed
   once by full path (ignoring case):
   a. `%LOCALAPPDATA%\Microsoft\WinGet\Packages\GitHub.Copilot_*\copilot.exe`, the folder whose name sorts last first;
   b. `%LOCALAPPDATA%\Microsoft\WinGet\Links\copilot.exe`, normally a link to a;
   c. `copilot.exe` in every folder of the `PATH`, in order. Other names (`copilot.cmd`, `copilot.ps1`, and the file
      `copilot` without an extension next to the VS Code shim) are never candidates, because only an `.exe` can be started
      without a shell. A file that does not exist is not a candidate and is not listed.
3. **Reject** a candidate, record the reason, and never start it, when, after links were followed, in this order: the
   file does not exist (`Missing`); its extension is not `.exe` (`NotExe`); a folder above it is named like a shim, that
   is, equals `copilot-shim`, `.vscode`, `.vscode-insiders`, or `github.copilot-chat`, or starts with
   `Microsoft VS Code`, ignoring case (`Shim`); or it is smaller than 20 MiB (`TooSmall`).
4. **Validate** the survivors in order: run `--no-auto-update --version` (10 seconds, hidden window, output and error
   joined), read `GitHub Copilot CLI (\d+)\.(\d+)\.(\d+)` (the trailing period, the line that suggests an update, and a
   pre-release suffix do not matter). The option `--no-auto-update` makes the CLI report the version built into the
   executable and not a newer one that it cached; that is the probe that the AI library runs, so the locator cannot
   accept a CLI that the library then refuses. A candidate that cannot be started, times out, or has no version is
   recorded as `Unreadable`
   and the next one is tried; a candidate whose version is below 1.0.113 is recorded as `TooOld` and the next one is
   tried. The first candidate with a new enough version is `Found`. When none is, the result is `TooOld` (the first old
   candidate) if any was old, else `Unreadable` (the first unreadable candidate), else `NotFound`.
5. **Result.** `CopilotCliInfo` with the sentences below. The answer is cached until the setting changes or a refresh is
   asked for (the connection check of the settings page); a cancelled search caches nothing.

- **Found**: "GitHub Copilot CLI {version} found at {path}."
- **NotFound**: for automatic detection, "The GitHub Copilot CLI was not found. Install it with
  `winget install GitHub.Copilot` or set its path in Settings.", followed by " Only the VS Code copilot shim was found
  at {path}." when a shim was rejected; for a path of the settings, "The Copilot CLI path in Settings ({path}) does not
  exist." with the advice to choose the installed copilot.exe or to clear the path.
- **NotAnExecutable**: "The Copilot CLI path in Settings ({path}) is not an .exe file." with the same advice.
- **TooOld**: "Copilot CLI {version} is older than 1.0.113. Run `winget upgrade GitHub.Copilot`."
- **Unreadable**: "The Copilot CLI at {path} did not report its version ({reason})." with the way to reinstall it or to
  choose another file.

The readiness checks map these results to their own statuses and messages.

#### Evidence

| Fact | Value |
| --- | --- |
| Real CLI size | 196,113,184 bytes (development machine) |
| VS Code shim size | 791,904 bytes (development machine) |
| Size limit | 20 MiB = 20,971,520 bytes, between the two with a wide margin |
| `--version` output | "GitHub Copilot CLI 1.0.114." and a line that suggests `copilot update` |
| Version probe | Run with `--no-auto-update`, as the AI library does |
| Library protocol | 4 to 4 (OTS item `CopilotSdk`, `DeclareSupportedCliVersion`) |
| CLI 1.0.106 | reports protocol 3 and fails with the library (spike probe) |
| CLI 1.0.114 | reports protocol 4 and works (spike probe) |
| Minimum version | 1.0.113, the first with protocol 4, between the probed 1.0.106 and 1.0.114 |
| Library minimum | The AI library refuses a CLI below 1.0.113 (`CopilotCli.MinimumVersion`), the same limit |

Paths on the development machine, where the user name is left out:

- the real CLI: `%LOCALAPPDATA%\Microsoft\WinGet\Packages\GitHub.Copilot_*\copilot.exe` (one package folder);
- the VS Code shim: `%LOCALAPPDATA%\Programs\Microsoft VS Code\bin\copilot-shim\copilot.exe`, with the file `copilot`
  without an extension next to it, and the folder `copilot-shim` is an entry of the `PATH`.

The minimum is a constant of the unit (`MinimumCliVersion`) so that a change of the supported range is one line and one
test. The three filters of rule 3 (folder, extension, size) and the version check are independent signals: a launcher
that passes one is stopped by another, and a user who disagrees with all of them sets a path in the settings.

#### Key Methods

**LocateAsync(overridePath, refresh, cancellationToken)**: Applies rules 1 to 5 and returns the result. Gathering and
judging candidates runs on a worker thread, because a `PATH` may name slow network folders; the checks run through the
process runner, which does not block the caller. Cancellation throws `OperationCanceledException`.

**TryParseVersion(output, out version)**: Reads the three numbers that follow `GitHub Copilot CLI`; returns `false` for
empty text, text without the product name, fewer than three numbers, or a number that does not fit an integer.

**IsSupportedVersion(version)**: True when the version is `MinimumCliVersion` or newer.

**IsShimPath(path)**: True when a folder of the path (not the file name) is a shim folder by rule 3; both separators and
any case are understood.

**ProcessRunner.RunAsync**: Starts the program with `UseShellExecute` off, no window, input closed, output and error
redirected and read at the same time as the wait, one list entry per argument, on a worker thread. When the time limit
ends it stops the program and the whole tree of processes it started and returns `TimedOut`; when the caller cancels it
does the same and throws. A start that fails (missing file, not a program, permission) is `Started = false` with the
reason as the output. If the streams do not end within two seconds after the kill, the output is dropped.

#### Error Handling

Every expected problem is data: a missing, unreadable, or old CLI is a status of the result, a program that cannot be
started or does not answer is a rejected candidate, and the file system seam answers "nothing there" instead of throwing
for an ordinary problem with a path. Only cancellation is thrown. The unit never logs and never keeps text that a
program printed beyond the version number: the reasons in the diagnostics are fixed sentences, so a program that prints
a token cannot leak it through a message or a log.

#### Limits

The rule is written for Windows paths (`copilot.exe`, WinGet) and is applied to path text with backslashes and slashes
alike, so that it can be tested on every platform. On another platform no `copilot.exe` exists, so the result is
`NotFound`. A project that gets a macOS or Linux release adds that platform's rule here.

#### Dependencies

The .NET base libraries only (`System.Diagnostics.Process`, `System.IO`, `System.Text.RegularExpressions`); no other
subsystem and no third-party item. The path of the settings is passed in by the caller; the unit does not read the
`Settings` subsystem.

#### Callers

The readiness checks of the subsystem (readiness of the Copilot provider and the connection check) and the factory of
the agents, and, through the path that it finds, the `CopilotRuntimeStarter` unit, which runs that CLI. The `AppShell`
subsystem builds the locator.

The unit also guards that its minimum is never below what the AI library accepts: an OTS test of AgentKit asserts that
`CopilotCli.MinimumVersion` of the library is not above `MinimumCliVersion`, so the locator cannot pass a CLI that the
library then refuses.
