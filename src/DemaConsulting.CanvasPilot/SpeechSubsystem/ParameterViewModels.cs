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

using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.Speech.ModelManagementSubsystem;

namespace DemaConsulting.CanvasPilot.SpeechSubsystem;

/// <summary>
///     The state that every kind of model parameter shares: its identity and descriptive texts, and its value as an
///     invariant-culture string, which is how the settings file stores it.
/// </summary>
public abstract class ParameterViewModelBase : ObservableObject
{
    /// <summary>
    ///     Initializes the shared state from the library's parameter description.
    /// </summary>
    /// <param name="parameter">The parameter description.</param>
    protected ParameterViewModelBase(ISpeechModelParameter parameter)
    {
        ArgumentNullException.ThrowIfNull(parameter);

        Id = parameter.Id;
        DisplayName = parameter.DisplayName;
        Description = parameter.Description;
    }

    /// <summary>
    ///     Gets the identifier under which the value is stored.
    /// </summary>
    public string Id { get; }

    /// <summary>
    ///     Gets the short label shown for the parameter.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    ///     Gets the longer explanation shown for the parameter.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Gets the current value as an invariant-culture string.
    /// </summary>
    public abstract string ValueText { get; }

    /// <summary>
    ///     Sets the value from an invariant-culture string.
    /// </summary>
    /// <param name="text">The stored text.</param>
    /// <returns>
    ///     <see langword="true" /> when the text was a value of this parameter; <see langword="false" /> when it was
    ///     not, in which case the value is unchanged.
    /// </returns>
    public abstract bool TrySetValueText(string text);
}

/// <summary>
///     A bounded number, shown as a slider, or as a whole-number box when the parameter is an integer. Every
///     written value is clamped to the declared range, and rounded for an integer.
/// </summary>
public sealed class NumericParameterViewModel : ParameterViewModelBase
{
    /// <summary>
    ///     Backing field of <see cref="Value" />.
    /// </summary>
    private double _value;

    /// <summary>
    ///     Creates the parameter at its declared default.
    /// </summary>
    /// <param name="parameter">The parameter description.</param>
    public NumericParameterViewModel(NumericParameter parameter)
        : base(parameter)
    {
        Minimum = parameter.Minimum;
        Maximum = parameter.Maximum;
        Step = parameter.Step;
        Unit = parameter.Unit ?? string.Empty;
        IsInteger = parameter.IsInteger;
        _value = parameter.Default;
    }

    /// <summary>
    ///     Gets the smallest accepted value.
    /// </summary>
    public double Minimum { get; }

    /// <summary>
    ///     Gets the largest accepted value.
    /// </summary>
    public double Maximum { get; }

    /// <summary>
    ///     Gets the smallest useful change of the value.
    /// </summary>
    public double Step { get; }

    /// <summary>
    ///     Gets the unit label, or an empty string.
    /// </summary>
    public string Unit { get; }

    /// <summary>
    ///     Gets a value indicating whether only whole numbers make sense.
    /// </summary>
    public bool IsInteger { get; }

    /// <summary>
    ///     Gets or sets the value. A write is clamped to the range and, for an integer, rounded; a number that is
    ///     not finite is ignored.
    /// </summary>
    public double Value
    {
        get => _value;
        set
        {
            if (!double.IsFinite(value))
            {
                return;
            }

            var clamped = Math.Clamp(value, Minimum, Maximum);
            SetProperty(ref _value, IsInteger ? Math.Round(clamped, MidpointRounding.AwayFromZero) : clamped);
        }
    }

    /// <inheritdoc />
    public override string ValueText => Value.ToString("R", CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public override bool TrySetValueText(string text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)
            || !double.IsFinite(number))
        {
            return false;
        }

        Value = number;
        return true;
    }
}

/// <summary>
///     One option of a choice parameter, as the drop-down list shows it.
/// </summary>
/// <remarks>
///     It stands for one <see cref="ChoiceParameterOption" /> of the speech library, whose text prints its members
///     and cannot be changed. A closed drop-down list tells assistive technology the text of its chosen item, so the
///     options are shown through this record, whose text is the label the list shows.
/// </remarks>
/// <param name="Value">The value stored in the settings.</param>
/// <param name="Label">The text shown in the list.</param>
public sealed record ParameterChoice(string Value, string Label)
{
    /// <summary>
    ///     Gives the label shown for the option, so a screen reader announces the chosen option by the words the
    ///     list shows.
    /// </summary>
    /// <returns>The <see cref="Label" />.</returns>
    public override string ToString() => Label;
}

/// <summary>
///     One of a fixed set of options, shown as a drop-down list.
/// </summary>
public sealed class ChoiceParameterViewModel : ParameterViewModelBase
{
    /// <summary>
    ///     Backing field of <see cref="SelectedOption" />.
    /// </summary>
    private ParameterChoice _selectedOption;

    /// <summary>
    ///     Creates the parameter at the option that is its declared default.
    /// </summary>
    /// <param name="parameter">The parameter description.</param>
    public ChoiceParameterViewModel(ChoiceParameter parameter)
        : base(parameter)
    {
        Options = parameter.Options.Select(option => new ParameterChoice(option.Value, option.Label)).ToList();
        _selectedOption = Options.First(option => string.Equals(option.Value, parameter.Default, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Gets the options in the order the model declares them.
    /// </summary>
    public IReadOnlyList<ParameterChoice> Options { get; }

    /// <summary>
    ///     Gets or sets the chosen option. A null or unknown option is ignored, so the value is always one the
    ///     model declared.
    /// </summary>
    public ParameterChoice SelectedOption
    {
        get => _selectedOption;
        set
        {
            var member = value is null
                ? null
                : Options.FirstOrDefault(option => string.Equals(option.Value, value.Value, StringComparison.Ordinal));
            if (member is not null)
            {
                SetProperty(ref _selectedOption, member);
            }
        }
    }

    /// <inheritdoc />
    public override string ValueText => SelectedOption.Value;

    /// <inheritdoc />
    public override bool TrySetValueText(string text)
    {
        var member = Options.FirstOrDefault(option => string.Equals(option.Value, text, StringComparison.Ordinal));
        if (member is null)
        {
            return false;
        }

        SelectedOption = member;
        return true;
    }
}

/// <summary>
///     A two-state switch, shown as a check box.
/// </summary>
public sealed partial class BooleanParameterViewModel : ParameterViewModelBase
{
    /// <summary>
    ///     Whether the switch is on.
    /// </summary>
    [ObservableProperty]
    private bool _value;

    /// <summary>
    ///     Creates the parameter at its declared default.
    /// </summary>
    /// <param name="parameter">The parameter description.</param>
    public BooleanParameterViewModel(BooleanParameter parameter)
        : base(parameter)
    {
        _value = parameter.Default;
    }

    /// <inheritdoc />
    public override string ValueText => Value ? "true" : "false";

    /// <inheritdoc />
    public override bool TrySetValueText(string text)
    {
        if (!bool.TryParse(text, out var flag))
        {
            return false;
        }

        Value = flag;
        return true;
    }
}
