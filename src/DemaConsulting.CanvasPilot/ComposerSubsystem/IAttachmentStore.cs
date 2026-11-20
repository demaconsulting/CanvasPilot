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

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     What the composer needs from the place where attachments are kept: saving pictures that were pasted, removing
///     the ones nobody refers to any more, and putting the attachments of a message into the project when it is sent.
///     The interface exists so that the intake of dropped content can be verified without a file system.
/// </summary>
public interface IAttachmentStore
{
    /// <summary>
    ///     Gets the full path of the project folder that the store works in.
    /// </summary>
    string ProjectFolder { get; }

    /// <summary>
    ///     Gets the full path of the folder in which pasted pictures are saved while the message is a draft.
    /// </summary>
    string PastedImagesFolder { get; }

    /// <summary>
    ///     Saves a picture that was pasted, for example a screenshot, as a PNG file in the draft folder of the project.
    /// </summary>
    /// <param name="png">The bytes of the PNG picture.</param>
    /// <returns>The full path of the saved file.</returns>
    /// <exception cref="ArgumentException">Thrown when the data is empty, too large, or not a PNG picture.</exception>
    /// <exception cref="IOException">Thrown when the file cannot be written.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when the folder cannot be written.</exception>
    string SavePastedImage(ReadOnlyMemory<byte> png);

    /// <summary>
    ///     Deletes the saved pasted pictures that no chip refers to any more.
    /// </summary>
    /// <param name="keep">The full paths of the pictures that chips of the draft still refer to; they are not deleted.</param>
    /// <returns>The number of files that were deleted.</returns>
    int RemoveLeftovers(IReadOnlyCollection<string> keep);

    /// <summary>
    ///     Puts the things that chips stand for into the project, so that the message can refer to them there. Either
    ///     all of them are put there, or none: when one cannot be, nothing that this call created is left behind.
    /// </summary>
    /// <param name="chips">The chips of the message, in order.</param>
    /// <returns>The attachments in the project, or the reason that the message cannot be sent.</returns>
    StagingResult StageForSend(IReadOnlyList<AttachmentChip> chips);
}
