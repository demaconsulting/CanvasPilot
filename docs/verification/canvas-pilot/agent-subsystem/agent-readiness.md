### AgentReadiness

#### Verification Approach

`AgentReadiness` is verified with unit tests defined in `AgentReadinessTests.cs` and `OllamaProbeTests.cs`. The checks
are tested against fakes of every collaborator (`CopilotCliTestDoubles.cs` and `ReadinessTestDoubles.cs`): the file
system, environment, and process runner that reproduce the layout of the development machine (the same fakes that verify
the locator, extended with the variables `ProgramFiles` and the token variables), a starter of the Copilot runtime that
answers with a scripted result and records its requests, and an Ollama probe that answers with a scripted list of models
and window. No program is run, no network is used, and no server is asked. The sentences of the design are asserted
exactly, word for word, so a change of wording fails a test and the design and tests change together.

The real Copilot starter does not exist yet, so the starter that this build ships (`GatedCopilotRuntimeStarter`) is
verified directly, and the check is also run with it to show that Copilot is never reported ready; the "ready" and
"failed" branches are run with the scripted starter. The real Ollama probe is verified against a stand-in HTTP handler
that plays the server: a handler that answers `/api/tags` and `/api/show` from the test, one that refuses the
connection, one that answers with an error status or with text that is not JSON, and one that never answers. The shape
of the `/api/show` answer is the assumption that only a live Ollama server can confirm; this is a manual check recorded
in the system verification, and until it is done the tests show that an answer in any other shape means "not known" and
never an error.

The token rule is verified two ways: a sentinel text that stands for a token is printed by the fake GitHub CLI and
must appear in no report, and the credential tests show that only emptiness decides.

#### Test Environment

N/A - standard test environment. The tests use no files, network, display, or external services. The time-out tests of
the probe wait for a limit of a tenth of a second.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- On the development machine layout, a machine with no Copilot CLI, only the shim, a path in the settings that does not
  exist, a CLI that does not answer, and a CLI older than 1.0.113 each give their exact sentence and status, and no
  check after the failed one runs (no credential probe, no start).
- Version 1.0.113 is accepted.
- Each of the three token variables counts as credentials and then the GitHub CLI is not run; empty and white space
  values do not count.
- A GitHub CLI on the `PATH`, or under the program files, counts when `gh auth token` exits with 0 and prints something
  within 5 seconds; it does not count when it fails, prints nothing, times out, or cannot start.
- With the shipped starter, a machine with a CLI and credentials gives `CopilotRuntimeUnavailable` with the sentence
  "Copilot support needs an AgentKit update; use Ollama meanwhile." and is never ready or usable.
- With a starter that can start the runtime the report is ready, and the starter is asked with the path and version of
  the CLI and with `CheckOnly`.
- No token text is in any report.
- Ollama: an invalid address or an empty model name is not configured and the server is not asked; no answer is
  unreachable; a missing model gives the `ollama pull` sentence; a model that is there is ready with the vision hint, in
  any letter case and with the implied `latest` tag, and the window is asked for under the name that the server lists.
- A window of 16,383 tokens warns and 16,384 does not; the smaller of the configured and the detected window decides;
  an unknown detected window uses the configured one; both sentences are exact.
- The probe turns a refused connection, an error status, text that is not JSON, and a time out into an unreachable
  server or an unknown window, and passes on a cancellation by the caller.
- Check connection searches for the CLI again, the plain check does not, and the result keeps the sentence and the
  usable flag of the report.

#### Test Scenarios

**AgentReadiness_Constants_Rule_AreTheDocumentedValues**: The token variable names are `COPILOT_GITHUB_TOKEN`,
`GH_TOKEN`, `GITHUB_TOKEN` in that order, both time limits are 5 seconds, and the gated sentence is the documented text.
This scenario is tested by `AgentReadiness_Constants_Rule_AreTheDocumentedValues`.

**AgentReadiness_Copilot_NoCli_ReportsMissingWithTheInstallSentence**: A machine with nothing installed but a token
variable set gives `CopilotCliMissing` with the install sentence, is not usable, and runs no program and no start. This
scenario is tested by `AgentReadiness_Copilot_NoCli_ReportsMissingWithTheInstallSentence`.

**AgentReadiness_Copilot_OnlyShim_ReportsMissingAndNamesTheShim**: A machine whose only `copilot.exe` is the VS Code
shim gives `CopilotCliMissing`, and the sentence ends with the shim hint that names the shim path. This scenario is
tested by `AgentReadiness_Copilot_OnlyShim_ReportsMissingAndNamesTheShim`.

**AgentReadiness_Copilot_OverridePathMissing_ReportsMissingForThatPath**: A path in the settings that does not exist
gives `CopilotCliMissing` with a sentence that names the path and says it does not exist, although the real CLI is
installed. This scenario is tested by `AgentReadiness_Copilot_OverridePathMissing_ReportsMissingForThatPath`.

**AgentReadiness_Copilot_CliDoesNotAnswer_ReportsMissingWithReinstallAdvice**: A real CLI that cannot be started gives
`CopilotCliMissing` with advice that includes `winget install GitHub.Copilot`. This scenario is tested by
`AgentReadiness_Copilot_CliDoesNotAnswer_ReportsMissingWithReinstallAdvice`.

**AgentReadiness_Copilot_CliTooOld_ReportsTooOldWithTheUpgradeSentence**: A CLI that reports 1.0.112 gives
`CopilotCliTooOld` with the exact upgrade sentence, is not usable, and only the version was asked for. This scenario is
tested by `AgentReadiness_Copilot_CliTooOld_ReportsTooOldWithTheUpgradeSentence`.

**AgentReadiness_Copilot_MinimumVersion_IsAcceptedAndGoesOnToCredentials**: A CLI that reports exactly 1.0.113 passes
the version check and the result is the sign-in status, since there are no credentials. This scenario is tested by
`AgentReadiness_Copilot_MinimumVersion_IsAcceptedAndGoesOnToCredentials`.

**AgentReadiness_Copilot_NoCredentials_ReportsNotSignedInWithTheExactSentence**: A good CLI without a token variable and
without a GitHub CLI gives `CopilotNotSignedIn` with the exact sentence (`gh auth login`, the variable, Check
connection, and the statement that the `/login` session of the CLI is not shared), is not usable, and the starter is not
asked. This scenario is tested by `AgentReadiness_Copilot_NoCredentials_ReportsNotSignedInWithTheExactSentence`.

**AgentReadiness_Copilot_TokenVariable_CountsAsCredentialsAndStopsAtTheGatedRuntime**: For each of the three variables,
a good CLI and the shipped starter give `CopilotRuntimeUnavailable` with the gated sentence, not usable, even though a
GitHub CLI is installed: the GitHub CLI is not run. This scenario is tested by
`AgentReadiness_Copilot_TokenVariable_CountsAsCredentialsAndStopsAtTheGatedRuntime`.

**AgentReadiness_Copilot_EmptyTokenVariables_AreNotCredentials**: An empty value and a white space value are not
credentials and the result is the sign-in status. This scenario is tested by
`AgentReadiness_Copilot_EmptyTokenVariables_AreNotCredentials`.

**AgentReadiness_Copilot_GitHubCliSignedIn_CountsAsCredentialsAndNeverKeepsTheToken**: A GitHub CLI under the program
files folder that prints a sentinel token counts as credentials, is run exactly once with the arguments `auth` and
`token` and a limit of 5 seconds, and the sentinel is in neither the sentence nor the text form of the report. This
scenario is tested by `AgentReadiness_Copilot_GitHubCliSignedIn_CountsAsCredentialsAndNeverKeepsTheToken`.

**AgentReadiness_Copilot_GitHubCliOnPath_IsRunFromThePath**: A GitHub CLI in a quoted folder of the `PATH` is run, and
the one under the program files is not. This scenario is tested by
`AgentReadiness_Copilot_GitHubCliOnPath_IsRunFromThePath`.

**AgentReadiness_Copilot_GitHubCliNotSignedIn_IsNotACredential**: A GitHub CLI that exits with 1, prints nothing, prints
only white space, times out, or cannot be started is not a credential, and the result is the sign-in status. This
scenario is tested by `AgentReadiness_Copilot_GitHubCliNotSignedIn_IsNotACredential`.

**AgentReadiness_Copilot_GatedStarter_NeverReportsReady**: With everything in order and the shipped starter the status
is not ready but `CopilotRuntimeUnavailable`, and the report is not usable. This scenario is tested by
`AgentReadiness_Copilot_GatedStarter_NeverReportsReady`.

**AgentReadiness_Copilot_StarterStarted_ReportsReadyAndAsksOnlyToCheck**: With a starter that can start, the status is
ready and usable with the sentence "GitHub Copilot CLI 1.0.114 is ready.", and the single request has the CLI path, the
version, and `CheckOnly`. This scenario is tested by
`AgentReadiness_Copilot_StarterStarted_ReportsReadyAndAsksOnlyToCheck`.

**AgentReadiness_Copilot_StarterFailed_ReportsUnavailableWithTheStartersSentence**: A starter that fails gives
`CopilotRuntimeUnavailable` with its own sentence. This scenario is tested by
`AgentReadiness_Copilot_StarterFailed_ReportsUnavailableWithTheStartersSentence`.

**GatedCopilotRuntimeStarter_Start_AnswersNeedsAgentKitUpdate**: The shipped starter answers `NeedsAgentKitUpdate` with
the documented sentence, throws for a cancelled token, and refuses a missing request. This scenario is tested by
`GatedCopilotRuntimeStarter_Start_AnswersNeedsAgentKitUpdate`.

**AgentReadiness_Copilot_RefreshAndCheckAsync_SearchAgainOnlyWhenAsked**: Two plain checks run `--version` once; the
connection check through `IProviderCheck` runs it again; the result is not usable. This scenario is tested by
`AgentReadiness_Copilot_RefreshAndCheckAsync_SearchAgainOnlyWhenAsked`.

**AgentReadiness_GetReport_CancelledToken_ThrowsOperationCanceled**: A cancelled token ends the check for Copilot and
for Ollama with a cancellation. This scenario is tested by
`AgentReadiness_GetReport_CancelledToken_ThrowsOperationCanceled`.

**AgentReadiness_GetReport_NullSettings_ThrowsArgumentNull**: Missing settings are refused with an argument null
exception. This scenario is tested by `AgentReadiness_GetReport_NullSettings_ThrowsArgumentNull`.

**AgentReadiness_Ollama_InvalidEndpoint_ReportsNotConfigured**: An empty address, an address without a scheme, and an
`ftp` address give `NotConfigured` with the exact sentence, and the server is not asked. This scenario is tested by
`AgentReadiness_Ollama_InvalidEndpoint_ReportsNotConfigured`.

**AgentReadiness_Ollama_NoModelName_ReportsNotConfigured**: A white space model name gives `NotConfigured` with the
exact sentence, and the server is not asked. This scenario is tested by
`AgentReadiness_Ollama_NoModelName_ReportsNotConfigured`.

**AgentReadiness_Ollama_ServerNotReachable_ReportsUnreachable**: A server that does not answer gives
`OllamaUnreachable` with the exact sentence that names the address, is not usable, was asked once at that address with a
limit of 5 seconds, and the window is not asked for. This scenario is tested by
`AgentReadiness_Ollama_ServerNotReachable_ReportsUnreachable`.

**AgentReadiness_Ollama_ModelMissing_ReportsTheOllamaPullCommand**: A server with other models (including a model whose
name only starts with the wanted name) gives `OllamaModelMissing` with the sentence that has `ollama pull gemma3`, not
usable, and the window is not asked for. This scenario is tested by
`AgentReadiness_Ollama_ModelMissing_ReportsTheOllamaPullCommand`.

**AgentReadiness_Ollama_ModelPresent_ReportsReadyWithTheVisionHint**: The names `gemma3`, `Gemma3`, `gemma3:latest`,
`vision-model:7b`, and a name without a tag that the server lists as it is give ready with the sentence that has the
vision hint; the window is asked for once, under the name that the server lists, with a limit of 5 seconds. This
scenario is tested by `AgentReadiness_Ollama_ModelPresent_ReportsReadyWithTheVisionHint`.

**AgentReadiness_Ollama_ConfiguredContext_WarnsAt16383AndNotAt16384**: With an unknown model limit, a configured window
of 16,383 gives the warning status, usable, with the exact sentence; 16,384 is ready. This scenario is tested by
`AgentReadiness_Ollama_ConfiguredContext_WarnsAt16383AndNotAt16384`.

**AgentReadiness_Ollama_DetectedContext_TheSmallerOfConfiguredAndDetectedDecides**: Seven pairs of configured and
detected windows: a model limit of 8,192, 16,383, or 2,048 (with a configured 4,096) gives the "model supports only"
sentence; a limit of 16,384 or more with a large setting is ready; a configured 8,192 with a large model limit gives the
"raise it in Settings" sentence; an unknown limit uses the configured window. This scenario is tested by
`AgentReadiness_Ollama_DetectedContext_TheSmallerOfConfiguredAndDetectedDecides`.

**AgentReadiness_DescribeSmallContext_Boundaries_AreTheRecommendedMinimum**: The recommended minimum is 16,384; 16,383
gives a sentence and 16,384 does not, for the configured window and for the model's limit. This scenario is tested by
`AgentReadiness_DescribeSmallContext_Boundaries_AreTheRecommendedMinimum`.

**AgentReadiness_CheckAsync_Ollama_MapsUsabilityAndMessage**: Through `IProviderCheck`, a ready server and a window
warning are usable and a missing model is not, each with the sentence of the report. This scenario is tested by
`AgentReadiness_CheckAsync_Ollama_MapsUsabilityAndMessage`.

**AgentReadiness_Messages_Rule_AreOneLineAndEndWithAFullStop**: The sentences of seven different outcomes have no line
break and end with a full stop. This scenario is tested by `AgentReadiness_Messages_Rule_AreOneLineAndEndWithAFullStop`.

**OllamaProbe_ListModels_ServerAnswers_ReturnsTheNamesFromTags**: A stand-in server with two models gives a reachable
list with both names in order, after one GET of `/api/tags`. This scenario is tested by
`OllamaProbe_ListModels_ServerAnswers_ReturnsTheNamesFromTags`.

**OllamaProbe_ListModels_NoModels_IsReachableAndEmpty**: A server without models is reachable with an empty list. This
scenario is tested by `OllamaProbe_ListModels_NoModels_IsReachableAndEmpty`.

**OllamaProbe_ListModels_Failure_IsAnUnreachableServer**: A refused connection, an error status, and an answer that is
not JSON each give an unreachable server and no exception. This scenario is tested by
`OllamaProbe_ListModels_Failure_IsAnUnreachableServer`.

**OllamaProbe_ListModels_NoAnswerInTime_IsAnUnreachableServer**: A server that never answers, with a limit of a tenth of
a second, gives an unreachable server. This scenario is tested by
`OllamaProbe_ListModels_NoAnswerInTime_IsAnUnreachableServer`.

**OllamaProbe_Cancelled_ThrowsOperationCanceled**: A cancelled caller token throws a cancellation for both questions and
is not taken for an unreachable server. This scenario is tested by `OllamaProbe_Cancelled_ThrowsOperationCanceled`.

**OllamaProbe_GetContextLength_ModelInfo_ReadsTheContextLengthEntry**: For the model families `llama`, `gemma3`, and
`custom`, the entry that ends in `.context_length` is read among other entries, after one POST of `/api/show`. This
scenario is tested by `OllamaProbe_GetContextLength_ModelInfo_ReadsTheContextLengthEntry`.

**OllamaProbe_GetContextLength_UnusableAnswer_IsUnknownNotAnError**: An answer without the entry, with a text value,
with zero, with a fraction, with no model information, or empty gives an unknown length; a value too large for an
integer is limited to the largest integer. This scenario is tested by
`OllamaProbe_GetContextLength_UnusableAnswer_IsUnknownNotAnError`.

**OllamaProbe_GetContextLength_Failure_IsUnknownNotAnError**: A model that the server does not know (status not found)
and a server that does not answer in time give an unknown length. This scenario is tested by
`OllamaProbe_GetContextLength_Failure_IsUnknownNotAnError`.

**OllamaProbe_Arguments_Invalid_AreRefused**: A missing address is refused for both questions and a white space model
name for the window. This scenario is tested by `OllamaProbe_Arguments_Invalid_AreRefused`.
