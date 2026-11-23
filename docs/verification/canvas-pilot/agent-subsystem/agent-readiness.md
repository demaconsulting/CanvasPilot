### AgentReadiness

#### Verification Approach

`AgentReadiness` is verified with unit tests defined in `AgentReadinessTests.cs` and `OllamaProbeTests.cs`. The checks
are tested against fakes of every collaborator (`CopilotCliTestDoubles.cs` and `ReadinessTestDoubles.cs`): the file
system, environment, and process runner that reproduce the layout of the development machine (the same fakes that
verify the locator), a starter of the Copilot runtime that answers with a scripted result (which a test can change, as a
user who signs in between two checks) and records its requests, a source of agents that reports a scripted sign-in, and
an Ollama probe that answers with a scripted list of models and window. The check looks for no credentials itself, so
no fake of a token variable or of the GitHub CLI is needed for the sign-in: the sign-in is the result of the scripted
start. No program but the CLI is run, no network is used, and no server is asked. The sentences of the design are
asserted exactly, word for word, so a change of wording fails a test and the design and tests change together.

The starter is a collaborator whose own rules (the one shared runtime, the mapping of every failure to a status and a
sentence) are verified in the unit verification document of `CopilotRuntimeStarter`; here the scripted starter answers
each status in turn (started, CLI too old, not signed in, failed) and the tests show that the check keeps the status and
the sentence of the starter, and that the check starts no runtime of its own. The real Ollama probe is verified against
a stand-in HTTP handler that plays the server: a handler that answers `/api/tags` and `/api/show` from the test, one
that refuses the connection, one that answers with an error status or with text that is not JSON, and one that never
answers. The shape of the `/api/show` answer is the assumption that only a live Ollama server can confirm; this is a
manual check recorded in the system verification, and until it is done the tests show that an answer in any other shape
means "not known" and never an error.

The catalogs of the models are fakes too: a stand-in catalog (`FakeModelCatalog`) that holds the models that a test
gives it and can fail the way a provider that cannot be asked does, for Copilot behind the fake source of the fake
starter, and for Ollama through a fake of `IOllamaModelCatalogs`. The readiness tests that do not concern the model
settings use a catalog that cannot be read, so that they never touch a network. The sentences of the library are
asserted in part (the words that name the problem), because they are the library's; the real catalogs are verified in
the OTS tests of AgentKit and in the verification of `ProviderModelCatalog`.

The token rule is verified by a sentinel text that stands for a token: it is set in a token variable of the fake
environment and must appear in no report, and the sentence that names the account holds only the login, the host, and
the kind of sign-in that the scripted runtime reports.

#### Test Environment

N/A - standard test environment. The tests use no files, network, display, or external services. The time-out tests of
the probe wait for a limit of a tenth of a second.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- On the development machine layout, a machine with no Copilot CLI, only the shim, a path in the settings that does not
  exist, a CLI that does not answer, and a CLI older than 1.0.113 each give their exact sentence and status, and no
  check after the failed one runs (no start).
- Version 1.0.113 is accepted and goes on to the starter.
- The check looks for no credentials of its own: a machine with no token variable and no GitHub CLI (signed in only
  through the Copilot CLI) is not turned away, and only the CLI itself is run.
- With a starter that starts the runtime the report is ready and usable with "GitHub Copilot CLI 1.0.114 is ready.",
  followed by the sign-in sentence of the runtime ("Signed in as octocat on github.com (Copilot CLI sign-in).": the
  login, the host without its scheme, and the kind of sign-in), and the starter is asked once with the path and the
  version of the CLI that the locator found. A runtime that does not say how it signed in leaves the sentence as it was.
- A starter that answers CLI too old, not signed in, or failed gives `CopilotCliTooOld`, `CopilotNotSignedIn`, or
  `CopilotRuntimeUnavailable` with the sentence of the starter unchanged, and the report is not usable.
- With a model in the settings, the catalog of the started runtime validates it: a model that can call tools and see
  pictures is ready, and the sentence names it; a model that is not offered, cannot be used, cannot call tools, or does
  not offer the chosen effort gives `ModelSettingsInvalid` with the sentence of the AI library and is not usable; a
  model that cannot take pictures gives `ModelLacksVision`, which is usable and still names the sign-in; a catalog that
  fails gives `CopilotRuntimeUnavailable` with its message. Settings that name no model are ready as before.
- No token text is in any report.
- Ollama: an invalid address or an empty model name is not configured and the server is not asked; no answer is
  unreachable; a missing model gives the `ollama pull` sentence; a model that is there is ready with the vision hint
  (when the catalog cannot be read), in any letter case and with the implied `latest` tag, and the window is asked for
  under the name that the server lists.
- With a catalog of the server, a model that can do everything is ready and named by the id of the catalog; a model that
  cannot call tools, and a window above what the model was trained for, give `ModelSettingsInvalid` and are not usable;
  a model that cannot take pictures is `ModelLacksVision` and usable; with a window that is also too small the status is
  the window warning and the sentence has both; the trained length of the catalog is used for the window warning when
  the server does not answer the question about the context.
- A window of 16,383 tokens warns and 16,384 does not; the smaller of the configured and the detected window decides;
  an unknown detected window uses the configured one; both sentences are exact.
- The probe turns a refused connection, an error status, text that is not JSON, and a time out into an unreachable
  server or an unknown window, and passes on a cancellation by the caller.
- Check connection searches for the CLI again, the plain check does not, the result keeps the sentence and the usable
  flag of the report, and a user who signs in between two checks is seen by the next Check connection.

#### Test Scenarios

**AgentReadiness_Constants_Rule_AreTheDocumentedValues**: The time limit of a question to the Ollama server is 5
seconds, and the sentence of a runtime that is not signed in is the one of the starter. This scenario is tested by
`AgentReadiness_Constants_Rule_AreTheDocumentedValues`.

**AgentReadiness_Copilot_NoCli_ReportsMissingWithTheInstallSentence**: A machine with nothing installed gives
`CopilotCliMissing` with the install sentence, is not usable, and runs no program and no start. This
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

**AgentReadiness_Copilot_MinimumVersion_IsAcceptedAndGoesOnToTheStarter**: A CLI that reports exactly 1.0.113 passes
the version check, the starter is asked with that version, and the result is ready. This scenario is tested by
`AgentReadiness_Copilot_MinimumVersion_IsAcceptedAndGoesOnToTheStarter`.

**AgentReadiness_Copilot_StarterNotSignedIn_ReportsTheSentenceOfTheStarter**: A good CLI and a starter that answers
that the runtime is not signed in give `CopilotNotSignedIn` with the sentence of the starter unchanged (`copilot`,
`/login`, `gh auth login`, the variables, and Check connection), not usable, with one request to the starter, and no
program but the CLI was run (no credential probe). This scenario is tested by
`AgentReadiness_Copilot_StarterNotSignedIn_ReportsTheSentenceOfTheStarter`.

**AgentReadiness_Copilot_OnlyTheCopilotCliIsSignedIn_IsNotRejected**: A machine with no token variable and no GitHub CLI
whose runtime signed in through the Copilot CLI is ready and usable, and only the CLI itself was run, so the GitHub CLI
is never asked. This scenario is tested by `AgentReadiness_Copilot_OnlyTheCopilotCliIsSignedIn_IsNotRejected`.

**AgentReadiness_Copilot_Ready_NamesTheAccountTheHostAndTheKindOfSignIn**: A runtime that signed in as `octocat` on
`https://github.com` through the Copilot CLI gives the sentence "GitHub Copilot CLI 1.0.114 is ready. Signed in as
octocat on github.com (Copilot CLI sign-in).", with the host shown without its scheme, and a token variable that holds a
sentinel token appears nowhere in the sentence. This scenario is tested by
`AgentReadiness_Copilot_Ready_NamesTheAccountTheHostAndTheKindOfSignIn`.

**AgentReadiness_Copilot_Ready_SignInUnknown_AddsNothing**: A runtime that gives no sign-in information, and one that
reports an unknown source with no login and no host, each leave the sentence exactly "GitHub Copilot CLI 1.0.114 is
ready." This scenario is tested by `AgentReadiness_Copilot_Ready_SignInUnknown_AddsNothing`.

**AgentReadiness_Copilot_StarterRefuses_ReportsTheMatchingStatusAndSentence**: A starter that answers CLI
too old, not signed in, or failed gives `CopilotCliTooOld`, `CopilotNotSignedIn`, or `CopilotRuntimeUnavailable`
respectively, each with the sentence of the starter unchanged, and the report is not usable. This scenario is tested by
`AgentReadiness_Copilot_StarterRefuses_ReportsTheMatchingStatusAndSentence`.

**AgentReadiness_Copilot_StarterStarted_ReportsReadyAndAsksWithTheCliThatWasFound**: With a starter that can start, the
status is ready and usable with the sentence "GitHub Copilot CLI 1.0.114 is ready.", and the single request has the path
and the version of the CLI that the locator found. This scenario is tested by
`AgentReadiness_Copilot_StarterStarted_ReportsReadyAndAsksWithTheCliThatWasFound`.

**AgentReadiness_Copilot_ModelOfTheSettings_IsValidatedByTheRuntimeCatalogAndNamedWhenReady**: A runtime whose catalog
offers `claude-haiku-5.5` with the efforts `low` and `xhigh`. Settings that name that model with the effort `xhigh` give
ready with "GitHub Copilot CLI 1.0.114 is ready with the model claude-haiku-5.5."; settings with no model give the plain
ready sentence. This scenario is tested by
`AgentReadiness_Copilot_ModelOfTheSettings_IsValidatedByTheRuntimeCatalogAndNamedWhenReady`.

**AgentReadiness_Copilot_ModelSettingsTheCatalogRefuses_ReportModelSettingsInvalid**: Four settings against a catalog of
three models: a model that cannot call tools ("does not support tool calling"), a model that the catalog does not have
("is not available from provider 'copilot'"), an effort that a model does not offer ("does not support reasoning effort
'bogus'. Supported: low, high."), and a model that cannot be used with its reason. Each gives `ModelSettingsInvalid`
with that part of the sentence of the library and is not usable. This scenario is tested by
`AgentReadiness_Copilot_ModelSettingsTheCatalogRefuses_ReportModelSettingsInvalid`.

**AgentReadiness_Copilot_ModelWithoutVisionAndUnreadableCatalog_WarnsAndReportsTheRuntime**: A model that cannot take
pictures, on a runtime that signed in through the Copilot CLI, gives `ModelLacksVision`, usable, with the sentence
"blind does not accept pictures" followed by the sign-in sentence; after the catalog is made to fail with the message of
a runtime that is not signed in, the same settings give `CopilotRuntimeUnavailable` with that message. This scenario is
tested by `AgentReadiness_Copilot_ModelWithoutVisionAndUnreadableCatalog_WarnsAndReportsTheRuntime`.

**AgentReadiness_Copilot_RefreshAndCheckAsync_SearchAgainOnlyWhenAsked**: Two plain checks run `--version` once; the
connection check through `IProviderCheck` runs it again; the result is not usable while the starter says that the
runtime is not signed in, and after the starter is changed to start (the user signed in) the next Check connection is
usable and ends with the sign-in sentence. This scenario is tested by
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

**AgentReadiness_Ollama_ModelSettings_AreValidatedByTheCatalogOfTheServer**: A server that lists four models, described
by a fake catalog: a model that does everything (asked as `full`) is ready with "Ollama is running with the model
full:latest."; a model without tool calling and a window of 32,768 for a model trained for 8,192 tokens are
`ModelSettingsInvalid` and not usable, with the sentences of the library; a model that cannot see pictures is
`ModelLacksVision`, usable; the same model with a window of 4,096 is the window warning with both sentences. This
scenario is tested by `AgentReadiness_Ollama_ModelSettings_AreValidatedByTheCatalogOfTheServer`.

**AgentReadiness_Ollama_NoContextAnswer_UsesTheTrainedLengthOfTheCatalog**: A model trained for 16,000 tokens, asked
with a window of 16,000, while the server gives no answer to the question about the context: the status is the window
warning and the sentence holds 16,000. This scenario is tested by
`AgentReadiness_Ollama_NoContextAnswer_UsesTheTrainedLengthOfTheCatalog`.

**AgentReadiness_DescribeSmallContext_Boundaries_AreTheRecommendedMinimum**: The recommended minimum is 16,384; 16,383
gives a sentence and 16,384 does not, for the configured window and for the model's limit. This scenario is tested by
`AgentReadiness_DescribeSmallContext_Boundaries_AreTheRecommendedMinimum`.

**AgentReadiness_CheckAsync_Ollama_MapsUsabilityAndMessage**: Through `IProviderCheck`, a ready server and a window
warning are usable and a missing model is not, each with the sentence of the report. This scenario is tested by
`AgentReadiness_CheckAsync_Ollama_MapsUsabilityAndMessage`.

**AgentReadiness_Messages_Rule_AreOneLineAndEndWithAFullStop**: The sentences of nine different outcomes (including a
ready Copilot with its sign-in sentence, and a runtime that is not signed in with the digest of what was found) have no
line break and end with a full stop. This scenario is tested by
`AgentReadiness_Messages_Rule_AreOneLineAndEndWithAFullStop`.

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
