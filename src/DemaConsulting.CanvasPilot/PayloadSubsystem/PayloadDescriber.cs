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
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.PayloadSubsystem;

/// <summary>
///     Describes a <see cref="MessagePayload" /> as text for a person to read, so that the user can see exactly what the
///     assistant receives: the message with its references, the attachments, and for each picture its fingerprints, its
///     size, the marks with their geometry in picture pixels, their notes, and the parts of the source file under them.
/// </summary>
/// <remarks>
///     <para>
///         The description is JSON with a fixed order of properties, two-space indentation, line feeds as line breaks,
///         and numbers and dates written the same way on every computer, so the same payload always gives the same text
///         and a test or a person can compare two descriptions. The pictures are not part of the text: each is replaced
///         by its size in bytes and its SHA-256 fingerprint, so the text stays short and the pictures can be shown
///         beside it. Text is written with the characters that it holds, except that quotation marks, backslashes, and
///         control characters are escaped as JSON requires; the text is shown to a person and is never put in a web page
///         or script. The describer holds no state and is safe for concurrent use.
///     </para>
/// </remarks>
public static class PayloadDescriber
{
    /// <summary>
    ///     The options of the JSON writer: indented, with line feeds, and with the characters of the payload not escaped
    ///     more than JSON requires, so that the text is readable.
    /// </summary>
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Indented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    ///     Describes a payload.
    /// </summary>
    /// <param name="payload">The payload.</param>
    /// <returns>The description as JSON text.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="payload" /> is <see langword="null" />.</exception>
    public static string Describe(MessagePayload payload)
    {
        ArgumentNullException.ThrowIfNull(payload);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteNumber("version", payload.Version);
            writer.WriteString("createdUtc", payload.CreatedUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("text", payload.Text);
            WriteSegments(writer, payload);
            WriteAttachments(writer, payload);
            WriteImages(writer, payload);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    ///     Writes the segments of the message.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="payload">The payload.</param>
    private static void WriteSegments(Utf8JsonWriter writer, MessagePayload payload)
    {
        writer.WriteStartArray("segments");
        foreach (var segment in payload.Segments)
        {
            writer.WriteStartObject();
            switch (segment)
            {
                case PayloadTextPart text:
                    writer.WriteString("type", "text");
                    writer.WriteString("origin", text.Origin.ToString().ToLowerInvariant());
                    writer.WriteString("text", text.Text);
                    break;

                case PayloadAttachmentPart attachment:
                    writer.WriteString("type", "attachment");
                    writer.WriteNumber("index", attachment.Index);
                    break;

                case PayloadMarkPart mark:
                    writer.WriteString("type", "mark");
                    writer.WriteNumber("number", mark.Number);
                    break;
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    ///     Writes the attachments.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="payload">The payload.</param>
    private static void WriteAttachments(Utf8JsonWriter writer, MessagePayload payload)
    {
        writer.WriteStartArray("attachments");
        foreach (var attachment in payload.Attachments)
        {
            writer.WriteStartObject();
            writer.WriteNumber("index", attachment.Index);
            writer.WriteString("kind", attachment.Kind.ToString().ToLowerInvariant());
            writer.WriteString("name", attachment.Name);
            writer.WriteString("originalPath", attachment.OriginalPath);
            writer.WriteString("projectRelativePath", attachment.ProjectRelativePath);
            writer.WriteNumber("sizeBytes", attachment.SizeBytes);
            writer.WriteNumber("fileCount", attachment.FileCount);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    ///     Writes the pictures with their marks.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="payload">The payload.</param>
    private static void WriteImages(Utf8JsonWriter writer, MessagePayload payload)
    {
        writer.WriteStartArray("images");
        foreach (var image in payload.Images)
        {
            writer.WriteStartObject();
            writer.WriteString("sourcePath", image.SourcePath);
            WriteOptionalString(writer, "sourceFileHash", image.SourceFileHash);
            writer.WriteString("contentHash", image.ContentHash);
            writer.WriteString("format", image.Format);
            writer.WriteNumber("width", image.Width);
            writer.WriteNumber("height", image.Height);
            WriteFile(writer, "annotatedPng", image.AnnotatedPng.Span);
            writer.WriteStartArray("marks");
            foreach (var mark in image.Marks)
            {
                WriteMark(writer, mark);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>
    ///     Writes one mark.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="mark">The mark.</param>
    private static void WriteMark(Utf8JsonWriter writer, PayloadMark mark)
    {
        writer.WriteStartObject();
        writer.WriteNumber("number", mark.Number);
        writer.WriteString("kind", mark.Kind.ToString().ToLowerInvariant());
        writer.WriteStartArray("points");
        foreach (var point in mark.Points)
        {
            WritePoint(writer, point);
        }

        writer.WriteEndArray();
        WriteBox(writer, "boundingBox", mark.BoundingBox);
        WriteOptionalString(writer, "note", mark.Note);

        // The area around the mark: where it is and the picture, as a size and a fingerprint
        writer.WriteStartObject("crop");
        WriteBox(writer, "box", mark.CropBox);
        WriteFile(writer, "png", mark.CropPng.Span);
        writer.WriteEndObject();

        // The parts of the source file under the mark, or why the picture cannot say
        writer.WriteStartObject("sourceElements");
        writer.WriteBoolean("available", mark.SourceElementsAvailable);
        WriteOptionalString(writer, "unavailableReason", mark.SourceElementsUnavailableReason);
        writer.WriteBoolean("truncated", mark.SourceElementsTruncated);
        writer.WriteStartArray("elements");
        foreach (var element in mark.SourceElements)
        {
            WriteElement(writer, element);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>
    ///     Writes one part of the source file.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="element">The part.</param>
    private static void WriteElement(Utf8JsonWriter writer, SourceElementReference element)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", element.Kind);
        writer.WriteString("id", element.Id);
        WriteOptionalString(writer, "text", element.Text);
        if (element.Line is { } line)
        {
            writer.WriteNumber("line", line);
        }
        else
        {
            writer.WriteNull("line");
        }

        if (element.Box is { } box)
        {
            WriteBox(writer, "box", box);
        }
        else
        {
            writer.WriteNull("box");
        }

        if (element.Pixel is { } pixel)
        {
            writer.WritePropertyName("pixel");
            WritePoint(writer, pixel);
        }
        else
        {
            writer.WriteNull("pixel");
        }

        writer.WriteEndObject();
    }

    /// <summary>
    ///     Writes a text property that may be missing.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="name">The name of the property.</param>
    /// <param name="value">The text, or <see langword="null" />.</param>
    private static void WriteOptionalString(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is null)
        {
            writer.WriteNull(name);
        }
        else
        {
            writer.WriteString(name, value);
        }
    }

    /// <summary>
    ///     Writes a point as an object.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="point">The point.</param>
    private static void WritePoint(Utf8JsonWriter writer, RenderPoint point)
    {
        writer.WriteStartObject();
        writer.WriteNumber("x", point.X);
        writer.WriteNumber("y", point.Y);
        writer.WriteEndObject();
    }

    /// <summary>
    ///     Writes a box as a property whose value is an object.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="name">The name of the property.</param>
    /// <param name="box">The box.</param>
    private static void WriteBox(Utf8JsonWriter writer, string name, RenderBox box)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("x", box.X);
        writer.WriteNumber("y", box.Y);
        writer.WriteNumber("width", box.Width);
        writer.WriteNumber("height", box.Height);
        writer.WriteEndObject();
    }

    /// <summary>
    ///     Writes a file that is not shown as text: its size in bytes and its SHA-256 fingerprint.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="name">The name of the property.</param>
    /// <param name="content">The bytes of the file.</param>
    private static void WriteFile(Utf8JsonWriter writer, string name, ReadOnlySpan<byte> content)
    {
        writer.WriteStartObject(name);
        writer.WriteNumber("bytes", content.Length);
        writer.WriteString("sha256", Convert.ToHexStringLower(SHA256.HashData(content)));
        writer.WriteEndObject();
    }
}
