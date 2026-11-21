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
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for <see cref="PayloadInspectorView" />: the window that shows exactly what the assistant would
///     receive for a sent message, as text and as the pictures of the payload. The payloads are made by hand with real
///     PNG pictures, so what the window shows can be compared with what the payload holds.
/// </summary>
public sealed class PayloadInspectorViewTests
{
    /// <summary>
    ///     Makes a payload with one marked picture whose bytes are real PNG files.
    /// </summary>
    /// <param name="marks">The numbers of the marks.</param>
    /// <returns>The payload.</returns>
    private static MessagePayload PayloadWithMarks(params int[] marks)
    {
        var png = new StubMarkImageComposer().Png;
        var payloadMarks = marks.Select(number => new PayloadMark(
            number,
            MarkKind.Ellipse,
            [new RenderPoint(10, 10), new RenderPoint(40, 30)],
            new RenderBox(10, 10, 30, 20),
            "this part",
            png,
            new RenderBox(0, 0, 64, 64),
            [],
            sourceElementsAvailable: false,
            sourceElementsUnavailableReason: "svg provides no element positions",
            sourceElementsTruncated: false));
        var image = new PayloadImage(@"C:\project\flow.svg", "file-hash", "pixel-hash", "svg", 200, 100, png, payloadMarks);
        return new MessagePayload(
            string.Join(' ', marks.Select(number => $"[Mark {number}]")),
            marks.Select(number => (PayloadSegment)new PayloadMarkPart(number)),
            [],
            [image],
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    ///     Finds a control in a window by its automation identifier.
    /// </summary>
    /// <typeparam name="T">The type of the control.</typeparam>
    /// <param name="window">The window.</param>
    /// <param name="automationId">The identifier.</param>
    /// <returns>The control.</returns>
    private static T Find<T>(Window window, string automationId)
        where T : Control => SettingsUiHelpers.Find<T>(window, automationId);

    /// <summary>
    ///     Test that the window shows the description of the payload as read-only text.
    /// </summary>
    [AvaloniaFact]
    public void PayloadInspectorView_Shown_ShowsTheDescriptionAsReadOnlyText()
    {
        // Arrange: an inspector for a payload with two marks
        var viewModel = new PayloadInspectorViewModel(PayloadWithMarks(1, 2));
        var window = new PayloadInspectorView(viewModel);

        // Act: show it
        window.Show();
        window.UpdateLayout();

        // Assert: the text is exactly the description, and cannot be edited
        var text = Find<TextBox>(window, "PayloadInspectorText");
        Assert.Equal(viewModel.Description, text.Text);
        Assert.True(text.IsReadOnly);
        Assert.Contains("[Mark 1] [Mark 2]", text.Text);
        Assert.Equal("Payload", window.Title);
        window.Close();
    }

    /// <summary>
    ///     Test that the window shows each picture of the payload, the annotated picture first and then the area of each
    ///     mark, each with its caption and decoded from the bytes of the payload.
    /// </summary>
    [AvaloniaFact]
    public void PayloadInspectorView_Shown_ShowsAnnotatedPictureAndEachMarkCropWithCaptions()
    {
        // Arrange: an inspector for a payload with two marks
        var viewModel = new PayloadInspectorViewModel(PayloadWithMarks(1, 2));
        var window = new PayloadInspectorView(viewModel);

        // Act: show it
        window.Show();
        window.UpdateLayout();

        // Assert: three captions in order and three decoded pictures
        var captions = window.GetVisualDescendants().OfType<TextBlock>()
            .Where(block => AutomationProperties.GetAutomationId(block) == "PayloadInspectorPictureCaption")
            .Select(block => block.Text).ToList();
        Assert.Equal(viewModel.Pictures.Select(picture => picture.Caption), captions);
        var images = window.GetVisualDescendants().OfType<Image>()
            .Where(image => AutomationProperties.GetAutomationId(image) == "PayloadInspectorPicture").ToList();
        Assert.Equal(3, images.Count);
        Assert.All(images, image => Assert.NotNull(image.Source));
        Assert.False(Find<TextBlock>(window, "PayloadInspectorNoPictures").IsVisible);
        window.Close();
    }

    /// <summary>
    ///     Test that a message without marks says that it has no pictures.
    /// </summary>
    [AvaloniaFact]
    public void PayloadInspectorView_Shown_MessageWithoutMarks_SaysItHasNoPictures()
    {
        // Arrange: an inspector for a text-only payload
        var payload = new MessagePayload("hello", [], [], [], DateTimeOffset.UtcNow);
        var window = new PayloadInspectorView(new PayloadInspectorViewModel(payload));

        // Act: show it
        window.Show();
        window.UpdateLayout();

        // Assert: the note is shown and no picture is
        Assert.True(Find<TextBlock>(window, "PayloadInspectorNoPictures").IsVisible);
        Assert.DoesNotContain(
            window.GetVisualDescendants().OfType<Image>(),
            image => AutomationProperties.GetAutomationId(image) == "PayloadInspectorPicture");
        window.Close();
    }

    /// <summary>
    ///     Test that the Close button closes the window and that closing releases the decoded pictures.
    /// </summary>
    [AvaloniaFact]
    public void PayloadInspectorView_CloseButton_ClosesWindowAndReleasesPictures()
    {
        // Arrange: a shown inspector whose first picture was decoded
        var viewModel = new PayloadInspectorViewModel(PayloadWithMarks(1));
        var window = new PayloadInspectorView(viewModel);
        window.Show();
        window.UpdateLayout();
        Assert.NotNull(viewModel.Pictures[0].Bitmap);

        // Act: press Close
        SettingsUiHelpers.Click(window, Find<Button>(window, "PayloadInspectorCloseButton"));

        // Assert: the window is closed and the pictures are released
        Assert.False(window.IsVisible);
        Assert.Null(viewModel.Pictures[0].Bitmap);
    }

    /// <summary>
    ///     Test that the main window shows the inspector as a dialog on its own.
    /// </summary>
    [AvaloniaFact]
    public async Task MainWindowView_ShowPayloadInspector_ShowsDialogOwnedByTheWindow()
    {
        // Arrange: a main window of a composed application
        var root = Directory.CreateTempSubdirectory("canvaspilot-ui-inspector-").FullName;
        try
        {
            using var composition = AppShellUiHelpers.Compose(root);
            var window = new MainWindowView { DataContext = composition.Shell };
            window.Show();
            var viewModel = new PayloadInspectorViewModel(PayloadWithMarks(1));

            // Act: ask for the inspector
            var shown = ((IPayloadInspectorLauncher)window).ShowPayloadInspectorAsync(viewModel);
            var dialog = AppShellUiHelpers.FindDialog<PayloadInspectorView>(window);

            // Assert: the dialog is owned by the window and shows the payload; closing it completes the task
            Assert.Equal(viewModel.Description, Find<TextBox>(dialog, "PayloadInspectorText").Text);
            dialog.Close();
            await shown;
            window.Close();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
