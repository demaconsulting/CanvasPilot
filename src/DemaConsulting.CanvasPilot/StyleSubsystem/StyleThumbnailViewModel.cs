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

using CommunityToolkit.Mvvm.ComponentModel;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     The small picture of one example image of a kit in the Style Kits window: the name of the file, its caption, the
///     picture once it has been drawn, and the text that stands in for it while it is drawn or when it could not be.
/// </summary>
public sealed partial class StyleThumbnailViewModel : ObservableObject
{
    /// <summary>
    ///     The picture, or <see langword="null" /> while it is drawn or when it could not be drawn.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    private RenderedImage? _image;

    /// <summary>
    ///     What stands in for the picture, or an empty string once it is shown.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPlaceholder))]
    private string _placeholderText = "Drawing...";

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleThumbnailViewModel" /> class that is waiting for its picture.
    /// </summary>
    /// <param name="fileName">The name of the example file.</param>
    /// <param name="caption">The caption, or an empty string for none.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public StyleThumbnailViewModel(string fileName, string caption)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(caption);

        FileName = fileName;
        Caption = caption;
    }

    /// <summary>
    ///     Gets the name of the example file.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    ///     Gets the caption, or an empty string for none.
    /// </summary>
    public string Caption { get; }

    /// <summary>
    ///     Gets the text under the picture: the caption, or the name of the file when there is none.
    /// </summary>
    public string Label => Caption.Length > 0 ? $"{FileName}: {Caption}" : FileName;

    /// <summary>
    ///     Gets a value indicating whether the picture is there.
    /// </summary>
    public bool HasImage => Image is not null;

    /// <summary>
    ///     Gets a value indicating whether a text stands in for the picture.
    /// </summary>
    public bool HasPlaceholder => PlaceholderText.Length > 0;

    /// <summary>
    ///     Shows the picture that was drawn.
    /// </summary>
    /// <param name="image">The picture.</param>
    public void Show(RenderedImage image)
    {
        ArgumentNullException.ThrowIfNull(image);

        Image = image;
        PlaceholderText = string.Empty;
    }

    /// <summary>
    ///     Shows why the picture could not be drawn.
    /// </summary>
    /// <param name="reason">The reason, as a sentence.</param>
    public void ShowFailure(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        Image = null;
        PlaceholderText = $"Could not be drawn: {reason}";
    }
}
