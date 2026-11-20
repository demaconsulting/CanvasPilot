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

using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.PayloadSubsystem;

/// <summary>
///     One thing that is attached to the message, as the reader finds it: where the user had it, and where it is in the
///     project once the message is sent.
/// </summary>
/// <param name="Index">The one-based index of the attachment in the message, as in <c>[Attachment 1: name]</c>.</param>
/// <param name="Kind">Whether the thing is a file, a folder, or a picture.</param>
/// <param name="Name">The name of the thing in the project.</param>
/// <param name="OriginalPath">The full path of the thing where the user had it, which is only shown.</param>
/// <param name="ProjectRelativePath">
///     The path of the thing in the project, relative to the project folder and written with forward slashes, for
///     example <c>attachments/brief.txt</c>.
/// </param>
/// <param name="SizeBytes">The size of a file in bytes, and the total size of the files of a folder that was copied.</param>
/// <param name="FileCount">One for a file, the number of files copied for a folder, and zero for a folder that is only referred to.</param>
public sealed record PayloadAttachment(
    int Index,
    AttachmentKind Kind,
    string Name,
    string OriginalPath,
    string ProjectRelativePath,
    long SizeBytes,
    int FileCount);
