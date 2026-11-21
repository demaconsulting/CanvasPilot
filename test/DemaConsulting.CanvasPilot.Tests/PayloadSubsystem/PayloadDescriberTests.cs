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

using System.Security.Cryptography;
using System.Text.Json;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="PayloadDescriber" />.
/// </summary>
public sealed class PayloadDescriberTests
{
    /// <summary>
    ///     The bytes of the annotated picture of the payload of the tests.
    /// </summary>
    private static readonly byte[] AnnotatedBytes = [9, 8, 7, 6];

    /// <summary>
    ///     The bytes of the area around the mark of the payload of the tests.
    /// </summary>
    private static readonly byte[] CropBytes = [1, 2, 3];

    /// <summary>
    ///     Makes a payload of one picture with one mark, two parts of the source under it, a note with a quotation mark, a
    ///     file attached, and a text with a line break and a quotation mark.
    /// </summary>
    /// <returns>The payload.</returns>
    private static MessagePayload MakePayload()
    {
        var mark = new PayloadMark(
            1,
            MarkKind.Rectangle,
            [new RenderPoint(10, 20), new RenderPoint(60.5, 50)],
            new RenderBox(10, 20, 50.5, 30),
            "say \"hi\"",
            CropBytes,
            new RenderBox(0, 4, 81, 64),
            [
                new SourceElementReference("node", "A", "Start", 2, new RenderBox(20, 20, 100, 40), null),
                new SourceElementReference("anchor", "top", null, null, null, new RenderPoint(100, 50))
            ],
            true,
            null,
            false);
        var image = new PayloadImage(@"C:\project\flow.mmd", "abc123", "def456", "mermaid", 400, 200, AnnotatedBytes, [mark]);
        return new MessagePayload(
            "Look [Mark 1] [Attachment 1: brief.txt]\n\"quoted\"",
            [
                new PayloadTextPart("Look ", TextOrigin.Typed),
                new PayloadMarkPart(1),
                new PayloadTextPart(" ", TextOrigin.Dictated),
                new PayloadAttachmentPart(1)
            ],
            [new PayloadAttachment(1, AttachmentKind.File, "brief.txt", @"C:\notes\brief.txt", "attachments/brief.txt", 120, 1)],
            [image],
            new DateTimeOffset(2026, 11, 20, 15, 0, 0, TimeSpan.Zero));
    }

    /// <summary>
    ///     Test that a payload is described as JSON with a fixed order of properties, indentation, and number format, with
    ///     the pictures shown as a size and a fingerprint.
    /// </summary>
    [Fact]
    public void PayloadDescriber_Describe_Payload_GivesExactlyThisText()
    {
        // Arrange: a payload with everything in it, and the fingerprints of its two pictures
        var payload = MakePayload();
        var annotatedHash = Convert.ToHexStringLower(SHA256.HashData(AnnotatedBytes));
        var cropHash = Convert.ToHexStringLower(SHA256.HashData(CropBytes));
        var expected = $$"""
            {
              "version": 1,
              "createdUtc": "2026-11-20T15:00:00.0000000+00:00",
              "text": "Look [Mark 1] [Attachment 1: brief.txt]\n\"quoted\"",
              "segments": [
                {
                  "type": "text",
                  "origin": "typed",
                  "text": "Look "
                },
                {
                  "type": "mark",
                  "number": 1
                },
                {
                  "type": "text",
                  "origin": "dictated",
                  "text": " "
                },
                {
                  "type": "attachment",
                  "index": 1
                }
              ],
              "attachments": [
                {
                  "index": 1,
                  "kind": "file",
                  "name": "brief.txt",
                  "originalPath": "C:\\notes\\brief.txt",
                  "projectRelativePath": "attachments/brief.txt",
                  "sizeBytes": 120,
                  "fileCount": 1
                }
              ],
              "images": [
                {
                  "sourcePath": "C:\\project\\flow.mmd",
                  "sourceFileHash": "abc123",
                  "contentHash": "def456",
                  "format": "mermaid",
                  "width": 400,
                  "height": 200,
                  "annotatedPng": {
                    "bytes": 4,
                    "sha256": "{{annotatedHash}}"
                  },
                  "marks": [
                    {
                      "number": 1,
                      "kind": "rectangle",
                      "points": [
                        {
                          "x": 10,
                          "y": 20
                        },
                        {
                          "x": 60.5,
                          "y": 50
                        }
                      ],
                      "boundingBox": {
                        "x": 10,
                        "y": 20,
                        "width": 50.5,
                        "height": 30
                      },
                      "note": "say \"hi\"",
                      "crop": {
                        "box": {
                          "x": 0,
                          "y": 4,
                          "width": 81,
                          "height": 64
                        },
                        "png": {
                          "bytes": 3,
                          "sha256": "{{cropHash}}"
                        }
                      },
                      "sourceElements": {
                        "available": true,
                        "unavailableReason": null,
                        "truncated": false,
                        "elements": [
                          {
                            "kind": "node",
                            "id": "A",
                            "text": "Start",
                            "line": 2,
                            "box": {
                              "x": 20,
                              "y": 20,
                              "width": 100,
                              "height": 40
                            },
                            "pixel": null
                          },
                          {
                            "kind": "anchor",
                            "id": "top",
                            "text": null,
                            "line": null,
                            "box": null,
                            "pixel": {
                              "x": 100,
                              "y": 50
                            }
                          }
                        ]
                      }
                    }
                  ]
                }
              ]
            }
            """.ReplaceLineEndings("\n");

        // Act: describe the payload
        var text = PayloadDescriber.Describe(payload);

        // Assert: exactly the expected text, with line feeds only
        Assert.Equal(expected, text);
        Assert.DoesNotContain('\r', text);
    }

    /// <summary>
    ///     Test that the same payload, and an equal one built again, always give the same text.
    /// </summary>
    [Fact]
    public void PayloadDescriber_Describe_EqualPayloads_GiveTheSameText()
    {
        // Arrange / Act: describe one payload twice and an equal payload once
        var payload = MakePayload();
        var first = PayloadDescriber.Describe(payload);
        var second = PayloadDescriber.Describe(payload);
        var other = PayloadDescriber.Describe(MakePayload());

        // Assert: all three descriptions are the same
        Assert.Equal(first, second);
        Assert.Equal(first, other);
    }

    /// <summary>
    ///     Test that the description does not hold the pictures themselves, only their size in bytes and fingerprint.
    /// </summary>
    [Fact]
    public void PayloadDescriber_Describe_Images_AreShownAsByteCountAndFingerprint()
    {
        // Arrange: a payload whose pictures are recognizable bytes
        var payload = MakePayload();

        // Act: describe it and read the description as JSON
        using var document = JsonDocument.Parse(PayloadDescriber.Describe(payload));
        var image = document.RootElement.GetProperty("images")[0];
        var annotated = image.GetProperty("annotatedPng");
        var crop = image.GetProperty("marks")[0].GetProperty("crop").GetProperty("png");

        // Assert: byte counts and SHA-256 fingerprints, and no bytes
        Assert.Equal(4, annotated.GetProperty("bytes").GetInt32());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(AnnotatedBytes)), annotated.GetProperty("sha256").GetString());
        Assert.Equal(3, crop.GetProperty("bytes").GetInt32());
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(CropBytes)), crop.GetProperty("sha256").GetString());
        Assert.Equal(2, annotated.EnumerateObject().Count());
    }

    /// <summary>
    ///     Test that text with quotation marks, backslashes, line breaks, and characters outside ASCII is described so that
    ///     reading the description as JSON gives back exactly the text, and that characters need no escape when JSON does
    ///     not require it.
    /// </summary>
    [Fact]
    public void PayloadDescriber_Describe_SpecialCharacters_AreEscapedAsJsonRequiresAndReadBackExactly()
    {
        // Arrange: a payload whose text and attachment name hold special characters
        var text = "Zoom \"in\" on C:\\temp\r\n\tCafé ✓ <b>&</b>";
        var payload = new MessagePayload(
            text,
            [new PayloadTextPart(text, TextOrigin.Typed)],
            [new PayloadAttachment(1, AttachmentKind.Folder, "Photos é", @"C:\Pictures\Photos é", "attachments/Photos é", 0, 0)],
            [],
            DateTimeOffset.UnixEpoch);

        // Act: describe it and read it back
        var description = PayloadDescriber.Describe(payload);
        using var document = JsonDocument.Parse(description);

        // Assert: the values read back are the originals; the characters are readable and not written as escapes
        Assert.Equal(text, document.RootElement.GetProperty("text").GetString());
        Assert.Equal(text, document.RootElement.GetProperty("segments")[0].GetProperty("text").GetString());
        Assert.Equal("Photos é", document.RootElement.GetProperty("attachments")[0].GetProperty("name").GetString());
        Assert.Contains("Café ✓", description, StringComparison.Ordinal);
        Assert.DoesNotContain("\\u00E9", description, StringComparison.Ordinal);
        Assert.Contains("\\r\\n\\t", description, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a picture whose file fingerprint is unknown and a mark whose picture cannot say what is under it are
    ///     described with an explicit null and the reason.
    /// </summary>
    [Fact]
    public void PayloadDescriber_Describe_MissingValues_AreWrittenAsNullAndReason()
    {
        // Arrange: a picture without a file fingerprint and a mark without a note and without source elements
        var mark = new PayloadMark(
            3,
            MarkKind.Arrow,
            [new RenderPoint(1, 1), new RenderPoint(5, 5)],
            new RenderBox(1, 1, 4, 4),
            null,
            CropBytes,
            new RenderBox(0, 0, 64, 64),
            [],
            false,
            "svg provides no element positions",
            false);
        var image = new PayloadImage(@"C:\project\logo.svg", null, "0f0f", "svg", 64, 32, AnnotatedBytes, [mark]);
        var payload = new MessagePayload("[Mark 3]", [new PayloadMarkPart(3)], [], [image], DateTimeOffset.UnixEpoch);

        // Act: describe it and read it back
        using var document = JsonDocument.Parse(PayloadDescriber.Describe(payload));
        var picture = document.RootElement.GetProperty("images")[0];
        var described = picture.GetProperty("marks")[0];
        var elements = described.GetProperty("sourceElements");

        // Assert: explicit nulls, the reason, and an empty list
        Assert.Equal(JsonValueKind.Null, picture.GetProperty("sourceFileHash").ValueKind);
        Assert.Equal(JsonValueKind.Null, described.GetProperty("note").ValueKind);
        Assert.False(elements.GetProperty("available").GetBoolean());
        Assert.Equal("svg provides no element positions", elements.GetProperty("unavailableReason").GetString());
        Assert.Equal(0, elements.GetProperty("elements").GetArrayLength());
        Assert.Equal("arrow", described.GetProperty("kind").GetString());
    }

    /// <summary>
    ///     Test that a missing payload is refused.
    /// </summary>
    [Fact]
    public void PayloadDescriber_Describe_NullPayload_ThrowsArgumentNullException()
    {
        // Act / Assert: no payload, no description
        Assert.Throws<ArgumentNullException>(() => PayloadDescriber.Describe(null!));
    }
}
