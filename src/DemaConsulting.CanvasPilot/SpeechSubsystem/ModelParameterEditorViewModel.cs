// Copyright (c) DEMA Consulting
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     View model of the editor for the tunable parameters of one speech model. It builds one numeric, choice, or
///     boolean editor for each parameter the model declares, so no model needs to be known in advance, and reads
///     and writes the values as a bag of invariant-culture strings, the form the settings file stores.
/// </summary>
public sealed partial class ModelParameterEditorViewModel : ObservableObject
{
    /// <summary>
    ///     Text shown when no model is chosen.
    /// </summary>
    public const string NoModelMessage = "Choose a model to see its settings.";

    /// <summary>
    ///     Text shown when the chosen model has no tunable parameter.
    /// </summary>
    public const string NoParametersMessage = "This model has no settings.";

    /// <summary>
    ///     The model whose parameters are shown.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasModel), nameof(HasParameters), nameof(EmptyMessage))]
    private ISpeechModel? _model;

    /// <summary>
    ///     Creates an editor with no model.
    /// </summary>
    /// <param name="title">The heading of the editor.</param>
    public ModelParameterEditorViewModel(string title)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        Title = title;
    }

    /// <summary>
    ///     Gets the heading of the editor.
    /// </summary>
    public string Title { get; }

    /// <summary>
    ///     Gets the parameter editors of the chosen model, in the order the model declares them.
    /// </summary>
    public ObservableCollection<ParameterViewModelBase> Parameters { get; } = [];

    /// <summary>
    ///     Gets a value indicating whether a model is chosen.
    /// </summary>
    public bool HasModel => Model is not null;

    /// <summary>
    ///     Gets a value indicating whether the chosen model has at least one tunable parameter.
    /// </summary>
    public bool HasParameters => Parameters.Count > 0;

    /// <summary>
    ///     Gets the text that explains why no parameter is shown, or an empty string when some are.
    /// </summary>
    public string EmptyMessage
    {
        get
        {
            if (HasParameters)
            {
                return string.Empty;
            }

            return HasModel ? NoParametersMessage : NoModelMessage;
        }
    }

    /// <summary>
    ///     Reads the current value of every parameter as an invariant-culture string.
    /// </summary>
    /// <returns>The values by parameter identifier; empty when no model is chosen or it has no parameter.</returns>
    public IReadOnlyDictionary<string, string> GetValues()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var parameter in Parameters)
        {
            values[parameter.Id] = parameter.ValueText;
        }

        return values;
    }

    /// <summary>
    ///     Sets parameters from stored invariant-culture strings. An entry for a parameter the model does not
    ///     declare, and a text that is not a value of its parameter, is ignored and leaves the parameter as it is.
    /// </summary>
    /// <param name="values">The stored values by parameter identifier.</param>
    public void SetValues(IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        foreach (var parameter in Parameters)
        {
            if (values.TryGetValue(parameter.Id, out var text) && text is not null)
            {
                parameter.TrySetValueText(text);
            }
        }
    }

    /// <summary>
    ///     Builds the parameter editors of the newly chosen model.
    /// </summary>
    /// <param name="value">The chosen model, or <see langword="null" />.</param>
    partial void OnModelChanged(ISpeechModel? value)
    {
        Parameters.Clear();
        if (value is not null)
        {
            foreach (var parameter in value.Parameters)
            {
                // A parameter of a kind this editor does not know is skipped, so a newer model cannot break the page.
                ParameterViewModelBase? editor = parameter switch
                {
                    NumericParameter numeric => new NumericParameterViewModel(numeric),
                    ChoiceParameter choice => new ChoiceParameterViewModel(choice),
                    BooleanParameter boolean => new BooleanParameterViewModel(boolean),
                    _ => null,
                };
                if (editor is not null)
                {
                    Parameters.Add(editor);
                }
            }
        }

        OnPropertyChanged(nameof(HasParameters));
        OnPropertyChanged(nameof(EmptyMessage));
    }
}
