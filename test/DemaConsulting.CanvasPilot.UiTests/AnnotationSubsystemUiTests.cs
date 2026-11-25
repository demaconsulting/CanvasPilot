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

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

using Path = Avalonia.Controls.Shapes.Path;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests of the Annotation subsystem with its view: the registry of marks, the layer's view model, and
///     the layer's view together, on a picture that the real render service drew, with only the source of the shown
///     picture replaced by a simple stand-in for the preview pane.
/// </summary>
public sealed class AnnotationSubsystemUiTests
{
    /// <summary>
    ///     An SVG of 800 x 400 pixels, drawn by the real render service as the picture the marks are made on.
    /// </summary>
    private const string Picture =
        "<svg xmlns='http://www.w3.org/2000/svg' width='800' height='400'>" +
        "<rect width='800' height='400' fill='#0000ff'/></svg>";

    /// <summary>
    ///     Test that the layer draws the note of each shown mark beside its number on one line, cuts a long note short
    ///     with an ellipsis inside the picture, draws no label beside a mark without a note, and gives the number, the
    ///     line, and the label of a noted mark the whole note as their tooltip.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationSubsystem_ShowMarkNotes_OverlayOnRenderedPicture_DrawsLabelsWithEllipsisAndTooltips()
    {
        // Arrange: a drawn picture that is shown, a registry, an overlay joined to a stand-in for the message, and the
        // layer's view in a window as large as the picture
        var folder = Directory.CreateTempSubdirectory("canvaspilot-ui-annotation-subsystem-").FullName;
        try
        {
            var picture = await RenderAsync(folder, "blue.svg", Picture);
            var source = new ShownPictureSource { CurrentImage = picture };
            var registry = new MarkRegistry();
            using var overlay = new AnnotationOverlayViewModel(registry, source);
            var chips = new List<Guid>();
            overlay.MarkAdded += (_, e) =>
            {
                chips.Add(e.Mark.Id);
                overlay.SetActiveMarkIds(chips);
            };
            var view = new AnnotationOverlayView { DataContext = overlay, Width = picture.Width, Height = picture.Height };
            var window = new Window { Width = picture.Width, Height = picture.Height, Content = view };
            window.Show();
            var longNote = string.Join(' ', Enumerable.Repeat("this part of the drawing needs another color", 4));

            // Act: draw a mark with a short note, a mark with a long note near the right edge, and a mark without a note
            Draw(overlay, (100, 80), (300, 200), "door is blocked");
            Draw(overlay, (700, 80), (780, 200), longNote);
            Draw(overlay, (100, 250), (300, 350), note: null);
            AppShellUiHelpers.Flush();
            var badges = FindAll<Border>(window, "AnnotationMarkBadge");
            var lines = FindAll<Path>(window, "AnnotationMarkShape");
            var labels = FindAll<Border>(window, "AnnotationMarkNote");

            // Assert: three marks, but only the two noted marks have a label, each holding its note on one line
            Assert.Equal(3, badges.Count);
            Assert.Equal(3, lines.Count);
            Assert.Equal(2, labels.Count);
            var shortText = Assert.IsType<TextBlock>(labels[0].Child);
            var longText = Assert.IsType<TextBlock>(labels[1].Child);
            Assert.Equal("door is blocked", shortText.Text);
            Assert.False(Assert.Single(shortText.TextLayout.TextLines).HasCollapsed);
            Assert.Equal(longNote, longText.Text);

            // Assert: the long note is cut short with an ellipsis on one line, and both labels stay inside the picture
            Assert.Equal(TextTrimming.CharacterEllipsis, longText.TextTrimming);
            Assert.True(Assert.Single(longText.TextLayout.TextLines).HasCollapsed);
            Assert.All(labels, label =>
            {
                Assert.True(label.Bounds.Left >= 0, $"label {label.Bounds}");
                Assert.True(label.Bounds.Right <= picture.Width, $"label {label.Bounds}");
            });

            // Assert: the number, the line, and the label of a noted mark show the whole note; the plain mark has none
            Assert.Equal("Mark 1: door is blocked", ToolTip.GetTip(badges[0]));
            Assert.Equal("Mark 1: door is blocked", ToolTip.GetTip(lines[0]));
            Assert.Equal("Mark 1: door is blocked", ToolTip.GetTip(labels[0]));
            Assert.Equal($"Mark 2: {longNote}", ToolTip.GetTip(labels[1]));
            Assert.Null(ToolTip.GetTip(badges[2]));
            Assert.Null(ToolTip.GetTip(lines[2]));
            window.Close();
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    /// <summary>
    ///     Draws a rectangle through the overlay's view model and ends its note, as the pointer and the note card do.
    /// </summary>
    /// <param name="overlay">The overlay.</param>
    /// <param name="from">The start of the rectangle in picture pixels.</param>
    /// <param name="to">The end of the rectangle in picture pixels.</param>
    /// <param name="note">The note, or <see langword="null" /> to skip it.</param>
    private static void Draw(AnnotationOverlayViewModel overlay, (double X, double Y) from, (double X, double Y) to, string? note)
    {
        overlay.SelectTool(AnnotationTool.Rectangle);
        overlay.BeginStroke(new RenderPoint(from.X, from.Y));
        overlay.EndStroke(new RenderPoint(to.X, to.Y));
        if (note is null)
        {
            overlay.SkipNote();
        }
        else
        {
            overlay.NoteText = note;
            overlay.CommitNote();
        }

        overlay.SelectTool(AnnotationTool.None);
    }

    /// <summary>
    ///     Finds every control of a window with an automation identifier.
    /// </summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="window">The window.</param>
    /// <param name="automationId">The identifier.</param>
    /// <returns>The controls, in the order of the visual tree.</returns>
    private static List<T> FindAll<T>(Window window, string automationId)
        where T : Control =>
        [.. window.GetVisualDescendants().OfType<T>().Where(control => AutomationProperties.GetAutomationId(control) == automationId)];

    /// <summary>
    ///     Writes a file in a folder and draws it with the real render service.
    /// </summary>
    /// <param name="folder">The folder, which is the project.</param>
    /// <param name="name">The name of the file.</param>
    /// <param name="content">The content of the file.</param>
    /// <returns>The picture.</returns>
    private static async Task<RenderedImage> RenderAsync(string folder, string name, string content)
    {
        var token = TestContext.Current.CancellationToken;
        var path = System.IO.Path.Combine(folder, name);
        await File.WriteAllTextAsync(path, content, token);
        var result = await new ImageRenderService().RenderAsync(path, folder, token);
        Assert.True(result.Succeeded, result.Error?.Message);
        return result.Image!;
    }

    /// <summary>
    ///     A stand-in for the preview pane that tells which picture is shown.
    /// </summary>
    private sealed class ShownPictureSource : IRenderedImageSource
    {
        /// <summary>
        ///     The picture.
        /// </summary>
        private RenderedImage? _image;

        /// <inheritdoc />
        public event EventHandler? CurrentImageChanged;

        /// <inheritdoc />
        public RenderedImage? CurrentImage
        {
            get => _image;
            set
            {
                _image = value;
                CurrentImageChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <inheritdoc />
        public string? ProjectFolder { get; set; }
    }
}
