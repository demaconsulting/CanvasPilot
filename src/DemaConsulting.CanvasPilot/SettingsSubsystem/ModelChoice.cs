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

namespace DemaConsulting.CanvasPilot.SettingsSubsystem;

/// <summary>
///     One entry of the model list of the AI Provider page: a model that the provider lists, or the entry that stands for
///     the default model of the provider.
/// </summary>
/// <param name="Id">The id that is stored in the settings; an empty string for the default model of the provider.</param>
/// <param name="Label">The text that the list shows, which says in a few words what the model cannot do.</param>
/// <param name="Info">What the provider says about the model; <see langword="null" /> for the default entry.</param>
public sealed record ModelChoice(string Id, string Label, ProviderModelInfo? Info)
{
    /// <summary>
    ///     Gets a value indicating whether the entry can be chosen: a model that the provider lists but says cannot be used
    ///     is shown disabled.
    /// </summary>
    public bool IsSelectable => Info is null || Info.IsUsable;

    /// <inheritdoc />
    public override string ToString() => Label;
}
