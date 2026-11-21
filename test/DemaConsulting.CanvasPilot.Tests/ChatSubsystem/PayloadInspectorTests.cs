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

using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="PayloadInspectorViewModel" /> and <see cref="PayloadPictureViewModel" />: that the
///     inspector shows exactly the text that the payload describer writes for the payload and the picture bytes that the
///     payload holds, in order, with a caption for each, and that it can be closed and released.
/// </summary>
public sealed class PayloadInspectorTests
{
    /// <summary>
    ///     Test that the inspector refuses a missing payload.
    /// </summary>
    [Fact]
    public void PayloadInspectorViewModel_New_NullPayload_Throws()
    {
        // Arrange / Act / Assert: a payload is required
        Assert.Throws<ArgumentNullException>(() => new PayloadInspectorViewModel(null!));
        Assert.Throws<ArgumentNullException>(() => new PayloadPictureViewModel(null!, ChatTestPayloads.PngBytes));
    }

    /// <summary>
    ///     Test that the description is exactly what the payload describer writes, and so lists the text with the places of
    ///     the marks in order, the marked picture, and each mark.
    /// </summary>
    [Fact]
    public void PayloadInspectorViewModel_Description_IsExactlyWhatThePayloadDescriberWrites()
    {
        // Arrange: a payload with two marks on one picture
        var payload = ChatTestPayloads.WithMarks(1, 2);

        // Act: open the inspector on it
        using var inspector = new PayloadInspectorViewModel(payload);

        // Assert: the description is the describer's output, and shows the references in order with the mark data
        Assert.Equal(PayloadDescriber.Describe(payload), inspector.Description);
        Assert.Same(payload, inspector.Payload);
        Assert.Contains("[Mark 1] [Mark 2]", inspector.Description);
        Assert.Contains("\"note\": \"check this\"", inspector.Description);
        Assert.Contains("svg provides no element positions", inspector.Description);
        Assert.Equal("Payload", inspector.Title);
    }

    /// <summary>
    ///     Test that the pictures are the annotated picture followed by the cut-out of each mark, with the bytes of the
    ///     payload and a caption that says what each is.
    /// </summary>
    [Fact]
    public void PayloadInspectorViewModel_Pictures_AnnotatedPictureThenEachMarkCrop_WithCaptions()
    {
        // Arrange / Act: a payload with two marks on one picture
        using var inspector = new PayloadInspectorViewModel(ChatTestPayloads.WithMarks(1, 2));

        // Assert: three pictures in order with captions that name the file, the size, the marks, and the area
        Assert.True(inspector.HasPictures);
        Assert.Equal(3, inspector.Pictures.Count);
        Assert.Equal("Annotated picture of flow.svg (svg, 200 x 100 pixels), marks 1, 2", inspector.Pictures[0].Caption);
        Assert.Equal("Mark 1: area at 0, 0, 64 x 64 pixels", inspector.Pictures[1].Caption);
        Assert.Equal("Mark 2: area at 0, 0, 64 x 64 pixels", inspector.Pictures[2].Caption);
        Assert.All(inspector.Pictures, picture => Assert.Equal(ChatTestPayloads.PngBytes.Length, picture.ByteCount));
    }

    /// <summary>
    ///     Test that a message without marks has no pictures.
    /// </summary>
    [Fact]
    public void PayloadInspectorViewModel_Pictures_MessageWithoutMarks_HasNone()
    {
        // Arrange / Act: a text-only payload
        using var inspector = new PayloadInspectorViewModel(ChatTestPayloads.OfText("hello"));

        // Assert: no pictures, and the text is still described
        Assert.False(inspector.HasPictures);
        Assert.Empty(inspector.Pictures);
        Assert.Contains("hello", inspector.Description);
    }

    /// <summary>
    ///     Test that the close command asks the window to close.
    /// </summary>
    [Fact]
    public void PayloadInspectorViewModel_CloseCommand_RaisesCloseRequested()
    {
        // Arrange: an inspector that counts close requests
        using var inspector = new PayloadInspectorViewModel(ChatTestPayloads.OfText("hello"));
        var requests = 0;
        inspector.CloseRequested += (_, _) => requests++;

        // Act: press Close
        inspector.CloseCommand.Execute(null);

        // Assert: the window was asked to close once
        Assert.Equal(1, requests);
    }

    /// <summary>
    ///     Test that releasing the inspector twice is harmless and that bytes that are not a picture give no bitmap and no
    ///     exception, so that the caption still shows what they are.
    /// </summary>
    [Fact]
    public void PayloadPictureViewModel_Bitmap_BytesAreNotAPicture_IsNullAndDisposeTwiceIsHarmless()
    {
        // Arrange: a picture entry whose bytes are not a decodable picture
        var picture = new PayloadPictureViewModel("not a picture", ChatTestPayloads.PngBytes);

        // Act: dispose twice, and ask for the bitmap afterwards
        picture.Dispose();
        picture.Dispose();

        // Assert: there is no bitmap, and the caption is kept
        Assert.Null(picture.Bitmap);
        Assert.Equal("not a picture", picture.Caption);
    }
}
