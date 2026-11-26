### ModelParameterEditor

#### Verification Approach

`ModelParameterEditor` is verified by unit tests in `ModelParameterEditorTests.cs`, which drive
`ModelParameterEditorViewModel` and the numeric, choice, and boolean parameter view models over substitute models
(NSubstitute) that declare real library parameter descriptors, and by one headless UI scenario in
`SpeechSettingsPageViewTests.cs` that shows the view. One test runs under a German culture, which writes decimals
with a comma, to prove that stored values do not depend on the culture.

#### Test Environment

Unit tests use the standard test environment. The UI test runs under the Avalonia headless platform with the Skia
renderer, serialized with test parallelization disabled. No file, network, or display is required.

#### Acceptance Criteria

- All unit and UI tests pass with zero failures.
- One editor of the right kind appears for each parameter, in the declared order and at the declared default, a
  parameter of an unknown kind is skipped, and each empty state has its own explanation.
- Numbers are clamped and rounded as declared, a number that is not finite is ignored, a choice accepts only declared
  options, and stored texts that are not values or that name unknown parameters are ignored.
- Values are written as invariant-culture strings under a culture that uses commas, and read back unchanged.

#### Test Scenarios

**ModelParameterEditorViewModel_Model_DeclaresAllKinds_BuildsEditorsAtDefaults**: A model declares a number, a choice,
and a switch. Three editors of those kinds appear, in that order, at the defaults 5, `b`, and on, with the declared
display name and description. This scenario is tested by
`ModelParameterEditorViewModel_Model_DeclaresAllKinds_BuildsEditorsAtDefaults`.

**ModelParameterEditorViewModel_Model_NoneOrWithoutParametersOrUnknownKind_ExplainsOrSkips**: With no model the
editor explains that a model must be chosen; a model without parameters is explained as having no settings; a model
with an unknown parameter kind and a switch shows only the switch; removing the model clears the editors. This
scenario is tested by `ModelParameterEditorViewModel_Model_NoneOrWithoutParametersOrUnknownKind_ExplainsOrSkips`.

**NumericParameterViewModel_Value_OutOfRangeOrFractionOrNotFinite_IsConstrained**: Values above and below the range
are clamped, a fraction is kept for a continuous number and rounded for an integer, and NaN and infinity change
nothing. This scenario is tested by `NumericParameterViewModel_Value_OutOfRangeOrFractionOrNotFinite_IsConstrained`.

**ChoiceParameterViewModel_SelectedOption_UnknownOrNull_IsIgnored**: A declared option is chosen; an unknown option
and none are ignored, and a stored text is accepted only when it is a declared option. This scenario is tested by
`ChoiceParameterViewModel_SelectedOption_UnknownOrNull_IsIgnored`.

**ModelParameterEditorViewModel_GetValues_GermanCulture_WritesInvariantStrings**: Under the German culture a number
of 2.5 and a switch that is on are written as `2.5` and `true`, and the choice as its option value. This scenario is
tested by `ModelParameterEditorViewModel_GetValues_GermanCulture_WritesInvariantStrings`.

**ModelParameterEditorViewModel_GetSetValues_RoundTrips**: Edited values of all three kinds are read, restored into a
new editor of the same model, and read again; the values match. This scenario is tested by
`ModelParameterEditorViewModel_GetSetValues_RoundTrips`.

**ModelParameterEditorViewModel_SetValues_UnknownOrInvalid_AreIgnored**: Stored texts that are not values, and an entry
for a parameter the model does not declare, change nothing, and the parameters report that the texts were refused.
This scenario is tested by `ModelParameterEditorViewModel_SetValues_UnknownOrInvalid_AreIgnored`.

**ModelParameterEditorViewModel_SetValues_OutOfRangeNumber_IsClamped**: A stored value of 1000 for a number from 0 to
10 is restored as 10. This scenario is tested by `ModelParameterEditorViewModel_SetValues_OutOfRangeNumber_IsClamped`.

**ModelParameterEditorViewModel_InvalidArguments_Throw**: A blank title and missing values are refused. This scenario
is tested by `ModelParameterEditorViewModel_InvalidArguments_Throw`.

**SpeechSettingsPageView_LoadedSettings_ShowDevicesModelsAndParameters**: A page is shown over a text-to-speech model
that declares a voice choice with the default `b`. The parameter drop-down list is shown with `b` selected. This
scenario is tested by `SpeechSettingsPageView_LoadedSettings_ShowDevicesModelsAndParameters`.

**ChoiceParameterViewModel_Options_ReadAsTheirLabels**: A choice of three options at its default; the options keep the
declared order and values, the text of each, and of the chosen one, is its label, and none is the printout of a record.
This scenario is tested by `ChoiceParameterViewModel_Options_ReadAsTheirLabels`.

**SpeechSettingsPageView_ParameterList_AnnouncesTheShownOption**: A shown Speech page with a synthesis model that has a
voice parameter; the value that the closed list gives to automation is the label of the default option, and after the
first option is chosen, its label. This scenario is tested by
`SpeechSettingsPageView_ParameterList_AnnouncesTheShownOption`.
