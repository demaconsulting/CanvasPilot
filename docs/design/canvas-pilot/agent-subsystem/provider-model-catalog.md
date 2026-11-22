### ProviderModelCatalog

![Agent Runtime](AgentSubsystemRuntimeView.svg)

#### Purpose

`ProviderModelCatalog` lists the models that the AI provider of the settings offers, with what each can do, in the types
of CanvasPilot, for the AI Provider page of the Settings. It is the implementation of `IProviderModelSource`, which the
Settings subsystem owns, so the page never depends on the Agent subsystem or on the AI library. The unit also holds its
supporting types: `ModelRequirements`, the one place that states what the application needs of a model and how the
choices of the settings become the model settings that the AI library validates (used by the readiness check and the
factory of the agents as well), and `IOllamaModelCatalogs` with its real `OllamaModelCatalogs`, which gives the catalog
of an Ollama server.

#### Data Model

**ProviderModelCatalog**: `sealed class`, implements `IProviderModelSource`. The constructor takes a
`CopilotCliLocator`, an `ICopilotRuntimeStarter`, and an `IOllamaModelCatalogs`, each defaulting to the real one; the
application passes the locator and the starter that it shares with the readiness check and the factory, so that no
second Copilot runtime is ever started for the list. It holds only these collaborators and is safe for concurrent use.
Constants: `OllamaListTimeout` (15 seconds), `NoOllamaModelsMessage`, and the two sentences that the factory of the
agents answers with as well (`InvalidEndpointMessage` and `NoAgentSourceMessage`; the factory takes them from here, so
this unit depends on no other unit of the subsystem that depends on it).

**ProviderModelList / ProviderModelInfo / ProviderEffortChoice** (in the Settings subsystem): plain data. A
`ProviderModelInfo` has the id, the name, `SupportsToolCalls`, `SupportsVision`, `SupportsReasoning`, the largest
context window, the reasoning efforts (id and name) that the model offers, the default effort, `IsUsable` and the reason
when not, and the warnings. `ProviderModelCatalog.Map` makes one from the `ModelInfo` of the AI library: the
capabilities come from `ModelInfo.Supports`, the efforts are copied only when the model says that its effort can be
adjusted (`ModelAdjustments.ReasoningEffort`), and the notes of the library that ask for attention (a warning, or
something the user must do first, such as accepting terms) become the warnings; plain information notes are dropped.

**ModelRequirements**: `internal static class`. `Required` is `ModelCapabilities.ToolCalls`: every drawing is made by
calling tools, so a model that cannot call them cannot be used and fails the validation. Vision is wanted and not
required (`VisionWarning`): a model that cannot take pictures still works for text and files, so it is a warning that
never blocks. `ToModelSettings(settings, withContextWindow)` makes the `ModelSettings` of the AI library from the stored
choices: `null` when no model is named (the default model of the provider; an effort cannot be validated without a model
and is not used), otherwise the trimmed id, the reasoning effort (`null` when empty), and, for Ollama only, the context
window of the settings. `Describe` joins the problems of a `ModelSettingsException` with a space.

**IOllamaModelCatalogs / OllamaModelCatalogs**: `For(Uri endpoint)` gives the `OllamaModelCatalog` of the server at the
address. The real class keeps the catalog of the address that was asked for last (a new one for another address), so the
readiness check, the factory, and the list share what the server said: describing a model is one request to the server,
and a catalog keeps its answer for five minutes. The HTTP client has no time limit of its own, and each caller bounds
its request with its own token. A test supplies a fake, or a handler for the real class.

#### Key Methods

**ListModelsAsync(settings, refresh, cancellationToken)**: Returns the models of the provider in the settings.

1. *GitHub Copilot.* `CopilotCliLocator.LocateAsync(settings.CopilotCliPath, refresh)` finds the CLI; a CLI that is
   missing or too old is the sentence of the locator and no models. The shared starter then starts, or reuses, the
   runtime on that CLI, and a start that fails is the sentence of the starter. The catalog of the runtime
   (`ICopilotAgentSource.Models`, a `CopilotModelCatalog`) is asked for `ModelQuery { IncludeUnusable = true }`, after
   `RefreshAsync` when `refresh` is set. A catalog that cannot be read (`InvalidOperationException`: the runtime is not
   connected or not signed in) gives the message of the library, and a runtime that was replaced meanwhile
   (`ObjectDisposedException`) gives "The Copilot runtime was stopped while the models were listed; choose Refresh
   models."
2. *Ollama.* An address that is not a valid `http` or `https` address gives `InvalidEndpointMessage`. Otherwise the
   catalog of the server (`IOllamaModelCatalogs.For`) is listed with `IncludeUnusable` (after `RefreshAsync` when
   `refresh` is set) with a limit of 15 seconds linked to the token of the caller. A connection that is refused, a time
   out, an answer that is not what Ollama sends, and the like give "Ollama did not answer at {address}; start Ollama or
   fix the address, then choose Refresh models. You can still type the name of a model." A server without models gives
   `NoOllamaModelsMessage`: "Ollama has no models yet; run `ollama pull <model>` for a model that can call tools, then
   choose Refresh models."
3. *Order.* Every model is returned, those that cannot be used as well: first the ones that can be used, then the
   others, each group by name. Hiding a model would leave the user wondering where it went; the page says what each
   lacks.

**Map(model)**: Public and static: the mapping above.

#### Error Handling

A provider that cannot be asked is a result with no models and one sentence, never an exception, so the page can offer
typing the name and say why. Only the cancellation of the caller is thrown. Exceptions are for programming errors: a
`null` setting or model.

#### Security

The unit reads no token and shows none: the sentences are fixed text, the message of the AI library (which explains and
holds no credential), or an address that the user typed. It starts no process itself; the starter does, on the CLI that
the locator accepted.

#### Dependencies

`CopilotCliLocator`, `ICopilotRuntimeStarter` and `ICopilotAgentSource` (`CopilotRuntimeStarter` unit),
`AiProviderSettings` and the types of `IProviderModelSource.cs` of the
Settings subsystem, and DemaConsulting.AgentKit (`ModelInfo`, `ModelQuery`, `IModelCatalog`, `ModelSettings`,
`OllamaModelCatalog`) with OllamaSharp (`OllamaApiClient`).

#### Callers

The AI Provider page of the Settings subsystem, through `IProviderModelSource` (the `AppShell` subsystem passes the
catalog to the page). `AgentReadiness` and `AgentProviderFactory` use `ModelRequirements` and `IOllamaModelCatalogs`.
