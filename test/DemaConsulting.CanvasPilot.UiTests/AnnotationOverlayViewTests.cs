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

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

using Path = Avalonia.Controls.Shapes.Path;
namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     A window with the real preview pane of the composed application, which hosts the layer for marks and its tool bar,
///     over a project whose pictures a test writes. It gives the tests the pointer and keys of a person and the mapping
///     between the points of the picture and the points of the window at the zoom of the pane.
/// </summary>
internal sealed class AnnotationHarness : IDisposable
{
    /// <summary>
    ///     An SVG of 800 x 400 pixels with a blue rectangle, larger than the pane at its own size, so the pane scales it.
    /// </summary>
    public const string BluePicture =
        "<svg xmlns='http://www.w3.org/2000/svg' width='800' height='400'>" +
        "<rect width='800' height='400' fill='#0000ff'/></svg>";

    /// <summary>
    ///     The same size of picture with other pixels, as after the file was edited.
    /// </summary>
    public const string GreenPicture =
        "<svg xmlns='http://www.w3.org/2000/svg' width='800' height='400'>" +
        "<rect width='800' height='400' fill='#00aa00'/></svg>";

    /// <summary>
    ///     The width of the pictures that the tests use, in pixels.
    /// </summary>
    public const double PictureWidth = 800;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AnnotationHarness" /> class.
    /// </summary>
    /// <param name="root">The temporary folder that holds everything of the test.</param>
    /// <param name="composition">The composed application.</param>
    /// <param name="projectFolder">The folder of the open project.</param>
    public AnnotationHarness(string root, ApplicationComposition composition, string projectFolder)
    {
        Root = root;
        Composition = composition;
        ProjectFolder = projectFolder;
        View = new PreviewPaneView { DataContext = composition.Preview };
        Window = new Window { Width = 640, Height = 520, Content = View };
        Window.Show();
        Flush();
    }

    /// <summary>
    ///     Gets the temporary folder that holds everything of the test.
    /// </summary>
    public string Root { get; }

    /// <summary>
    ///     Gets the composed application.
    /// </summary>
    public ApplicationComposition Composition { get; }

    /// <summary>
    ///     Gets the folder of the open project.
    /// </summary>
    public string ProjectFolder { get; }

    /// <summary>
    ///     Gets the window.
    /// </summary>
    public Window Window { get; }

    /// <summary>
    ///     Gets the view of the preview pane.
    /// </summary>
    public PreviewPaneView View { get; }

    /// <summary>
    ///     Gets the layer on which the user marks the picture.
    /// </summary>
    public AnnotationOverlayViewModel Overlay => Composition.Annotation;

    /// <summary>
    ///     Gets the view of the layer, which is as large as the picture in the picture's own pixels.
    /// </summary>
    public AnnotationOverlayView OverlayView => Find<AnnotationOverlayView>("AnnotationOverlay");

    /// <summary>
    ///     Creates the application, a project, and the window.
    /// </summary>
    /// <returns>The harness, which the test disposes.</returns>
    public static async Task<AnnotationHarness> CreateAsync()
    {
        var root = Directory.CreateTempSubdirectory("canvaspilot-ui-annotation-").FullName;
        var composition = AppShellUiHelpers.Compose(root);
        var projects = System.IO.Path.Combine(root, "projects");
        var created = await composition.Projects.CreateAsync(projects, "Demo", TestContext.Current.CancellationToken);
        return new AnnotationHarness(root, composition, created.Project!.FolderPath);
    }

    /// <summary>
    ///     Lets the dispatcher run what is queued.
    /// </summary>
    public static void Flush() => AppShellUiHelpers.Flush();

    /// <summary>
    ///     Writes a picture file in the project and shows it, once the render has finished.
    /// </summary>
    /// <param name="name">The name of the file.</param>
    /// <param name="svg">The content of the file.</param>
    /// <returns>A task that completes when the picture is shown.</returns>
    public async Task ShowAsync(string name, string svg)
    {
        var path = System.IO.Path.Combine(ProjectFolder, name);
        await File.WriteAllTextAsync(path, svg, TestContext.Current.CancellationToken);
        var explorer = Composition.Shell.Workspace.Explorer;
        explorer.Refresh();
        explorer.SelectFile(path);
        await Composition.Preview.RenderCompletion;
        Flush();
    }

    /// <summary>
    ///     Finds a control by its automation identifier.
    /// </summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="automationId">The identifier.</param>
    /// <returns>The control.</returns>
    public T Find<T>(string automationId)
        where T : Control => SettingsUiHelpers.Find<T>(Window, automationId);

    /// <summary>
    ///     Finds every control with an automation identifier.
    /// </summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="automationId">The identifier.</param>
    /// <returns>The controls, in the order of the visual tree.</returns>
    public IReadOnlyList<T> FindAll<T>(string automationId)
        where T : Control =>
        [.. Window.GetVisualDescendants().OfType<T>().Where(control => AutomationProperties.GetAutomationId(control) == automationId)];

    /// <summary>
    ///     Clicks the button of a tool.
    /// </summary>
    /// <param name="automationId">The identifier of the button.</param>
    public void ChooseTool(string automationId)
    {
        SettingsUiHelpers.Click(Window, Find<ToggleButton>(automationId));
        Flush();
    }

    /// <summary>
    ///     Gets where the picture is in the window, with the scale that the pane applied.
    /// </summary>
    /// <returns>The rectangle that the layer covers in the window's coordinates.</returns>
    public Rect Extent()
    {
        var view = OverlayView;
        var topLeft = view.TranslatePoint(new Point(0, 0), Window) ?? throw new InvalidOperationException("The layer is not in the window.");
        var bottomRight = view.TranslatePoint(new Point(view.Bounds.Width, view.Bounds.Height), Window) ?? throw new InvalidOperationException("The layer is not in the window.");
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>
    ///     Gets the point of the window that shows a point of the picture.
    /// </summary>
    /// <param name="x">The x coordinate in picture pixels.</param>
    /// <param name="y">The y coordinate in picture pixels.</param>
    /// <returns>The point in the window's coordinates.</returns>
    public Point At(double x, double y)
    {
        var extent = Extent();
        var scale = extent.Width / PictureWidth;
        return new Point(extent.X + (x * scale), extent.Y + (y * scale));
    }

    /// <summary>
    ///     Presses the left button at a point of the picture, moves to the other point through some points on the way, and
    ///     leaves the button down.
    /// </summary>
    /// <param name="from">The point of the picture to press at.</param>
    /// <param name="through">The points of the picture to move through, in order.</param>
    public void Press(Point from, params Point[] through)
    {
        Window.MouseDown(from, MouseButton.Left);
        foreach (var point in through)
        {
            Window.MouseMove(point, RawInputModifiers.LeftMouseButton);
        }

        Flush();
    }

    /// <summary>
    ///     Releases the left button at a point.
    /// </summary>
    /// <param name="at">The point in the window's coordinates.</param>
    public void Release(Point at)
    {
        Window.MouseUp(at, MouseButton.Left);
        Flush();
    }

    /// <summary>
    ///     Draws a shape with the pointer from one point of the picture to another through the middle, and releases.
    /// </summary>
    /// <param name="from">The start in picture pixels.</param>
    /// <param name="to">The end in picture pixels.</param>
    public void Drag((double X, double Y) from, (double X, double Y) to)
    {
        var start = At(from.X, from.Y);
        var end = At(to.X, to.Y);
        Press(start, At((from.X + to.X) / 2, (from.Y + to.Y) / 2), end);
        Release(end);
    }

    /// <summary>
    ///     Clicks the button that skips the note.
    /// </summary>
    public void SkipNote()
    {
        SettingsUiHelpers.Click(Window, Find<Button>("AnnotationNoteSkip"));
        Flush();
    }

    /// <summary>
    ///     Finds a control by its name in the XAML.
    /// </summary>
    /// <typeparam name="T">The control type.</typeparam>
    /// <param name="name">The name.</param>
    /// <returns>The control.</returns>
    public T FindNamed<T>(string name)
        where T : Control => Window.GetVisualDescendants().OfType<T>().First(control => control.Name == name);

    /// <summary>
    ///     Gets where a control is in the window.
    /// </summary>
    /// <param name="control">The control.</param>
    /// <returns>The rectangle the control covers in the window's coordinates.</returns>
    public Rect ExtentOf(Control control)
    {
        var topLeft = control.TranslatePoint(new Point(0, 0), Window) ?? throw new InvalidOperationException("The control is not in the window.");
        var bottomRight = control.TranslatePoint(new Point(control.Bounds.Width, control.Bounds.Height), Window) ?? throw new InvalidOperationException("The control is not in the window.");
        return new Rect(topLeft, bottomRight);
    }

    /// <summary>
    ///     Presses and releases a key.
    /// </summary>
    /// <param name="key">The physical key.</param>
    /// <param name="modifiers">The modifiers that are held.</param>
    public void Key(PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        Window.KeyPressQwerty(key, modifiers);
        Window.KeyReleaseQwerty(key, modifiers);
        Flush();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Window.Close();
        Composition.Dispose();
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A render that is still finishing can hold a file for a moment; the folder is in the temporary folder
        }
    }
}

/// <summary>
///     Headless UI tests for the layer on which the user marks the shown picture and for its tool bar, in the real preview
///     pane of the composed application: the pointer and the keys go through the window as the platform delivers them.
/// </summary>
public sealed class AnnotationOverlayViewTests
{
    /// <summary>
    ///     Checks that a point is where it should be, to a tenth of a pixel.
    /// </summary>
    /// <param name="x">The expected x coordinate.</param>
    /// <param name="y">The expected y coordinate.</param>
    /// <param name="actual">The point.</param>
    private static void AssertPoint(double x, double y, RenderPoint actual)
    {
        Assert.Equal(x, actual.X, 1);
        Assert.Equal(y, actual.Y, 1);
    }

    /// <summary>
    ///     Test that each tool shows its shape while the pointer drags, asks for the note when the pointer is released, and
    ///     then makes a numbered mark in the pixels of the picture, whatever the zoom of the pane is, adds the mark's chip to
    ///     the message, and draws the mark with its number.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_DragWithEachTool_DrawsDraftThenMakesMarkChipInPicturePixels()
    {
        // Arrange: a picture of 800 x 400 pixels that is scaled to fit the pane
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        var tools = new[]
        {
            ("AnnotationToolRectangle", MarkKind.Rectangle),
            ("AnnotationToolEllipse", MarkKind.Ellipse),
            ("AnnotationToolFreehand", MarkKind.Freehand),
            ("AnnotationToolArrow", MarkKind.Arrow),
        };
        var scale = harness.Extent().Width / AnnotationHarness.PictureWidth;

        for (var index = 0; index < tools.Length; index++)
        {
            var (button, kind) = tools[index];

            // Act: choose the tool, press, and move; look at the draft; release; look at the note; skip it
            harness.ChooseTool(button);
            harness.Press(harness.At(100, 80), harness.At(200, 60), harness.At(300, 200));
            var draft = harness.FindAll<Path>("AnnotationDraftShape");
            var drawing = harness.Overlay.IsDrawing;
            harness.Release(harness.At(300, 200));
            var noteShown = harness.Find<StackPanel>("AnnotationNotePanel").IsVisible;
            var draftWhileNote = harness.FindAll<Path>("AnnotationDraftShape").Count;
            harness.SkipNote();

            // Assert: the draft was drawn while the button was down and while the note waited, the note panel went away,
            // one more mark exists with the kind, number, and the pixels of the picture, and it has its chip and its drawing
            Assert.True(scale < 1);
            Assert.Single(draft);
            Assert.True(drawing);
            Assert.True(noteShown);
            Assert.Equal(1, draftWhileNote);
            Assert.False(harness.Find<StackPanel>("AnnotationNotePanel").IsVisible);
            Assert.Empty(harness.FindAll<Path>("AnnotationDraftShape"));
            Assert.Equal(index + 1, harness.Overlay.VisibleMarks.Count);
            var mark = harness.Overlay.VisibleMarks[^1];
            Assert.Equal(index + 1, mark.Number);
            Assert.Equal(kind, mark.Kind);
            AssertPoint(100, 80, mark.Geometry.Points[0]);
            AssertPoint(300, 200, mark.Geometry.Points[^1]);
            Assert.Equal(index + 1, harness.Composition.Composer.MarkChipIds.Count);
            Assert.Equal(mark.Id, harness.Composition.Composer.MarkChipIds[^1]);
            Assert.Equal(index + 1, harness.FindAll<Path>("AnnotationMarkShape").Count);
            var badge = harness.FindAll<Border>("AnnotationMarkBadge")[^1];
            Assert.Equal((index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), Assert.IsType<TextBlock>(badge.Child).Text);
        }

        Assert.Equal([MarkKind.Rectangle, MarkKind.Ellipse, MarkKind.Freehand, MarkKind.Arrow], harness.Overlay.VisibleMarks.Select(mark => mark.Kind));
    }

    /// <summary>
    ///     Test that the same drawing gives the same pixels when the picture is shown at its own size.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_DragRectangle_FitToWindowOff_CommitsSamePixelCoordinates()
    {
        // Arrange: the picture shown at its own size, with scroll bars
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.Composition.Preview.FitToWindow = false;
        AnnotationHarness.Flush();
        var scale = harness.Extent().Width / AnnotationHarness.PictureWidth;

        // Act: draw a rectangle
        harness.ChooseTool("AnnotationToolRectangle");
        harness.Drag((120, 60), (260, 150));
        harness.SkipNote();

        // Assert: the picture was not scaled and the mark holds the pixels that were pointed at
        Assert.Equal(1.0, scale, 2);
        var mark = Assert.Single(harness.Overlay.VisibleMarks);
        AssertPoint(120, 60, mark.Geometry.Points[0]);
        AssertPoint(260, 150, mark.Geometry.Points[1]);
    }

    /// <summary>
    ///     Test that the note panel takes the keyboard focus, limits the note to the length that a mark keeps, and that
    ///     Enter makes the mark with the note typed.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_NoteTyped_EnterMakesMarkWithNoteAndFocusIsInNote()
    {
        // Arrange: a finished rectangle that waits for its note
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.ChooseTool("AnnotationToolRectangle");
        harness.Drag((100, 80), (300, 200));
        var noteBox = harness.Find<TextBox>("AnnotationNoteBox");
        AnnotationHarness.Flush();

        // Act: type a note and press Enter
        var focused = noteBox.IsFocused;
        harness.Window.KeyTextInput("make it larger");
        AnnotationHarness.Flush();
        var typed = noteBox.Text;
        harness.Key(PhysicalKey.Enter);

        // Assert: the focus was in the note, the box limits the length, and the mark has the typed note
        Assert.True(focused);
        Assert.Equal("make it larger", typed);
        Assert.Equal(200, noteBox.MaxLength);
        var mark = Assert.Single(harness.Overlay.VisibleMarks);
        Assert.Equal("make it larger", mark.Note);
        Assert.False(harness.Find<StackPanel>("AnnotationNotePanel").IsVisible);
        Assert.Contains("note: make it larger", AutomationProperties.GetName(harness.FindAll<Path>("AnnotationMarkShape")[0]), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the buttons of the note make the mark with the typed note or without it.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_NoteButtons_AddMarkOrSkipNote()
    {
        // Arrange: two finished shapes in turn
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.ChooseTool("AnnotationToolEllipse");

        // Act: type a note and click Add mark, then type a note and click Skip note
        harness.Drag((100, 80), (200, 150));
        harness.Overlay.NoteText = "kept";
        SettingsUiHelpers.Click(harness.Window, harness.Find<Button>("AnnotationNoteAdd"));
        AnnotationHarness.Flush();
        harness.Drag((300, 80), (400, 150));
        harness.Overlay.NoteText = "dropped";
        harness.SkipNote();

        // Assert: the first mark has its note and the second none
        Assert.Equal(["kept", null], harness.Overlay.VisibleMarks.Select(mark => mark.Note));
    }

    /// <summary>
    ///     Test that the note card of a new mark is drawn with the design of the application: "Add mark" is the primary button
    ///     filled with the accent, "Skip note" is the secondary button with a quiet outline, and the card is rounded like the
    ///     other cards.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_NoteCard_UsesThePrimaryAndSecondaryButtonsAndTheCardRadius()
    {
        // Arrange: a finished shape that waits for its note, in the dark theme
        using var harness = await AnnotationHarness.CreateAsync();
        harness.Window.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark;
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.ChooseTool("AnnotationToolRectangle");
        harness.Drag((100, 80), (300, 200));
        AnnotationHarness.Flush();
        var add = harness.Find<Button>("AnnotationNoteAdd");
        var skip = harness.Find<Button>("AnnotationNoteSkip");
        var card = harness.Find<Border>("AnnotationNoteCard");

        // Act: read how the buttons and the card are drawn
        var addColor = Assert.IsAssignableFrom<ISolidColorBrush>(Face(add).Background).Color;
        var skipFace = Face(skip);
        var skipColor = Assert.IsAssignableFrom<ISolidColorBrush>(skipFace.Background).Color;
        var skipOutline = Assert.IsAssignableFrom<ISolidColorBrush>(skipFace.BorderBrush).Color;

        // Assert: the accent fill with the text on the accent, the raised fill with the strong outline, and the card radius
        Assert.Equal(ThemeUiHelpers.ColorOf("CpAccent", Avalonia.Styling.ThemeVariant.Dark), addColor);
        Assert.Equal(ThemeUiHelpers.ColorOf("CpTextOnAccent", Avalonia.Styling.ThemeVariant.Dark), Assert.IsAssignableFrom<ISolidColorBrush>(add.Foreground).Color);
        Assert.Equal(ThemeUiHelpers.ColorOf("CpSurfaceRaised", Avalonia.Styling.ThemeVariant.Dark), skipColor);
        Assert.Equal(ThemeUiHelpers.ColorOf("CpBorderStrong", Avalonia.Styling.ThemeVariant.Dark), skipOutline);
        Assert.Equal(new CornerRadius(14), card.CornerRadius);
        Assert.True(add.Bounds.Height >= 32 && skip.Bounds.Height >= 32);
    }

    /// <summary>
    ///     Finds the part of a button that draws its fill and outline.
    /// </summary>
    /// <param name="button">The button.</param>
    /// <returns>The content presenter of the button's template.</returns>
    private static ContentPresenter Face(Button button) =>
        button.GetVisualDescendants().OfType<ContentPresenter>().First(item => item.Name == "PART_ContentPresenter");

    /// <summary>
    ///     Test that Escape throws away a shape while the button is down and a finished shape that waits for its note, and
    ///     that no mark or chip results.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_Escape_DuringDragAndWhileNote_MakesNoMark()
    {
        // Arrange: a tool and a picture
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.ChooseTool("AnnotationToolRectangle");

        // Act: press, move, and press Escape with the button down; then release, draw again, and press Escape at the note
        harness.Press(harness.At(100, 80), harness.At(300, 200));
        var drawing = harness.Overlay.IsDrawing;
        harness.Key(PhysicalKey.Escape);
        var afterFirst = (harness.Overlay.HasDraft, harness.FindAll<Path>("AnnotationDraftShape").Count);
        harness.Release(harness.At(300, 200));
        harness.Drag((100, 80), (300, 200));
        var noteWaiting = harness.Overlay.IsNoteEditing;
        harness.Key(PhysicalKey.Escape);
        var afterSecond = (harness.Overlay.HasDraft, harness.Find<StackPanel>("AnnotationNotePanel").IsVisible);

        // Assert: both shapes were thrown away, nothing was made, and the tool is still chosen
        Assert.True(drawing);
        Assert.Equal((false, 0), afterFirst);
        Assert.True(noteWaiting);
        Assert.Equal((false, false), afterSecond);
        Assert.Empty(harness.Overlay.VisibleMarks);
        Assert.Empty(harness.Composition.Marks.Marks);
        Assert.Empty(harness.Composition.Composer.MarkChipIds);
        Assert.Equal(AnnotationTool.Rectangle, harness.Overlay.CurrentTool);
    }

    /// <summary>
    ///     Test that a click makes no mark and says that a click is no mark.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_ClickWithoutDrag_MakesNoMark()
    {
        // Arrange: a tool and a picture
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.ChooseTool("AnnotationToolEllipse");
        var point = harness.At(150, 100);

        // Act: press and release at the same point
        harness.Press(point);
        harness.Release(point);

        // Assert: no note, no mark, and the status line explains
        Assert.False(harness.Find<StackPanel>("AnnotationNotePanel").IsVisible);
        Assert.Empty(harness.Composition.Marks.Marks);
        Assert.Equal("Drag to draw a mark; a click makes none.", harness.Find<TextBlock>("AnnotationStatus").Text);
    }

    /// <summary>
    ///     Test that a stroke goes on while the pointer is outside the picture, with its points cut to the picture.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_PointerLeavesPicture_StrokeContinuesAndIsClampedToPicture()
    {
        // Arrange: a rectangle tool
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.ChooseTool("AnnotationToolRectangle");
        var extent = harness.Extent();

        // Act: press inside the picture, move to the right of it and below it, and release there
        var outside = new Point(extent.Right + 30, extent.Bottom + 20);
        harness.Press(harness.At(600, 300), outside);
        harness.Release(outside);
        harness.SkipNote();

        // Assert: the mark runs to the corner of the picture
        var mark = Assert.Single(harness.Overlay.VisibleMarks);
        AssertPoint(600, 300, mark.Geometry.Points[0]);
        AssertPoint(800, 400, mark.Geometry.Points[1]);
    }

    /// <summary>
    ///     Test that dragging the picture out is off while a tool is chosen, so the drag draws a mark, and that with the
    ///     pointer chosen the same drag starts the drag out of the picture.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_ToolChosen_PictureDragOutIsOffAndPointerDragsPictureOut()
    {
        // Arrange: a picture, and a drag operation that counts its starts instead of calling the operating system
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        var starts = 0;
        harness.View.DragStarter = (_, _, _) =>
        {
            starts++;
            return Task.FromResult(DragDropEffects.Copy);
        };
        var before = harness.Composition.Preview.IsDragOutEnabled;

        // Act: drag with the rectangle tool, then with the pointer chosen
        harness.ChooseTool("AnnotationToolRectangle");
        var whileTool = harness.Composition.Preview.IsDragOutEnabled;
        harness.Drag((100, 80), (300, 200));
        harness.SkipNote();
        var startsWithTool = starts;
        harness.ChooseTool("AnnotationToolSelect");
        var afterTool = harness.Composition.Preview.IsDragOutEnabled;
        harness.Drag((100, 80), (300, 200));
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (starts == 0 && DateTime.UtcNow < deadline)
        {
            AnnotationHarness.Flush();
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        // Assert: dragging out was on, off while the tool was chosen, and on again; the first drag made a mark and no drag
        // out, and the second drag started one drag out and made no mark
        Assert.True(before);
        Assert.False(whileTool);
        Assert.Equal(0, startsWithTool);
        Assert.True(afterTool);
        Assert.Equal(1, starts);
        Assert.Single(harness.Overlay.VisibleMarks);
        Assert.Equal(AnnotationTool.None, harness.Overlay.CurrentTool);
    }

    /// <summary>
    ///     Test that after the file is edited and drawn again the mark of the earlier picture is not drawn and the tool bar
    ///     says so, that the mark stays in the message, and that the picture with the same pixels shows it again.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_FileChanged_HidesMarkOfEarlierPictureAndSaysSo()
    {
        // Arrange: a mark on the blue picture
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("pic.svg", AnnotationHarness.BluePicture);
        harness.ChooseTool("AnnotationToolRectangle");
        harness.Drag((100, 80), (300, 200));
        harness.SkipNote();
        var drawnBefore = harness.FindAll<Path>("AnnotationMarkShape").Count;
        var hintBefore = harness.Find<TextBlock>("AnnotationHiddenMarks").IsVisible;

        // Act: the file now draws green, and then blue again; a second mark is drawn on the green picture
        await harness.ShowAsync("other.svg", AnnotationHarness.BluePicture);
        await harness.ShowAsync("pic.svg", AnnotationHarness.GreenPicture);
        var onGreen = (harness.FindAll<Path>("AnnotationMarkShape").Count, harness.Find<TextBlock>("AnnotationHiddenMarks").IsVisible, harness.Find<TextBlock>("AnnotationHiddenMarks").Text);
        harness.Drag((10, 10), (60, 60));
        harness.SkipNote();
        var secondOnGreen = harness.Overlay.VisibleMarks.Single();
        await harness.ShowAsync("other.svg", AnnotationHarness.BluePicture);
        await harness.ShowAsync("pic.svg", AnnotationHarness.BluePicture);
        var backOnBlue = (harness.FindAll<Path>("AnnotationMarkShape").Count, harness.Overlay.VisibleMarks.Single().Number);

        // Assert: the first mark was drawn and then hidden with a hint, the message kept both chips and the registry both
        // marks, the new mark has the next number, and the original pixels show the original mark again
        Assert.Equal(1, drawnBefore);
        Assert.False(hintBefore);
        Assert.Equal((0, true, "1 mark of the message was made on another picture and is hidden here."), onGreen);
        Assert.Equal(2, secondOnGreen.Number);
        Assert.Equal((1, 1), backOnBlue);
        Assert.Equal(2, harness.Composition.Composer.MarkChipIds.Count);
        Assert.Equal(2, harness.Composition.Marks.Marks.Length);
    }

    /// <summary>
    ///     Test that undoing the chip of a mark takes the mark off the picture and redoing puts it back, because the chip is
    ///     what makes the mark part of the message.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_ChipUndoAndRedo_HidesAndShowsTheMark()
    {
        // Arrange: a mark on the picture, after some typed text
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.Composition.Composer.InsertTyped("Look: ");
        harness.ChooseTool("AnnotationToolArrow");
        harness.Drag((100, 80), (300, 200));
        harness.SkipNote();
        var drawn = harness.FindAll<Path>("AnnotationMarkShape").Count;

        // Act: undo, then redo
        harness.Composition.Composer.Undo();
        AnnotationHarness.Flush();
        var afterUndo = (harness.FindAll<Path>("AnnotationMarkShape").Count, harness.FindAll<Border>("AnnotationMarkBadge").Count);
        harness.Composition.Composer.Redo();
        AnnotationHarness.Flush();
        var afterRedo = (harness.FindAll<Path>("AnnotationMarkShape").Count, harness.FindAll<Border>("AnnotationMarkBadge").Count);

        // Assert: the mark was drawn, gone after the undo, and back after the redo
        Assert.Equal(1, drawn);
        Assert.Equal((0, 0), afterUndo);
        Assert.Equal((1, 1), afterRedo);
    }

    /// <summary>
    ///     Test that the keyboard alone draws a mark: the arrow keys move a cursor, Enter starts and finishes the shape, and
    ///     Enter in the note makes the mark.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_Keyboard_ArrowKeysAndEnter_DrawRectangleMark()
    {
        // Arrange: the rectangle tool, with the keyboard focus on the layer
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.ChooseTool("AnnotationToolRectangle");
        harness.OverlayView.Focus();
        AnnotationHarness.Flush();

        // Act: move the cursor right twice from the middle, start, move right with Control and down, and finish
        harness.Key(PhysicalKey.ArrowRight);
        harness.Key(PhysicalKey.ArrowRight);
        var cursorDrawn = harness.FindAll<Path>("AnnotationKeyboardCursor").Count;
        harness.Key(PhysicalKey.Enter);
        var drawing = harness.Overlay.IsDrawing;
        harness.Key(PhysicalKey.ArrowRight, RawInputModifiers.Control);
        harness.Key(PhysicalKey.ArrowDown);
        harness.Key(PhysicalKey.Enter);
        var noteFocused = harness.Find<TextBox>("AnnotationNoteBox").IsFocused;
        harness.Key(PhysicalKey.Enter);

        // Assert: the cursor was shown, the shape started and ended at the cursor, the note took the focus, and the mark has the pixels
        Assert.Equal(1, cursorDrawn);
        Assert.True(drawing);
        Assert.True(noteFocused);
        var mark = Assert.Single(harness.Overlay.VisibleMarks);
        AssertPoint(416, 200, mark.Geometry.Points[0]);
        AssertPoint(456, 208, mark.Geometry.Points[1]);
        Assert.Single(harness.Composition.Composer.MarkChipIds);
    }

    /// <summary>
    ///     Test that Escape without a shape, with the layer focused, gives up the tool, and that the layer takes the keyboard
    ///     only while a tool is chosen.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_EscapeWithoutShape_GivesUpToolAndLayerTakesKeyboardOnlyWithTool()
    {
        // Arrange: a picture and no tool
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        var withoutTool = (harness.OverlayView.Focusable, harness.OverlayView.IsTabStop);

        // Act: choose a tool, focus the layer, and press Escape
        harness.ChooseTool("AnnotationToolFreehand");
        var withTool = (harness.OverlayView.Focusable, harness.OverlayView.IsTabStop);
        harness.OverlayView.Focus();
        harness.Key(PhysicalKey.Escape);

        // Assert: the layer was focusable only with a tool, and Escape chose the pointer
        Assert.Equal((false, false), withoutTool);
        Assert.Equal((true, true), withTool);
        Assert.Equal(AnnotationTool.None, harness.Overlay.CurrentTool);
        Assert.True(harness.Find<ToggleButton>("AnnotationToolSelect").IsChecked);
        Assert.True(harness.Composition.Preview.IsDragOutEnabled);
    }

    /// <summary>
    ///     Test that the tool bar has a named button for every tool, that exactly the button of the chosen tool is checked, that
    ///     a checked tool button unchecks to the pointer, and that the buttons are off while no picture is shown.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationToolbarView_ToolButtons_AreNamedCheckedWithTheToolAndOffWithoutPicture()
    {
        // Arrange: no picture is shown yet
        using var harness = await AnnotationHarness.CreateAsync();
        var ids = new[] { "AnnotationToolSelect", "AnnotationToolRectangle", "AnnotationToolEllipse", "AnnotationToolFreehand", "AnnotationToolArrow" };
        var buttons = ids.Select(id => harness.Find<ToggleButton>(id)).ToArray();
        var offWithoutPicture = buttons.Select(button => button.IsEffectivelyEnabled).ToArray();
        var statusWithoutPicture = harness.Find<TextBlock>("AnnotationStatus").Text;
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);

        // Act: choose the ellipse, then click it again
        harness.ChooseTool("AnnotationToolEllipse");
        var checkedWithEllipse = buttons.Select(button => button.IsChecked == true).ToArray();
        harness.ChooseTool("AnnotationToolEllipse");
        var checkedAfterSecondClick = buttons.Select(button => button.IsChecked == true).ToArray();

        // Assert: every button has a name; they were off without a picture, with the reason shown; exactly one is checked
        Assert.All(buttons, button => Assert.False(string.IsNullOrWhiteSpace(AutomationProperties.GetName(button))));
        Assert.Equal([false, false, false, false, false], offWithoutPicture);
        Assert.Equal("Show a picture to mark it.", statusWithoutPicture);
        Assert.All(buttons, button => Assert.True(button.IsEffectivelyEnabled));
        Assert.Equal([false, false, true, false, false], checkedWithEllipse);
        Assert.Equal([true, false, false, false, false], checkedAfterSecondClick);
    }

    /// <summary>
    ///     Test that the tools are off and no tool stays chosen while the message cannot be edited, for example when the
    ///     project is closed, and that they come back when it can.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationToolbarView_MessageCannotBeEdited_ToolsAreOffAndToolIsGivenUp()
    {
        // Arrange: a picture, with a tool chosen
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.ChooseTool("AnnotationToolRectangle");
        var rectangle = harness.Find<ToggleButton>("AnnotationToolRectangle");

        // Act: the composer loses its project, and then gets one again
        harness.Composition.Composer.SetProject(null);
        AnnotationHarness.Flush();
        var off = (rectangle.IsEffectivelyEnabled, harness.Overlay.CurrentTool, harness.Find<TextBlock>("AnnotationStatus").Text, harness.Composition.Preview.IsDragOutEnabled);
        harness.Composition.Composer.SetProject(harness.ProjectFolder);
        AnnotationHarness.Flush();

        // Assert: the tools were off with the reason and the pointer chosen, and are on again
        Assert.Equal((false, AnnotationTool.None, "Open a project to mark the picture.", true), off);
        Assert.True(rectangle.IsEffectivelyEnabled);
    }

    /// <summary>
    ///     Test that the layer is laid out in the pixels of the picture, so that its size is the picture's size, whatever the
    ///     zoom is, and that marks are drawn with the colors of the annotated picture.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_ShownMarks_AreLaidOutInPicturePixelsWithTheColorOfTheirNumber()
    {
        // Arrange: a picture that is scaled down, with two marks
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.ChooseTool("AnnotationToolRectangle");
        harness.Drag((100, 80), (300, 200));
        harness.SkipNote();
        harness.Drag((400, 80), (500, 200));
        harness.SkipNote();

        // Act: read the layer and its shapes
        var view = harness.OverlayView;
        var shapes = harness.FindAll<Path>("AnnotationMarkShape");
        var badges = harness.FindAll<Border>("AnnotationMarkBadge");

        // Assert: the layer is 800 x 400, the lines are in the colors of the numbers, and each badge sits at the top-left
        // corner of its mark
        Assert.Equal(new Size(800, 400), view.Bounds.Size);
        Assert.Equal("#ff1e88e5", Assert.IsType<Avalonia.Media.SolidColorBrush>(shapes[1].Stroke).Color.ToString().ToLowerInvariant());
        Assert.Equal("#ffe53935", Assert.IsType<Avalonia.Media.SolidColorBrush>(shapes[0].Stroke).Color.ToString().ToLowerInvariant());
        Assert.Equal(2, badges.Count);
        Assert.Equal(100 - badges[0].Width / 2, Canvas.GetLeft(badges[0]), 1);
        Assert.Equal(80 - badges[0].Height / 2, Canvas.GetTop(badges[0]), 1);
        Assert.Equal("Mark 2, rectangle", AutomationProperties.GetName(shapes[1]));
    }

    /// <summary>
    ///     Test that the floating tool bar shows icon buttons whose names and tooltips tell the tool and its key, that the keys
    ///     choose the tools, and that the bar and the note card stay inside a narrow stage.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationToolbarView_FloatingBar_HasTooltipsKeysAndStaysInsideNarrowStage()
    {
        // Arrange: a picture, on a stage that is 360 wide
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        harness.Window.Width = 360;
        AnnotationHarness.Flush();
        var rectangle = harness.Find<ToggleButton>("AnnotationToolRectangle");
        var tips = new[]
        {
            ("AnnotationToolSelect", "Select", "Select  V"),
            ("AnnotationToolRectangle", "Rectangle", "Rectangle  R"),
            ("AnnotationToolEllipse", "Ellipse", "Ellipse  E"),
            ("AnnotationToolFreehand", "Freehand", "Freehand  F"),
            ("AnnotationToolArrow", "Arrow", "Arrow  A"),
        };

        // Act: press the key of the rectangle with the focus on the stage, then draw a mark so that its note card shows
        harness.Find<ScrollViewer>("PreviewScroller").Focus();
        harness.Key(PhysicalKey.R);
        var chosenByKey = (harness.Overlay.CurrentTool, rectangle.IsChecked);
        harness.Drag((120, 60), (260, 150));
        var bar = harness.FindNamed<Border>("ToolBar");
        var card = harness.FindNamed<Border>("NoteCard");
        var width = harness.Window.ClientSize.Width;
        var barExtent = harness.ExtentOf(bar);
        var cardExtent = harness.ExtentOf(card);
        harness.SkipNote();

        // Assert: names and tooltips; the key chose the rectangle; the bar and the card lie inside the stage
        foreach (var (id, name, tip) in tips)
        {
            var button = harness.Find<ToggleButton>(id);
            Assert.Equal(name, AutomationProperties.GetName(button));
            Assert.Equal(tip, ToolTip.GetTip(button));
        }

        Assert.Equal((AnnotationTool.Rectangle, true), chosenByKey);
        Assert.True(barExtent.Left >= 0 && barExtent.Right <= width, $"bar {barExtent} in {width}");
        Assert.True(cardExtent.Left >= 0 && cardExtent.Right <= width, $"card {cardExtent} in {width}");
    }

    /// <summary>
    ///     Test that the clear button is off while the message holds no mark and that using it takes the chips of all marks
    ///     out of the message.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationToolbarView_ClearMarks_TakesTheMarkChipsOutOfTheMessage()
    {
        // Arrange: a picture with two marks in the message
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        var clear = harness.Find<Button>("AnnotationClearMarks");
        var offWithoutMarks = clear.IsEffectivelyEnabled;
        harness.ChooseTool("AnnotationToolRectangle");
        harness.Drag((100, 80), (300, 200));
        harness.SkipNote();
        harness.Drag((400, 80), (500, 200));
        harness.SkipNote();
        var chipsBefore = harness.Composition.Composer.MarkChipIds.Count;

        // Act: use the clear button
        SettingsUiHelpers.Click(harness.Window, clear);
        AnnotationHarness.Flush();

        // Assert
        Assert.False(offWithoutMarks);
        Assert.Equal(2, chipsBefore);
        Assert.Empty(harness.Composition.Composer.MarkChipIds);
        Assert.Empty(harness.Overlay.VisibleMarks);
        Assert.False(clear.IsEffectivelyEnabled);
    }

    /// <summary>
    ///     Draws a rectangle with the pointer, gives it a note with the Add mark button, and chooses the Select tool.
    /// </summary>
    /// <param name="harness">The harness, with a picture shown.</param>
    /// <param name="from">The start of the rectangle in picture pixels.</param>
    /// <param name="to">The end of the rectangle in picture pixels.</param>
    /// <param name="note">The note.</param>
    /// <returns>The mark that was made.</returns>
    private static Mark DrawNotedMark(AnnotationHarness harness, (double X, double Y) from, (double X, double Y) to, string note)
    {
        harness.ChooseTool("AnnotationToolRectangle");
        harness.Drag(from, to);
        harness.Overlay.NoteText = note;
        SettingsUiHelpers.Click(harness.Window, harness.Find<Button>("AnnotationNoteAdd"));
        AnnotationHarness.Flush();
        harness.ChooseTool("AnnotationToolSelect");
        return harness.Overlay.VisibleMarks[^1];
    }

    /// <summary>
    ///     Double-clicks with the left button at a point of the window, as two quick clicks at the same place, which the
    ///     platform reports as a press with a click count of two.
    /// </summary>
    /// <param name="harness">The harness.</param>
    /// <param name="at">The point in the window's coordinates.</param>
    private static void DoubleClick(AnnotationHarness harness, Point at)
    {
        harness.Window.MouseDown(at, MouseButton.Left);
        harness.Window.MouseUp(at, MouseButton.Left);
        harness.Window.MouseDown(at, MouseButton.Left);
        harness.Window.MouseUp(at, MouseButton.Left);
        AnnotationHarness.Flush();
    }

    /// <summary>
    ///     Gets the center of a control in the window's coordinates.
    /// </summary>
    /// <param name="harness">The harness.</param>
    /// <param name="control">The control.</param>
    /// <returns>The center.</returns>
    private static Point CenterOf(AnnotationHarness harness, Control control) => harness.ExtentOf(control).Center;

    /// <summary>
    ///     Test that a mark with a note shows the note on one line in a label beside its number badge, at the height of
    ///     the badge, inside the picture, and named for a screen reader.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_MarkWithNote_ShowsOneLineLabelBesideItsBadge()
    {
        // Arrange: a picture
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);

        // Act: draw a mark with a note
        DrawNotedMark(harness, (100, 80), (300, 200), "door is blocked");
        var badge = Assert.Single(harness.FindAll<Border>("AnnotationMarkBadge"));
        var label = Assert.Single(harness.FindAll<Border>("AnnotationMarkNote"));
        var text = Assert.IsType<TextBlock>(label.Child);

        // Assert: the label holds the note on one line, sits right of the badge at its height, and is inside the picture
        Assert.Equal("door is blocked", text.Text);
        Assert.Equal(1, text.MaxLines);
        Assert.Equal(TextWrapping.NoWrap, text.TextWrapping);
        Assert.Equal(TextTrimming.CharacterEllipsis, text.TextTrimming);
        Assert.Single(text.TextLayout.TextLines);
        Assert.True(Canvas.GetLeft(label) > Canvas.GetLeft(badge) + badge.Width);
        Assert.Equal(Canvas.GetTop(badge), Canvas.GetTop(label), 1);
        Assert.Equal(badge.Height, label.Height, 1);
        Assert.True(label.Bounds.Right <= AnnotationHarness.PictureWidth);
        Assert.Equal("Note of mark 1: door is blocked", AutomationProperties.GetName(label));
    }

    /// <summary>
    ///     Test that a long note near the right edge of the picture goes to the left of its badge, is cut short with an
    ///     ellipsis on one line inside the picture, and that a mark without a note has no label.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_LongNoteNearEdge_IsCutWithEllipsisInsidePictureAndNoteLessMarkHasNoLabel()
    {
        // Arrange: a picture and a long note
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        var note = string.Join(' ', Enumerable.Repeat("this part of the drawing needs another color", 4));

        // Act: draw a mark with the long note near the right edge, and a mark without a note
        DrawNotedMark(harness, (700, 80), (780, 200), note);
        harness.ChooseTool("AnnotationToolRectangle");
        harness.Drag((100, 250), (300, 350));
        harness.SkipNote();
        var label = Assert.Single(harness.FindAll<Border>("AnnotationMarkNote"));
        var text = Assert.IsType<TextBlock>(label.Child);
        var line = Assert.Single(text.TextLayout.TextLines);

        // Assert: one label, for the noted mark, left of its badge, cut short on one line, inside the picture
        Assert.Equal(2, harness.FindAll<Border>("AnnotationMarkBadge").Count);
        Assert.Equal(note, text.Text);
        Assert.True(line.HasCollapsed);
        Assert.True(label.Bounds.Left >= 0, $"label {label.Bounds}");
        Assert.True(label.Bounds.Right <= 700, $"label {label.Bounds}");
        Assert.True(label.Bounds.Width <= AnnotationOverlayStyle.NoteMaxWidth(800, 400) + 0.5);
    }

    /// <summary>
    ///     Test that the note label takes the overlay surface and the primary text color of the theme in use, in the dark
    ///     and the light theme, so that it is legible in both.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_NoteLabel_UsesThemeColorsInDarkAndLight()
    {
        // Arrange: a mark with a note
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        DrawNotedMark(harness, (100, 80), (300, 200), "legible");

        foreach (var variant in ThemeUiHelpers.Variants)
        {
            // Act: show the window in the variant and read the colors of the label
            harness.Window.RequestedThemeVariant = variant;
            AnnotationHarness.Flush();
            var label = Assert.Single(harness.FindAll<Border>("AnnotationMarkNote"));
            var background = Assert.IsAssignableFrom<ISolidColorBrush>(label.Background).Color;
            var foreground = Assert.IsAssignableFrom<ISolidColorBrush>(Assert.IsType<TextBlock>(label.Child).Foreground).Color;

            // Assert: the colors are the tokens of the variant
            Assert.Equal(ThemeUiHelpers.ColorOf("CpSurfaceOverlay", variant), background);
            Assert.Equal(ThemeUiHelpers.ColorOf("CpTextPrimary", variant), foreground);
        }
    }

    /// <summary>
    ///     Test that the label shows a changed note as soon as it changes, and goes away when the note is taken away.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_NoteChanged_LabelShowsTheNewNote()
    {
        // Arrange: a mark with a note
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        var mark = DrawNotedMark(harness, (100, 80), (300, 200), "old");

        // Act: change the note, then take it away
        harness.Composition.Marks.ChangeNote(mark.Id, "new");
        AnnotationHarness.Flush();
        var changed = Assert.IsType<TextBlock>(Assert.Single(harness.FindAll<Border>("AnnotationMarkNote")).Child).Text;
        harness.Composition.Marks.ChangeNote(mark.Id, null);
        AnnotationHarness.Flush();

        // Assert: the label showed the new note, and there is none without a note
        Assert.Equal("new", changed);
        Assert.Empty(harness.FindAll<Border>("AnnotationMarkNote"));
        Assert.Single(harness.FindAll<Border>("AnnotationMarkBadge"));
    }

    /// <summary>
    ///     Test that the badge, the line, and the note label of a mark with a note have the whole note as their tooltip,
    ///     that they answer the pointer, and that a mark without a note has no tooltip.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_MarkWithNote_BadgeLineAndLabelHaveTheWholeNoteAsTooltip()
    {
        // Arrange: a picture
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        var note = new string('w', 150);

        // Act: draw a mark with a long note, and one without
        DrawNotedMark(harness, (100, 80), (300, 200), note);
        harness.ChooseTool("AnnotationToolRectangle");
        harness.Drag((400, 250), (500, 350));
        harness.SkipNote();
        var badges = harness.FindAll<Border>("AnnotationMarkBadge");
        var lines = harness.FindAll<Path>("AnnotationMarkShape");
        var label = Assert.Single(harness.FindAll<Border>("AnnotationMarkNote"));

        // Assert: the three elements of the noted mark carry the whole note and answer the pointer; the other has no tip
        var expected = $"Mark 1: {note}";
        Assert.Equal(expected, ToolTip.GetTip(badges[0]));
        Assert.Equal(expected, ToolTip.GetTip(lines[0]));
        Assert.Equal(expected, ToolTip.GetTip(label));
        Assert.True(badges[0].IsHitTestVisible && lines[0].IsHitTestVisible && label.IsHitTestVisible);
        Assert.Null(ToolTip.GetTip(badges[1]));
        Assert.Null(ToolTip.GetTip(lines[1]));
    }

    /// <summary>
    ///     Test that a double-click on the badge of a mark with the Select tool opens the note card for that mark, filled
    ///     with its note and named for it, with the focus in the note, Cancel in place of Skip, and Save note.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_DoubleClickMarkWithSelect_OpensNoteCardFilledWithFocusInNote()
    {
        // Arrange: a mark with a note, and the Select tool
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        var mark = DrawNotedMark(harness, (100, 80), (300, 200), "door");
        var badge = Assert.Single(harness.FindAll<Border>("AnnotationMarkBadge"));

        // Act: double-click the badge
        DoubleClick(harness, CenterOf(harness, badge));
        AnnotationHarness.Flush();
        var noteBox = harness.Find<TextBox>("AnnotationNoteBox");

        // Assert: the card is open for the mark with its note, the focus is in the note, and the buttons are for a change
        Assert.Equal(mark.Id, harness.Overlay.EditingMarkId);
        Assert.True(harness.Find<StackPanel>("AnnotationNotePanel").IsVisible);
        Assert.Equal("door", noteBox.Text);
        Assert.True(noteBox.IsFocused);
        Assert.Equal(4, noteBox.CaretIndex);
        Assert.Equal("Note for mark 1", AutomationProperties.GetName(noteBox));
        Assert.False(harness.Find<Button>("AnnotationNoteSkip").IsVisible);
        Assert.True(harness.Find<Button>("AnnotationNoteCancel").IsVisible);
        Assert.Equal("Save note", harness.Find<Button>("AnnotationNoteAdd").Content);
        Assert.Empty(harness.FindAll<Path>("AnnotationDraftShape"));
        Assert.Equal(AnnotationTool.None, harness.Overlay.CurrentTool);
    }

    /// <summary>
    ///     Test that Enter in the opened note saves the changed note on the same mark, and that Escape closes the card
    ///     with the note unchanged and the mark and its chip kept; the note is opened from its label.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_EditNote_EnterSavesAndEscapeKeepsTheMark()
    {
        // Arrange: a mark with a note, and the Select tool
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        var mark = DrawNotedMark(harness, (100, 80), (300, 200), "door");

        // Act: double-click the note label, type, and press Enter
        DoubleClick(harness, CenterOf(harness, Assert.Single(harness.FindAll<Border>("AnnotationMarkNote"))));
        harness.Window.KeyTextInput(" is blocked");
        AnnotationHarness.Flush();
        harness.Key(PhysicalKey.Enter);
        var saved = (harness.Overlay.IsNoteEditing, Assert.Single(harness.Overlay.VisibleMarks).Note);
        var label = Assert.IsType<TextBlock>(Assert.Single(harness.FindAll<Border>("AnnotationMarkNote")).Child).Text;

        // Act: double-click the line of the mark, type, and press Escape
        DoubleClick(harness, harness.At(200, 200));
        var reopened = harness.Overlay.IsEditingMarkNote;
        harness.Window.KeyTextInput(" now");
        AnnotationHarness.Flush();
        harness.Key(PhysicalKey.Escape);

        // Assert: the note was saved on the same mark, then kept when escaped, with the mark and its chip still there
        Assert.Equal((false, "door is blocked"), saved);
        Assert.Equal("door is blocked", label);
        Assert.True(reopened);
        Assert.False(harness.Overlay.IsNoteEditing);
        var now = Assert.Single(harness.Overlay.VisibleMarks);
        Assert.Equal((mark.Id, mark.Number, "door is blocked"), (now.Id, now.Number, now.Note));
        Assert.Equal([mark.Id], harness.Composition.Composer.MarkChipIds);
        Assert.Single(harness.Composition.Marks.Marks);
    }

    /// <summary>
    ///     Test that the Save note and Cancel buttons save or keep the note, and that the keyboard focus goes back to
    ///     where it was before the note was opened.
    /// </summary>
    [AvaloniaFact]
    public async Task AnnotationOverlayView_EditNote_ButtonsSaveOrCancelAndFocusGoesBack()
    {
        // Arrange: a mark with a note, and the focus on the stage, which stands in for the message
        using var harness = await AnnotationHarness.CreateAsync();
        await harness.ShowAsync("blue.svg", AnnotationHarness.BluePicture);
        var mark = DrawNotedMark(harness, (100, 80), (300, 200), "old");
        var stage = harness.Find<ScrollViewer>("PreviewScroller");

        // Act: open the note as the host does, change it, and click Save note
        stage.Focus();
        harness.Overlay.BeginEditNote(mark.Id);
        AnnotationHarness.Flush();
        var focusInNote = harness.Find<TextBox>("AnnotationNoteBox").IsFocused;
        harness.Overlay.NoteText = "saved";
        SettingsUiHelpers.Click(harness.Window, harness.Find<Button>("AnnotationNoteAdd"));
        AnnotationHarness.Flush();
        var afterSave = (harness.Composition.Marks.Find(mark.Id)!.Note, stage.IsFocused);

        // Act: open it again, change it, and click Cancel
        stage.Focus();
        harness.Overlay.BeginEditNote(mark.Id);
        AnnotationHarness.Flush();
        harness.Overlay.NoteText = "dropped";
        SettingsUiHelpers.Click(harness.Window, harness.Find<Button>("AnnotationNoteCancel"));
        AnnotationHarness.Flush();

        // Assert: the focus moved into the note, the note was saved and then kept, and the focus came back each time
        Assert.True(focusInNote);
        Assert.Equal(("saved", true), afterSave);
        Assert.Equal("saved", harness.Composition.Marks.Find(mark.Id)!.Note);
        Assert.True(stage.IsFocused);
        Assert.False(harness.Overlay.IsNoteEditing);
        Assert.Single(harness.Composition.Composer.MarkChipIds);
    }
}
