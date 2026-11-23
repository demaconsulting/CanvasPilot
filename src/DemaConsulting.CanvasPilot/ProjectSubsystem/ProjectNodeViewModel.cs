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

namespace DemaConsulting.CanvasPilot.ProjectSubsystem;

/// <summary>
///     What kind of entry of the project folder a node of the explorer tree is, which decides its icon. The kind is never the
///     only sign of what the entry is: the name of the file, with its extension, is shown beside the icon.
/// </summary>
public enum ProjectNodeKind
{
    /// <summary>A folder.</summary>
    Folder,

    /// <summary>An SVG picture.</summary>
    Svg,

    /// <summary>A Mermaid diagram.</summary>
    Mermaid,

    /// <summary>A Vega-Lite chart.</summary>
    VegaLite,

    /// <summary>Another source that CanvasNet draws, such as PlantUML or OpenSCAD.</summary>
    Source,

    /// <summary>A PDF document.</summary>
    Pdf,

    /// <summary>A bitmap picture, such as PNG or JPEG.</summary>
    Bitmap,

    /// <summary>The <c>style.json</c> file of a style kit.</summary>
    Style
}

/// <summary>
///     A folder or a file in the explorer tree of the open project. A folder holds its children and can be expanded; a file
///     can be selected and, when CanvasNet draws it, previewed.
/// </summary>
public sealed partial class ProjectNodeViewModel : ObservableObject
{
    /// <summary>
    ///     The children of a folder, which a file does not have.
    /// </summary>
    private readonly ObservableCollection<ProjectNodeViewModel> _children = [];

    /// <summary>
    ///     Whether a folder is expanded in the tree.
    /// </summary>
    [ObservableProperty]
    private bool _isExpanded;

    /// <summary>
    ///     Creates a node.
    /// </summary>
    /// <param name="name">The name of the file or folder, which is shown.</param>
    /// <param name="fullPath">The full path of the file or folder.</param>
    /// <param name="isFolder">Whether the node is a folder.</param>
    /// <param name="kind">What kind of entry the node is.</param>
    /// <param name="isPreviewable">Whether CanvasNet draws the file, which then can be previewed.</param>
    public ProjectNodeViewModel(string name, string fullPath, bool isFolder, ProjectNodeKind kind, bool isPreviewable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        Name = name;
        FullPath = fullPath;
        IsFolder = isFolder;
        Kind = kind;
        IsPreviewable = isPreviewable && !isFolder;
        Children = new ReadOnlyObservableCollection<ProjectNodeViewModel>(_children);
    }

    /// <summary>
    ///     Gets the name of the file or folder.
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///     Gets the full path of the file or folder.
    /// </summary>
    public string FullPath { get; }

    /// <summary>
    ///     Gets a value indicating whether the node is a folder.
    /// </summary>
    public bool IsFolder { get; }

    /// <summary>
    ///     Gets what kind of entry the node is.
    /// </summary>
    public ProjectNodeKind Kind { get; }

    /// <summary>
    ///     Gets a value indicating whether CanvasNet draws the file, so that selecting it shows a preview.
    /// </summary>
    public bool IsPreviewable { get; }

    /// <summary>
    ///     Gets the format of a file in capital letters, such as <c>SVG</c> or <c>VL.JSON</c>, or an empty string for a folder.
    /// </summary>
    public string Format => IsFolder ? string.Empty : FormatOf(Name);

    /// <summary>
    ///     Gets the children of a folder, folders first and then files, each sorted by name ignoring case.
    /// </summary>
    public ReadOnlyObservableCollection<ProjectNodeViewModel> Children { get; }

    /// <summary>
    ///     Gets the collection that the explorer changes to follow the folder on disk.
    /// </summary>
    internal ObservableCollection<ProjectNodeViewModel> MutableChildren => _children;

    /// <summary>
    ///     Tells the kind of entry that a file with a name is.
    /// </summary>
    /// <param name="fileName">The file name, with its extension.</param>
    /// <returns>The kind; <see cref="ProjectNodeKind.Source" /> for a source that CanvasNet draws and that has no kind of its own.</returns>
    public static ProjectNodeKind KindOf(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (name.Equals("style.json", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectNodeKind.Style;
        }

        if (name.EndsWith(".vl.json", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".vl", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".vega-lite.json", StringComparison.OrdinalIgnoreCase))
        {
            return ProjectNodeKind.VegaLite;
        }

        return Path.GetExtension(name).ToLowerInvariant() switch
        {
            ".svg" => ProjectNodeKind.Svg,
            ".mmd" or ".mermaid" => ProjectNodeKind.Mermaid,
            ".pdf" => ProjectNodeKind.Pdf,
            ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" => ProjectNodeKind.Bitmap,
            _ => ProjectNodeKind.Source
        };
    }

    /// <summary>
    ///     Tells whether a file is one that the explorer lists although CanvasNet may not draw it: a bitmap, a PDF, or the
    ///     <c>style.json</c> file of a kit.
    /// </summary>
    /// <param name="fileName">The file name, with its extension.</param>
    /// <returns><see langword="true" /> when the explorer lists the file.</returns>
    public static bool IsListedAsset(string fileName)
    {
        return KindOf(fileName) is ProjectNodeKind.Bitmap or ProjectNodeKind.Pdf or ProjectNodeKind.Style;
    }

    /// <summary>
    ///     Gives the format of a file name in capital letters: its extension, or all of a compound extension such as
    ///     <c>.vl.json</c>.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The format, or an empty string when the name has no extension.</returns>
    private static string FormatOf(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (name.EndsWith(".vl.json", StringComparison.OrdinalIgnoreCase))
        {
            return "VL.JSON";
        }

        var extension = Path.GetExtension(name);
        return extension.Length > 1 ? extension[1..].ToUpperInvariant() : string.Empty;
    }
}
