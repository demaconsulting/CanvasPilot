### CopilotCliLocator

#### Verification Approach

`CopilotCliLocator` is verified with unit tests defined in `CopilotCliLocatorTests.cs` and `ProcessRunnerTests.cs`. The
locator is tested against fakes of its three seams: a file system, an environment, and a process runner that exist only
in the tests (`CopilotCliTestDoubles.cs`). The fakes reproduce a real machine exactly: the paths and the sizes are those
of the development machine (the real Copilot CLI of 196,113,184 bytes in the winget package folder, the link to it in
the winget links folder, the VS Code shim `copilot.exe` of 791,904 bytes and the file without an extension next to it,
and the npm `copilot.cmd`), with the user name replaced. No program is started and no disk is read in the tests of the
rule;
a fake process runner records which programs were run with which arguments and time limit, so the tests can show that a
rejected candidate is never started.

The process runner is the one real collaborator that is verified with real programs: its tests start the command
interpreter of the platform (`cmd.exe` on Windows, `/bin/sh` elsewhere) with trivial commands, because a fake could not
show that a process was started without a shell, that its output was collected, or that a process that outlives its time
limit is killed with the processes it started. The kill is shown by the output of a command that prints a line and then
runs for half a minute: the output is complete only when the pipes were closed by the end of the whole tree. The real
file system reader is tested against a temporary folder and the real environment reader against `PATH`.

#### Test Environment

N/A - standard test environment. The rule tests use no files, network, or programs. The process runner tests use the
command interpreter of the operating system and a temporary folder, which every machine that runs the tests has; they
take a few seconds because one of them waits for a time limit to end.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- On the layout of the development machine the real CLI is found, the link to it is counted once, and the shim, the shim
  script, and the npm script are never started.
- A candidate is rejected when it is not an `.exe`, lies in a shim folder (each name of the list, the start of the VS
  Code name, any case, either separator), or is smaller than 20 MiB; a file of exactly 20 MiB is accepted.
- A path in the settings is the only candidate, is not filtered, is validated with `--version`, and fails visibly
  without a fallback when it is missing, not an `.exe`, unreadable, or too old.
- A candidate that cannot be started, times out, or prints no version is passed over; a version below 1.0.113 is
  reported only when no candidate is new enough; 1.0.113 is accepted.
- The version is read from `GitHub Copilot CLI x.y.z.` with or without the update line, a suffix, or a trailing period.
- Nothing that a program printed is kept in a message or a diagnostic.
- The process runner returns the output, exit code, and both streams of a real command, passes an argument with spaces
  as one argument, reports a time out and kills the whole process tree, throws when the caller cancels, and reports a
  missing program as a result.

#### Test Scenarios

**CopilotCliLocator_TryParseVersion_OutputText_ReadsVersionOrNone**: Twelve texts are read: the output of CLI 1.0.114
with and without the update line and with Windows line ends, a pre-release suffix, a warning line before the version,
and the texts that have no version (only numbers, two numbers, letters, a number too large for an integer, other words,
empty, and `null`). The first five give their three numbers and the others give none. This scenario is tested by
`CopilotCliLocator_TryParseVersion_OutputText_ReadsVersionOrNone`.

**CopilotCliLocator_IsSupportedVersion_Versions_AcceptsOnlyTheMinimumAndNewer**: Versions 1.0.106, 1.0.112, and 0.9.999
are too old; 1.0.113, 1.0.114, 1.1.0, and 2.0.0 are accepted; the minimum constant is 1.0.113. This scenario is tested
by `CopilotCliLocator_IsSupportedVersion_Versions_AcceptsOnlyTheMinimumAndNewer`.

**CopilotCliLocator_IsShimPath_Paths_MatchesTheShimFolderList**: Each of the four shim folder names, the start of the VS
Code installation folder name (also the Insiders name and with slashes and other case), and a name in upper case are
shims; the real installation path, a folder `tools`, a file that is only named `copilot-shim.exe`, and a folder `vscode`
without the dot are not. This scenario is tested by `CopilotCliLocator_IsShimPath_Paths_MatchesTheShimFolderList`.

**CopilotCliLocator_ShimDirectoryNames_Rule_ListsTheDocumentedNames**: The list of shim folder names, the VS Code
prefix, the size limit, and the time limit are exactly those of the rule. This scenario is tested by
`CopilotCliLocator_ShimDirectoryNames_Rule_ListsTheDocumentedNames`.

**CopilotCliLocator_Locate_DevelopmentMachine_FindsTheRealCliAndNotTheShim**: On the layout and `PATH` of the
development machine, with a version answer for both the real CLI and the shim, the real CLI is found with version
1.0.114, the shim is the single rejected candidate with the reason shim, and exactly one program is run: the real CLI,
with the arguments `--no-auto-update --version` and a limit of 10 seconds. This scenario is tested by
`CopilotCliLocator_Locate_DevelopmentMachine_FindsTheRealCliAndNotTheShim`.

**CopilotCliLocator_Locate_ShimFirstOnPath_StillRejectsTheShim**: A `PATH` that names the shim folder before the package
folder still yields the real CLI at version 1.0.113, and the shim is not run. This scenario is tested by
`CopilotCliLocator_Locate_ShimFirstOnPath_StillRejectsTheShim`.

**CopilotCliLocator_Locate_OnlyShimOnPath_NotFoundWithShimHint**: With only the npm and shim folders on the `PATH`, the
result is not found, has no path or version, tells the user to install with `winget install GitHub.Copilot` or to set
the path in Settings, names the shim file, lists it with the reason shim, and runs no program. This scenario is tested
by `CopilotCliLocator_Locate_OnlyShimOnPath_NotFoundWithShimHint`.

**CopilotCliLocator_Locate_NothingInstalled_NotFoundWithoutShimHint**: With no variables and an empty file system the
result is not found, nothing is rejected, no program is run, and the message does not mention a shim. This scenario is
tested by `CopilotCliLocator_Locate_NothingInstalled_NotFoundWithoutShimHint`.

**CopilotCliLocator_Locate_NpmCmdAndScriptWithoutExtension_AreIgnored**: With only `copilot.cmd` and the file `copilot`
without an extension on disk, nothing is a candidate: the result is not found, nothing is listed as rejected, and no
program is run. This scenario is tested by `CopilotCliLocator_Locate_NpmCmdAndScriptWithoutExtension_AreIgnored`.

**CopilotCliLocator_Locate_FileSize_RejectsBelowTheLimitAndAcceptsTheLimit**: Two `.exe` files on the `PATH`, one
a byte below 20 MiB and one of exactly 20 MiB: the second is used, the first is listed with the reason too small and its
size in the detail, and only the second is run. This scenario is tested by
`CopilotCliLocator_Locate_FileSize_RejectsBelowTheLimitAndAcceptsTheLimit`.

**CopilotCliLocator_Locate_LinkWithUnusableTarget_IsRejectedForTheTarget**: A winget link whose target is a `.cmd` file,
and one whose target does not exist, are each rejected under the name of the target, with the reason not an `.exe` and
missing; the result is not found and no program is run. This scenario is tested by
`CopilotCliLocator_Locate_LinkWithUnusableTarget_IsRejectedForTheTarget`.

**CopilotCliLocator_Locate_LinkAndPathNameTheSameFile_CheckedOnce**: The package folder, the link to it, and the link
folder in upper case on the `PATH` all name one file; the file is found at the package path and run once. This scenario
is tested by `CopilotCliLocator_Locate_LinkAndPathNameTheSameFile_CheckedOnce`.

**CopilotCliLocator_Locate_TwoPackageFolders_TriesTheLastNameFirst**: Two package folders that each hold a large CLI:
the folder whose name sorts last is used, and the other is never run. This scenario is tested by
`CopilotCliLocator_Locate_TwoPackageFolders_TriesTheLastNameFirst`.

**CopilotCliLocator_Locate_OverrideInShimFolder_WinsWithoutFiltersOrFallback**: The real CLI is installed, but the
settings name the shim, which answers with version 1.0.120: the shim is found with that version, and only the shim is
run. This scenario is tested by `CopilotCliLocator_Locate_OverrideInShimFolder_WinsWithoutFiltersOrFallback`.

**CopilotCliLocator_Locate_OverrideInQuotes_IsTrimmed**: A path in quotes with a space after it is used as the path
inside the quotes. This scenario is tested by `CopilotCliLocator_Locate_OverrideInQuotes_IsTrimmed`.

**CopilotCliLocator_Locate_OverrideMissing_NotFoundWithoutFallback**: A path that does not exist gives not found, with
the path in the message and the path in the result, and no program is run although the real CLI is installed. This
scenario is tested by `CopilotCliLocator_Locate_OverrideMissing_NotFoundWithoutFallback`.

**CopilotCliLocator_Locate_OverrideNotExe_NotAnExecutableWithoutFallback**: The existing npm `copilot.cmd` as the path
gives the status not an executable, a message that says it is not an `.exe` file, and no program is run. This scenario
is tested by `CopilotCliLocator_Locate_OverrideNotExe_NotAnExecutableWithoutFallback`.

**CopilotCliLocator_Locate_OverrideFailsValidation_ReportsItWithoutFallback**: An existing path whose program prints no
version gives unreadable, and one that prints 1.0.112 gives too old; in both the path is reported and only that program
is run. This scenario is tested by `CopilotCliLocator_Locate_OverrideFailsValidation_ReportsItWithoutFallback`.

**CopilotCliLocator_Locate_FirstCandidateUnreadable_FallsThroughToTheNext**: Two large CLIs on the `PATH`, the first of
which cannot be started, does not answer in time, or prints no version: the second is used, the first is listed as
unreadable with the matching reason (could not be started, within 10 seconds, has no version), and both were run in
order. This scenario is tested by `CopilotCliLocator_Locate_FirstCandidateUnreadable_FallsThroughToTheNext`.

**CopilotCliLocator_Locate_OldCli_IsReportedOnlyWhenNoLaterCandidatePasses**: An unreadable CLI, one of version 1.0.112,
and one of version 1.0.114 are on the `PATH`: the last is used and the other two are listed as unreadable and too old.
With the last one removed, the result is too old at the second path with version 1.0.112 and the sentence "Copilot CLI
1.0.112 is older than 1.0.113. Run `winget upgrade GitHub.Copilot`." This scenario is tested by
`CopilotCliLocator_Locate_OldCli_IsReportedOnlyWhenNoLaterCandidatePasses`.

**CopilotCliLocator_Locate_AllCandidatesUnreadable_ReportsTheFirst**: Two large CLIs that cannot be started give
unreadable at the first path, both listed, with a message that says no version was reported and names the install
command. This scenario is tested by `CopilotCliLocator_Locate_AllCandidatesUnreadable_ReportsTheFirst`.

**CopilotCliLocator_Locate_ProgramPrintsSecret_IsNeverKept**: A CLI whose answer carries a token and no version gives a
message and diagnostics that do not contain the token. This scenario is tested by
`CopilotCliLocator_Locate_ProgramPrintsSecret_IsNeverKept`.

**CopilotCliLocator_Locate_Caching_ReusesUntilRefreshOrSettingChange**: Two searches with the same (empty) setting run
the check once and return the same result object; a refresh and a changed setting each run it again. This scenario is
tested by `CopilotCliLocator_Locate_Caching_ReusesUntilRefreshOrSettingChange`.

**CopilotCliLocator_Locate_Cancelled_ThrowsAndIsNotCached**: A search with a cancelled token throws a cancellation
exception; the next search with a live token runs and finds the CLI. This scenario is tested by
`CopilotCliLocator_Locate_Cancelled_ThrowsAndIsNotCached`.

**CopilotCliLocator_Locate_PathWithQuotesAndEmptyEntries_IsReadEntryByEntry**: A `PATH` with empty entries and an entry
in quotes with spaces and a folder name with a space is read entry by entry, and the CLI in the quoted folder is found.
This scenario is tested by `CopilotCliLocator_Locate_PathWithQuotesAndEmptyEntries_IsReadEntryByEntry`.

**CopilotCliInfo_Constructor_InvalidArguments_AreRefused**: A missing list of rejected candidates, a missing message,
and a white-space message are refused; a valid result keeps its values and is found only for the found status. This
scenario is tested by `CopilotCliInfo_Constructor_InvalidArguments_AreRefused`.

**ProcessRunner_RunAsync_EchoCommand_ReturnsItsOutput**: The shell runs `echo hello`; the result is started, has exit
code 0, did not time out, and has the output `hello`. This scenario is tested by
`ProcessRunner_RunAsync_EchoCommand_ReturnsItsOutput`.

**ProcessRunner_RunAsync_FailingCommand_ReturnsExitCodeAndBothStreams**: The shell prints one line on each stream and
exits with 3; the code is 3 and the output is the standard output line followed by the standard error line. This
scenario is tested by `ProcessRunner_RunAsync_FailingCommand_ReturnsExitCodeAndBothStreams`.

**ProcessRunner_RunAsync_ArgumentWithSpaces_ArrivesAsOneArgument**: A script with several spaces is passed as one list
entry and runs as one command, so no argument was split. This scenario is tested by
`ProcessRunner_RunAsync_ArgumentWithSpaces_ArrivesAsOneArgument`.

**ProcessRunner_RunAsync_LongRunningCommand_TimesOutAndKillsTheTree**: The shell prints a line and then runs a program
for half a minute with a limit of four seconds; the result is timed out with exit code -1 and the output `started`,
which is complete only because the whole tree was killed and the pipes were closed. This scenario is tested by
`ProcessRunner_RunAsync_LongRunningCommand_TimesOutAndKillsTheTree`.

**ProcessRunner_RunAsync_CancelledToken_ThrowsOperationCanceled**: A program that would run for half a minute is started
with a token that is cancelled after half a second; the run throws a cancellation exception. This scenario is tested by
`ProcessRunner_RunAsync_CancelledToken_ThrowsOperationCanceled`.

**ProcessRunner_RunAsync_MissingProgram_ReturnsNotStarted**: A path in a folder that does not exist gives a result that
is not started, not timed out, has exit code -1, and has a reason. This scenario is tested by
`ProcessRunner_RunAsync_MissingProgram_ReturnsNotStarted`.

**ProcessRunner_RunAsync_InvalidArguments_AreRefused**: A white-space path, a missing argument list, and a time limit of
zero raise an argument exception, an argument null exception, and an out-of-range exception. This scenario is tested by
`ProcessRunner_RunAsync_InvalidArguments_AreRefused`.

**ProcessResult_NotStarted_Reason_HasNoExitCode**: The not-started result has no start, no time out, exit code -1, and
the reason as its output. This scenario is tested by `ProcessResult_NotStarted_Reason_HasNoExitCode`.

**SystemEnvironmentReader_GetVariable_SetAndUnsetVariables_ReturnValueOrNull**: `PATH` has a value and a made-up name
has none. This scenario is tested by `SystemEnvironmentReader_GetVariable_SetAndUnsetVariables_ReturnValueOrNull`.

**CliFileSystem_Queries_RealFiles_AnswerFromTheDisk**: In a temporary folder with a file of 1,234 bytes in a folder that
matches a pattern, the reader finds the file, does not find a missing one, reports the length and 0 for a missing file,
resolves a file that is not a link to itself, lists only the matching folder, and lists nothing for a missing folder.
This scenario is tested by `CliFileSystem_Queries_RealFiles_AnswerFromTheDisk`.
