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
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.PayloadSubsystem;

namespace DemaConsulting.CanvasPilot.ChatSubsystem;

/// <summary>
///     The view model of the payload inspector: what the assistant would receive for a sent message, shown to the user
///     exactly, as the text that <see cref="PayloadDescriber" /> writes for it and as the pictures that the payload holds.
/// </summary>
/// <remarks>
///     <para>
///         <b>Exactly what would be sent.</b> The description is not written again here: it is the output of
///         <see cref="PayloadDescriber.Describe" />, which lists the text with the places of the marks and attachments in
///         the order the user placed them, the attachments with their paths, and for every marked picture its identity,
///         its marks with geometry, note, area, and the parts of the source file under the mark. The pictures are the
///         PNG bytes of the payload itself: the annotated picture of every marked file and the cut-out around every mark,
///         in the order of the payload, each with a caption that says what it is.
///     </para>
///     <para>
///         The payload is immutable, so the inspector can keep it as long as it is open and nothing in it can change
///         while the user reads it. The view model is owned by the user interface thread and is disposed when its window
///         closes, which releases the decoded pictures.
///     </para>
/// </remarks>
public sealed class PayloadInspectorViewModel : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="PayloadInspectorViewModel" /> class.
    /// </summary>
    /// <param name="payload">The payload to show.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="payload" /> is <see langword="null" />.</exception>
    public PayloadInspectorViewModel(MessagePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        Payload = payload;
        Description = PayloadDescriber.Describe(payload);
        Pictures = BuildPictures(payload);
        CloseCommand = new RelayCommand(() => CloseRequested?.Invoke(this, EventArgs.Empty));
    }

    /// <summary>
    ///     Raised when the user asks to close the inspector, so that the window can close.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    ///     Gets the text of the window title.
    /// </summary>
    public string Title { get; } = "Payload";

    /// <summary>
    ///     Gets the payload that is shown.
    /// </summary>
    public MessagePayload Payload { get; }

    /// <summary>
    ///     Gets what the assistant would receive, as the text that <see cref="PayloadDescriber" /> writes.
    /// </summary>
    public string Description { get; }

    /// <summary>
    ///     Gets the pictures of the payload in order: for each marked picture, its annotated picture followed by the
    ///     cut-out of each of its marks. Empty for a message without marks.
    /// </summary>
    public IReadOnlyList<PayloadPictureViewModel> Pictures { get; }

    /// <summary>
    ///     Gets a value indicating whether the payload holds any picture.
    /// </summary>
    public bool HasPictures => Pictures.Count > 0;

    /// <summary>
    ///     Gets the command that closes the inspector.
    /// </summary>
    public IRelayCommand CloseCommand { get; }

    /// <summary>
    ///     Releases the decoded pictures.
    /// </summary>
    public void Dispose()
    {
        foreach (var picture in Pictures)
        {
            picture.Dispose();
        }
    }

    /// <summary>
    ///     Makes the picture entries of a payload, so that the view can show each with its caption.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <returns>The pictures, annotated picture first for each marked file.</returns>
    private static List<PayloadPictureViewModel> BuildPictures(MessagePayload payload)
    {
        var pictures = new List<PayloadPictureViewModel>();
        foreach (var image in payload.Images)
        {
            // The whole picture with every mark drawn and numbered on it, as the assistant receives it
            var numbers = string.Join(", ", image.Marks.Select(mark => mark.Number.ToString(CultureInfo.InvariantCulture)));
            pictures.Add(new PayloadPictureViewModel(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Annotated picture of {Path.GetFileName(image.SourcePath)} ({image.Format}, {image.Width} x {image.Height} pixels), marks {numbers}"),
                image.AnnotatedPng));

            // The area around each mark, cut from the annotated picture
            foreach (var mark in image.Marks)
            {
                var box = mark.CropBox;
                pictures.Add(new PayloadPictureViewModel(
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"Mark {mark.Number}: area at {box.X:0.##}, {box.Y:0.##}, {box.Width:0.##} x {box.Height:0.##} pixels"),
                    mark.CropPng));
            }
        }

        return pictures;
    }
}
