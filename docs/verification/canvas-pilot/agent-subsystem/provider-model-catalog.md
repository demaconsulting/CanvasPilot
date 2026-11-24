### ProviderModelCatalog

#### Verification Approach

`ProviderModelCatalog` is verified with unit tests defined in `ProviderModelCatalogTests.cs`. The models of a provider
come from a catalog of the AI library, so the tests use two kinds of stand-in. For the lists and the failures, a fake
catalog (`FakeModelCatalog`) holds the models that a test makes (`TestModels.Make`, with chosen capabilities, efforts,
window, and availability), counts its lists and refreshes, and can fail with the exception that the real catalog throws;
for GitHub Copilot it sits behind a fake source of agents of a fake starter (the same fakes that verify the factory),
and for Ollama behind a fake of `IOllamaModelCatalogs`. For the mapping of what a real Ollama server says, the tests use
the real `OllamaModelCatalog` of the AI library over a stand-in HTTP handler that answers `/api/tags` and `/api/show`
from the test, and they count the requests that reach it, which shows the cache and the refresh. The locator is the real
one over the fake file system and process runner of the locator tests. No program is run, no network is used, and no
server is asked.

That the real Copilot catalog lists what the live service reports is not shown by these tests; it is shown by the opt-in
live test `LiveCopilotTurn_SettingsPage_ListsModelsAndTheChosenModelAndEffortRun`, which is recorded in the manual
verification of the system, and the libraries' own behavior is the subject of the OTS tests of AgentKit.

#### Test Environment

N/A - standard test environment. The tests use no files, no network, no process, and no display. The stand-in server is
an HTTP handler in memory.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A model of the AI library is mapped with its id and name, whether it calls tools, accepts pictures, and reasons, its
  window, its efforts with the provider's ids (only when the effort can be adjusted), its default effort, whether it can
  be used and why not, and only the notes that ask for attention; a model with nothing is mapped as it is, and a missing
  model is refused.
- GitHub Copilot lists every model of the runtime that the starter was asked for (once, on the CLI that the locator
  found), the ones that can be used first and each group by name, with no message; a plain list does not refresh the
  catalog and a refresh does.
- Ollama lists the models of the server at the trimmed address, the usable first, and a refresh describes them again.
- Through the real catalog and a stand-in server the capabilities (tools, pictures, thinking), the trained length, the
  efforts of a model that thinks (`none`, `low`, `medium`, `high`), and an embedding model as unusable are mapped; a
  second list within the cache time sends no request and a refresh sends requests again.
- A machine without a CLI, a starter that says not signed in, a runtime without a source, a catalog that fails with the
  library's message, and a runtime that was stopped each give no models and one sentence, and no exception.
- A server that refuses the connection, answers something that is not JSON, times out, or answers with an error status
  gives no models and the sentence that names the address and says that the name can still be typed; a server without
  models gives the sentence for pulling a model; an address that is not valid gives the sentence of the factory.
- A cancellation by the caller is thrown for both providers, and a missing setting is refused.
- The catalogs of Ollama give the same catalog for the address that was asked for last, so the second list at the same
  address sends no request, and another address gets a new one.

#### Test Scenarios

**ProviderModelCatalog_Map_Model_CarriesCapabilitiesEffortsWindowAndWarnings**: A model that calls tools, sees pictures,
and reasons, with the efforts `none`, `low`, and `xhigh`, a window of 1,000,000 tokens, a default effort, and three
notes of the three kinds. The mapped model has the id and name, the three capabilities, the window, the three efforts in
order, the default effort, is usable, and has the two warnings (the information note is dropped). This scenario is
tested by `ProviderModelCatalog_Map_Model_CarriesCapabilitiesEffortsWindowAndWarnings`.

**ProviderModelCatalog_Map_ModelWithContextTiers_CarriesTheSizesAndTheDefault**: A model with the sizes `default`
(Standard, 328,000 tokens) and `long_context` (Long context, 1,000,000), the same sizes on a model whose size cannot be
adjusted, and a model with none. The first maps both sizes with their names and windows and the default `default`; the
other two map no sizes and no default. This scenario is tested by
`ProviderModelCatalog_Map_ModelWithContextTiers_CarriesTheSizesAndTheDefault`.

**ProviderModelCatalog_Map_PlainAndUnusableModels_AreMappedWithTheirLimits**: A model without tools, pictures, window,
and efforts; a model that cannot be used with its reason; and a model that lists an effort but whose effort cannot be
adjusted. They are mapped as they are, the last one with no efforts, and a missing model is refused. This scenario is
tested by `ProviderModelCatalog_Map_PlainAndUnusableModels_AreMappedWithTheirLimits`.

**ProviderModelCatalog_ListModels_Copilot_ListsEveryModelOfTheSharedRuntime**: A runtime with three models in no order,
one of which cannot be used and one of which cannot call tools. The list is the two that can be used by name and then
the one that cannot, with no message; the starter was asked once with the path and the version of the real CLI; the
catalog was not refreshed. This scenario is tested by
`ProviderModelCatalog_ListModels_Copilot_ListsEveryModelOfTheSharedRuntime`.

**ProviderModelCatalog_ListModels_CopilotRefresh_AsksTheRuntimeAgain**: A plain list, and then a list with the refresh
flag; the catalog was refreshed once, by the second. This scenario is tested by
`ProviderModelCatalog_ListModels_CopilotRefresh_AsksTheRuntimeAgain`.

**ProviderModelCatalog_ListModels_CopilotCannotBeAsked_IsAMessageAndNoModels**: A machine with nothing installed (the
install sentence), a starter that says not signed in (its sentence), a started runtime without a source (the sentence of
the factory), a catalog that fails with "The Copilot runtime could not list models" (that message), and a catalog whose
runtime was stopped (the sentence that says to choose Refresh models). Each gives no models. This scenario is tested by
`ProviderModelCatalog_ListModels_CopilotCannotBeAsked_IsAMessageAndNoModels`.

**ProviderModelCatalog_ListModels_Ollama_ListsTheModelsOfTheServerAtTheAddress**: A server with a model that has two
efforts and an embedding model, and an address with spaces around it. Both are listed, the usable one first, with the
efforts and the capabilities; every catalog was asked for the trimmed address; a refresh describes the models again.
This scenario is tested by `ProviderModelCatalog_ListModels_Ollama_ListsTheModelsOfTheServerAtTheAddress`.

**ProviderModelCatalog_ListModels_OllamaCannotBeAsked_IsAMessageAndNoModels**: A catalog that fails with a refused
connection, with text that is not JSON, and with a time out each give no models and the sentence "Ollama did not answer
at http://localhost:11434" that says that the name can be typed; an empty server gives the sentence for pulling a model;
an address that is not an address gives the sentence for the address. This scenario is tested by
`ProviderModelCatalog_ListModels_OllamaCannotBeAsked_IsAMessageAndNoModels`.

**ProviderModelCatalog_ListModels_OllamaOnStandInServer_MapsWhatTheServerDescribes**: The real catalog over a stand-in
server with a model that completes, calls tools, sees pictures, and thinks (trained for 262,144 tokens) and a second
model. The first is mapped with the three capabilities, the window, and `none`, `low`, `medium`, `high`; the server was
asked only on the first list, and again after the refresh. This scenario is tested by
`ProviderModelCatalog_ListModels_OllamaOnStandInServer_MapsWhatTheServerDescribes`.

**ProviderModelCatalog_ListModels_OllamaServerError_IsAMessageAndNoModels**: A stand-in server that answers every
question with an error status gives no models and the sentence "Ollama did not answer". This scenario is tested by
`ProviderModelCatalog_ListModels_OllamaServerError_IsAMessageAndNoModels`.

**ProviderModelCatalog_ListModels_CancelledToken_ThrowsOperationCanceled**: A token that is cancelled makes the list of
each provider throw a cancellation, and a missing setting is refused. This scenario is tested by
`ProviderModelCatalog_ListModels_CancelledToken_ThrowsOperationCanceled`.

**OllamaModelCatalogs_For_SameAddress_SharesWhatTheServerSaid**: Two lists at one address send one request; a list at
another address sends another to the other host; a missing address is refused. This scenario is tested by
`OllamaModelCatalogs_For_SameAddress_SharesWhatTheServerSaid`.
