### ModelParameterEditor

![Speech Structure](SpeechSubsystemView.svg)

#### Purpose

`ModelParameterEditor` is the view model and view that edit the tunable parameters of one speech model. A model
declares its parameters as typed descriptors, so the editor builds one numeric, choice, or boolean editor for each
without knowing the model in advance, and it reads and writes the values as invariant-culture strings, which is how
the settings file stores them.

#### Data Model

**ModelParameterEditorViewModel**: `Title`, `Model` (the model whose parameters are shown, or `null`),
`Parameters` (`ObservableCollection<ParameterViewModelBase>`), `HasModel`, `HasParameters`, and `EmptyMessage`
("Choose a model to see its settings." or "This model has no settings.", or empty when parameters are shown).

**ParameterViewModelBase**: `Id`, `DisplayName`, `Description`, `ValueText` (the value as an invariant-culture
string), and `TrySetValueText(text)`, which returns whether the text was a value of the parameter.

**NumericParameterViewModel**: `Minimum`, `Maximum`, `Step`, `Unit`, `IsInteger`, and `Value`. Every write is clamped
to the range and rounded to a whole number for an integer; a number that is not finite is ignored. `ValueText` uses
the shortest text that reads back as the same number, with a point as the separator.

**ChoiceParameterViewModel**: `Options` and `SelectedOption`. A null or unknown option is ignored, so the value is
always one the model declared. `ValueText` is the option's value.

**BooleanParameterViewModel**: `Value`. `ValueText` is `true` or `false`; either case is read.

**View**: `ModelParameterEditorView` is an Avalonia user control with a heading, an explanation line, and a list that
shows each parameter with a template for its kind: a slider or a whole-number box, a drop-down list, or a check box
(`SpeechParametersEmptyMessage`, `SpeechParameterList`, `SpeechParameterSlider`, `SpeechParameterNumberBox`,
`SpeechParameterComboBox`, and `SpeechParameterCheckBox`).

#### Key Methods

**Model** (setting): Rebuilds `Parameters` from the library's `ISpeechModel.Parameters`, each at its declared
default. A parameter of a kind the editor does not know is skipped, so a model from a newer library cannot break the
page.

**GetValues**: Returns every parameter's `ValueText` by parameter identifier.

**SetValues**: Sets each parameter that has an entry through `TrySetValueText`. An entry for a parameter the model
does not declare, and a text that is not a value, is ignored and leaves the parameter as it is. A number outside the
range is clamped.

#### Error Handling

Invalid values are ignored, never thrown, because they come from a settings file that can be edited by hand or
written for another version of a model. A blank title and missing values are refused with `ArgumentException` and
`ArgumentNullException`.

#### Dependencies

- **DemaConsulting.Speech** (OTS) — `ISpeechModel`, `NumericParameter`, `ChoiceParameter`, `BooleanParameter`, and
  `ChoiceParameterOption`; see the Speech OTS design.
- **CommunityToolkit.Mvvm** and **Avalonia** — property change notification and the view.

#### Callers

- `SpeechSettingsPage` — composes one editor for the chosen speech-to-text model and one for the chosen
  text-to-speech model, and stores their values.
