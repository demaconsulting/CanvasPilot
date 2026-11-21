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

namespace DemaConsulting.CanvasPilot.PreviewSubsystem;

/// <summary>
///     One line of the export dialog: a preset with the size line that is shown beside its name and the group it
///     belongs to.
/// </summary>
public sealed class ExportPresetItemViewModel
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="ExportPresetItemViewModel" /> class.
    /// </summary>
    /// <param name="preset">The preset the line stands for.</param>
    public ExportPresetItemViewModel(ExportPreset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);

        Preset = preset;
    }

    /// <summary>
    ///     Gets the preset the line stands for.
    /// </summary>
    public ExportPreset Preset { get; }

    /// <summary>
    ///     Gets the name of the preset.
    /// </summary>
    public string Name => Preset.Name;

    /// <summary>
    ///     Gets the size, the background, and the resolution of the preset as one line.
    /// </summary>
    public string SizeLine => Preset.DescribeSize();

    /// <summary>
    ///     Gets the name of the group the preset is listed under: the picture as it is, the sizes of the style, or the
    ///     sizes of the application.
    /// </summary>
    public string Group => Preset.Origin switch
    {
        ExportPresetOrigin.AsRendered => "As shown",
        ExportPresetOrigin.StyleKit => "Style kit",
        _ => "Application"
    };
}
