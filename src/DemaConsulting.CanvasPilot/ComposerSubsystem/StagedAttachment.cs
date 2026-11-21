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

using System.Collections.Immutable;

namespace DemaConsulting.CanvasPilot.ComposerSubsystem;

/// <summary>
///     The kind of thing that is attached to a message.
/// </summary>
public enum AttachmentKind
{
    /// <summary>
    ///     A file.
    /// </summary>
    File,

    /// <summary>
    ///     A folder with everything in it.
    /// </summary>
    Folder,

    /// <summary>
    ///     A picture file.
    /// </summary>
    Image
}

/// <summary>
///     One thing that is attached to a message, as it is in the project once the message is sent.
/// </summary>
/// <param name="Chip">The chip of the draft that stands for the thing.</param>
/// <param name="OriginalPath">The full path of the thing where the user had it, which is only shown, never opened again.</param>
/// <param name="ProjectRelativePath">
///     The path of the thing in the project, relative to the project folder and written with forward slashes, for
///     example <c>attachments/photo.png</c>.
/// </param>
/// <param name="Kind">The kind of the thing.</param>
/// <param name="Name">The name of the thing in the project, which differs from the original name when that name was taken or unsafe.</param>
/// <param name="SizeBytes">
///     The size of a file in bytes, and the total size of the files of a folder that was copied. A folder that was
///     only referred to in place is not measured and has size zero.
/// </param>
/// <param name="FileCount">
///     One for a file, the number of files copied for a folder, and zero for a folder that was only referred to in place.
/// </param>
/// <param name="WasCopied">
///     <see langword="true" /> when the thing was copied or moved into the project's attachments folder, and
///     <see langword="false" /> when it was already in the project and is referred to in place.
/// </param>
/// <param name="SkippedLinks">The number of symbolic links and junctions in a copied folder that were not followed or copied.</param>
public sealed record StagedAttachment(
    AttachmentChip Chip,
    string OriginalPath,
    string ProjectRelativePath,
    AttachmentKind Kind,
    string Name,
    long SizeBytes,
    int FileCount,
    bool WasCopied,
    int SkippedLinks);

/// <summary>
///     Why the attachments of a message could not be put into the project.
/// </summary>
/// <param name="Chip">The chip of the thing that could not be attached.</param>
/// <param name="Reason">A sentence that names the thing and says what is wrong, which is shown to the user.</param>
public sealed record StagingFailure(AttachmentChip Chip, string Reason);

/// <summary>
///     The outcome of putting the attachments of a message into the project: all of them, or none, with the reason.
/// </summary>
public sealed class StagingResult
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="StagingResult" /> class.
    /// </summary>
    /// <param name="attachments">The attachments, in the order of their chips; empty on failure.</param>
    /// <param name="failure">The reason, or <see langword="null" /> on success.</param>
    private StagingResult(ImmutableArray<StagedAttachment> attachments, StagingFailure? failure)
    {
        Attachments = attachments;
        Failure = failure;
    }

    /// <summary>
    ///     Gets a value indicating whether every attachment is in the project.
    /// </summary>
    public bool Succeeded => Failure is null;

    /// <summary>
    ///     Gets the attachments, one for each chip, in the order of the chips; empty on failure.
    /// </summary>
    public ImmutableArray<StagedAttachment> Attachments { get; }

    /// <summary>
    ///     Gets the reason the message cannot be sent, or <see langword="null" /> on success.
    /// </summary>
    public StagingFailure? Failure { get; }

    /// <summary>
    ///     Makes the result of a success.
    /// </summary>
    /// <param name="attachments">The attachments, in the order of their chips.</param>
    /// <returns>The result.</returns>
    internal static StagingResult Success(IEnumerable<StagedAttachment> attachments) => new([.. attachments], null);

    /// <summary>
    ///     Makes the result of a failure.
    /// </summary>
    /// <param name="chip">The chip of the thing that could not be attached.</param>
    /// <param name="reason">The sentence that tells the user why.</param>
    /// <returns>The result.</returns>
    internal static StagingResult Fail(AttachmentChip chip, string reason) => new([], new StagingFailure(chip, reason));
}
