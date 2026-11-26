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
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using DemaConsulting.Speech.ModelManagementSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="ModelParameterEditorViewModel" /> and the numeric, choice, and boolean parameter
///     view models, with substitute models.
/// </summary>
public sealed class ModelParameterEditorTests
{
    /// <summary>
    ///     Test that an editor shows one entry per declared parameter, of the right kind and at its default, in the
    ///     declared order.
    /// </summary>
    [Fact]
    public void ModelParameterEditorViewModel_Model_DeclaresAllKinds_BuildsEditorsAtDefaults()
    {
        // Arrange: a model that declares a number, a choice, and a switch
        var editor = new ModelParameterEditorViewModel("Settings");
        var model = SpeechTestData.Model(
            "tts",
            SpeechModelRole.Synthesis,
            SpeechTestData.Numeric(),
            SpeechTestData.Choice(),
            SpeechTestData.Boolean(defaultValue: true));

        // Act: choose the model
        editor.Model = model;

        // Assert: three editors of the right kinds, at their defaults
        Assert.Equal("Settings", editor.Title);
        Assert.True(editor.HasModel);
        Assert.True(editor.HasParameters);
        Assert.Equal(string.Empty, editor.EmptyMessage);
        Assert.IsType<NumericParameterViewModel>(editor.Parameters[0]);
        var choice = Assert.IsType<ChoiceParameterViewModel>(editor.Parameters[1]);
        var boolean = Assert.IsType<BooleanParameterViewModel>(editor.Parameters[2]);
        Assert.Equal(5, ((NumericParameterViewModel)editor.Parameters[0]).Value);
        Assert.Equal("b", choice.SelectedOption.Value);
        Assert.True(boolean.Value);
        Assert.Equal(["speed", "voice", "echo"], editor.Parameters.Select(parameter => parameter.Id));
        Assert.Equal("Speed", editor.Parameters[0].DisplayName);
        Assert.Equal("How fast.", editor.Parameters[0].Description);
    }

    /// <summary>
    ///     Test that no model, a model without parameters, and a parameter of an unknown kind are all explained or
    ///     skipped without failing.
    /// </summary>
    [Fact]
    public void ModelParameterEditorViewModel_Model_NoneOrWithoutParametersOrUnknownKind_ExplainsOrSkips()
    {
        // Arrange: an editor with no model
        var editor = new ModelParameterEditorViewModel("Settings");
        var withoutModel = (editor.HasModel, editor.HasParameters, editor.EmptyMessage);

        // Act: choose a model without parameters, then one with an unknown kind of parameter and a known one
        editor.Model = SpeechTestData.Model("stt", SpeechModelRole.Recognition);
        var withoutParameters = (editor.HasModel, editor.HasParameters, editor.EmptyMessage);
        editor.Model = SpeechTestData.Model("tts", SpeechModelRole.Synthesis, Substitute.For<ISpeechModelParameter>(), SpeechTestData.Boolean());
        var withUnknownKind = editor.Parameters.Count;
        editor.Model = null;

        // Assert: each state has its own explanation, and the unknown kind was skipped
        Assert.Equal((false, false, ModelParameterEditorViewModel.NoModelMessage), withoutModel);
        Assert.Equal((true, false, ModelParameterEditorViewModel.NoParametersMessage), withoutParameters);
        Assert.Equal(1, withUnknownKind);
        Assert.Empty(editor.Parameters);
        Assert.Equal(ModelParameterEditorViewModel.NoModelMessage, editor.EmptyMessage);
    }

    /// <summary>
    ///     Test that a numeric value is clamped to its range, rounded for an integer, and that a number that is not
    ///     finite is ignored.
    /// </summary>
    [Fact]
    public void NumericParameterViewModel_Value_OutOfRangeOrFractionOrNotFinite_IsConstrained()
    {
        // Arrange: a continuous and an integer parameter from 0 to 10
        var continuous = new NumericParameterViewModel(SpeechTestData.Numeric());
        var whole = new NumericParameterViewModel(SpeechTestData.Numeric("index", isInteger: true));

        // Act: write values outside the range, a fraction, and numbers that are not finite
        continuous.Value = 99;
        var high = continuous.Value;
        continuous.Value = -4;
        var low = continuous.Value;
        continuous.Value = 2.25;
        whole.Value = 2.5;
        var rounded = whole.Value;
        whole.Value = double.NaN;
        whole.Value = double.PositiveInfinity;

        // Assert: values stay in range, an integer is rounded, and the infinities and NaN changed nothing
        Assert.Equal(10, high);
        Assert.Equal(0, low);
        Assert.Equal(2.25, continuous.Value);
        Assert.Equal(3, rounded);
        Assert.Equal(3, whole.Value);
        Assert.Equal("x", continuous.Unit);
        Assert.Equal((0d, 10d, 1d), (continuous.Minimum, continuous.Maximum, continuous.Step));
        Assert.True(whole.IsInteger);
    }

    /// <summary>
    ///     Test that every option of a choice, and the chosen one, reads as the label the list shows, so a screen reader
    ///     announces the chosen option by its words and not by the record of the speech library.
    /// </summary>
    [Fact]
    public void ChoiceParameterViewModel_Options_ReadAsTheirLabels()
    {
        // Arrange: a choice at its default
        var choice = new ChoiceParameterViewModel(SpeechTestData.Choice());

        // Act: read the text of every option and of the chosen one
        var texts = choice.Options.Select(option => option.ToString()).ToList();
        var chosen = choice.SelectedOption.ToString();

        // Assert: each text is the shown label, in the declared order, and never the record
        Assert.Equal(["Anna", "Ben", "Cleo"], texts);
        Assert.Equal(["a", "b", "c"], choice.Options.Select(option => option.Value));
        Assert.Equal("Ben", chosen);
        Assert.All(texts, text => Assert.DoesNotContain(" { ", text, StringComparison.Ordinal));
    }

    /// <summary>
    ///     Test that a choice accepts only the options the model declared.
    /// </summary>
    [Fact]
    public void ChoiceParameterViewModel_SelectedOption_UnknownOrNull_IsIgnored()
    {
        // Arrange: a choice at its default
        var choice = new ChoiceParameterViewModel(SpeechTestData.Choice());

        // Act: choose a declared option, an unknown one, and none
        choice.SelectedOption = choice.Options[2];
        choice.SelectedOption = new ParameterChoice("zzz", "Unknown");
        choice.SelectedOption = null!;

        // Assert: the declared choice stands
        Assert.Equal("c", choice.SelectedOption.Value);
        Assert.Equal("c", choice.ValueText);
        Assert.False(choice.TrySetValueText("zzz"));
        Assert.True(choice.TrySetValueText("a"));
        Assert.Equal("a", choice.ValueText);
    }

    /// <summary>
    ///     Test that values are written as invariant-culture strings whatever the current culture is.
    /// </summary>
    [Fact]
    public void ModelParameterEditorViewModel_GetValues_GermanCulture_WritesInvariantStrings()
    {
        // Arrange: an editor with a fractional number, a choice, and a switch, under a culture that writes commas
        var editor = new ModelParameterEditorViewModel("Settings")
        {
            Model = SpeechTestData.Model("tts", SpeechModelRole.Synthesis, SpeechTestData.Numeric(), SpeechTestData.Choice(), SpeechTestData.Boolean()),
        };
        var original = CultureInfo.CurrentCulture;
        IReadOnlyDictionary<string, string> values;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            ((NumericParameterViewModel)editor.Parameters[0]).Value = 2.5;
            ((BooleanParameterViewModel)editor.Parameters[2]).Value = true;

            // Act: read the values
            values = editor.GetValues();
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        // Assert: the number uses a point, and the switch is lower case
        Assert.Equal("2.5", values["speed"]);
        Assert.Equal("b", values["voice"]);
        Assert.Equal("true", values["echo"]);
    }

    /// <summary>
    ///     Test that values written by one editor are read back by a new editor of the same model.
    /// </summary>
    [Fact]
    public void ModelParameterEditorViewModel_GetSetValues_RoundTrips()
    {
        // Arrange: an editor with edited values
        var model = SpeechTestData.Model("tts", SpeechModelRole.Synthesis, SpeechTestData.Numeric(), SpeechTestData.Choice(), SpeechTestData.Boolean());
        var first = new ModelParameterEditorViewModel("Settings") { Model = model };
        ((NumericParameterViewModel)first.Parameters[0]).Value = 7.75;
        ((ChoiceParameterViewModel)first.Parameters[1]).SelectedOption = ((ChoiceParameterViewModel)first.Parameters[1]).Options[0];
        ((BooleanParameterViewModel)first.Parameters[2]).Value = true;
        var stored = first.GetValues();

        // Act: restore the stored values into a new editor
        var second = new ModelParameterEditorViewModel("Settings") { Model = model };
        second.SetValues(stored);

        // Assert: the new editor shows the same values
        Assert.Equal(7.75, ((NumericParameterViewModel)second.Parameters[0]).Value);
        Assert.Equal("a", ((ChoiceParameterViewModel)second.Parameters[1]).SelectedOption.Value);
        Assert.True(((BooleanParameterViewModel)second.Parameters[2]).Value);
        Assert.Equal(stored, second.GetValues());
    }

    /// <summary>
    ///     Test that stored values for unknown parameters, and texts that are not values, are ignored and leave the
    ///     parameter as it is.
    /// </summary>
    [Fact]
    public void ModelParameterEditorViewModel_SetValues_UnknownOrInvalid_AreIgnored()
    {
        // Arrange: an editor at its defaults
        var editor = new ModelParameterEditorViewModel("Settings")
        {
            Model = SpeechTestData.Model("tts", SpeechModelRole.Synthesis, SpeechTestData.Numeric(), SpeechTestData.Choice(), SpeechTestData.Boolean()),
        };

        // Act: set values that do not fit, and one for a parameter the model does not declare
        editor.SetValues(new Dictionary<string, string>
        {
            ["speed"] = "fast",
            ["voice"] = "zzz",
            ["echo"] = "maybe",
            ["gone"] = "1",
        });

        // Assert: nothing changed
        Assert.Equal("5", editor.Parameters[0].ValueText);
        Assert.Equal("b", editor.Parameters[1].ValueText);
        Assert.Equal("false", editor.Parameters[2].ValueText);
        Assert.False(editor.Parameters[0].TrySetValueText("NaN"));
        Assert.False(editor.Parameters[2].TrySetValueText(string.Empty));
    }

    /// <summary>
    ///     Test that a stored number outside the range is clamped to it.
    /// </summary>
    [Fact]
    public void ModelParameterEditorViewModel_SetValues_OutOfRangeNumber_IsClamped()
    {
        // Arrange: an editor with a number from 0 to 10
        var editor = new ModelParameterEditorViewModel("Settings")
        {
            Model = SpeechTestData.Model("tts", SpeechModelRole.Synthesis, SpeechTestData.Numeric()),
        };

        // Act: restore a stored value that is too large
        editor.SetValues(new Dictionary<string, string> { ["speed"] = "1000" });

        // Assert: the value is the largest accepted one
        Assert.Equal("10", editor.Parameters[0].ValueText);
    }

    /// <summary>
    ///     Test that an editor needs a title and values to set.
    /// </summary>
    [Fact]
    public void ModelParameterEditorViewModel_InvalidArguments_Throw()
    {
        // Arrange: an editor
        var editor = new ModelParameterEditorViewModel("Settings");

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new ModelParameterEditorViewModel(" "));
        Assert.Throws<ArgumentNullException>(() => editor.SetValues(null!));
    }
}
