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

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     One example image in the editor of a kit: the file, which is in the <c>examples</c> folder of the kit already or
///     is chosen by the user and copied there when the kit is saved, and its caption.
/// </summary>
public sealed partial class StyleExampleRowViewModel : ObservableObject
{
    /// <summary>
    ///     What the example shows.
    /// </summary>
    [ObservableProperty]
    private string _caption;

    /// <summary>
    ///     The name of the file in the <c>examples</c> folder; for a new file it is made unique when the kit is saved.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayText))]
    private string _fileName;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleExampleRowViewModel" /> class.
    /// </summary>
    /// <param name="fileName">The name of the file in the <c>examples</c> folder.</param>
    /// <param name="caption">The caption; empty for none.</param>
    /// <param name="sourcePath">The full path of the file as it is now: in the kit folder, or the file that the user chose.</param>
    /// <param name="isNew"><see langword="true" /> for a file that is copied into the kit when it is saved.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public StyleExampleRowViewModel(string fileName, string caption, string sourcePath, bool isNew)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(caption);
        ArgumentNullException.ThrowIfNull(sourcePath);

        _fileName = fileName;
        _caption = caption;
        SourcePath = sourcePath;
        IsNew = isNew;
    }

    /// <summary>
    ///     Gets the full path of the file as it is now, or an empty string for a kit that has no folder.
    /// </summary>
    public string SourcePath { get; }

    /// <summary>
    ///     Gets a value indicating whether the file is chosen by the user and not yet in the kit.
    /// </summary>
    public bool IsNew { get; }

    /// <summary>
    ///     Gets the text that says where the example is: the name of the file, and that it is new when it will be copied.
    /// </summary>
    public string DisplayText => IsNew ? $"{FileName} (new)" : FileName;
}
