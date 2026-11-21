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

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests for the <see cref="ComposerDocument" /> unit, the segment records it holds, and its snapshots.
/// </summary>
public sealed class ComposerDocumentTests
{
    /// <summary>
    ///     The character that stands for a chip in the flat text.
    /// </summary>
    private const string Chip = "\uFFFC";
    /// <summary>
    ///     Test that text inserted into an empty document is one typed segment and the flat text equals it.
    /// </summary>
    [Fact]
    public void ComposerDocument_InsertText_IntoEmptyDocument_HoldsOneTypedSegment()
    {
        // Arrange: an empty document
        var document = new ComposerDocument();

        // Act: insert typed text at the start
        var inserted = document.InsertText(0, "hello", TextOrigin.Typed);

        // Assert: one typed segment holds the text and the length counts its characters
        Assert.Equal("hello", inserted);
        Assert.Equal(new TextSegment("hello", TextOrigin.Typed), Assert.Single(document.Segments));
        Assert.Equal(5, document.Length);
        Assert.Equal("hello", document.FlatText);
    }

    /// <summary>
    ///     Test that text inserted in the middle of text goes in at the offset.
    /// </summary>
    [Fact]
    public void ComposerDocument_InsertText_InTheMiddle_InsertsAtOffset()
    {
        // Arrange: a document that holds "ac"
        var document = new ComposerDocument();
        document.InsertText(0, "ac", TextOrigin.Typed);

        // Act: insert "b" between the two characters
        document.InsertText(1, "b", TextOrigin.Typed);

        // Assert: the text reads "abc" and is still one segment
        Assert.Equal("abc", document.FlatText);
        Assert.Single(document.Segments);
    }

    /// <summary>
    ///     Test that adjacent text of the same origin is joined into one segment.
    /// </summary>
    [Fact]
    public void ComposerDocument_InsertText_NextToTextOfSameOrigin_JoinsIntoOneSegment()
    {
        // Arrange: a document with typed text
        var document = new ComposerDocument();
        document.InsertText(0, "one ", TextOrigin.Typed);

        // Act: add more typed text after it
        document.InsertText(4, "two", TextOrigin.Typed);

        // Assert: both pieces are one segment
        Assert.Equal(new TextSegment("one two", TextOrigin.Typed), Assert.Single(document.Segments));
    }

    /// <summary>
    ///     Test that text of a different origin stays a separate segment and splits the text around it.
    /// </summary>
    [Fact]
    public void ComposerDocument_InsertText_DictatedInsideTypedText_KeepsOriginsSeparate()
    {
        // Arrange: typed text "one three"
        var document = new ComposerDocument();
        document.InsertText(0, "one three", TextOrigin.Typed);

        // Act: dictate "two " inside it
        document.InsertText(4, "two ", TextOrigin.Dictated);

        // Assert: typed, dictated, and typed segments in that order
        Assert.Equal(
            [
                new TextSegment("one ", TextOrigin.Typed),
                new TextSegment("two ", TextOrigin.Dictated),
                new TextSegment("three", TextOrigin.Typed)
            ],
            document.Segments);
        Assert.Equal("one two three", document.FlatText);
    }

    /// <summary>
    ///     Test that a chip inserted inside text splits the text and counts as one position.
    /// </summary>
    [Fact]
    public void ComposerDocument_InsertChip_BetweenText_SplitsTextAndCountsOnePosition()
    {
        // Arrange: typed text "ab"
        var document = new ComposerDocument();
        document.InsertText(0, "ab", TextOrigin.Typed);
        var chip = new FileChip(@"C:\data\notes.txt");

        // Act: insert the chip between the two characters
        document.InsertChip(1, chip);

        // Assert: text, chip, text; the length is the characters plus one; the flat text has one placeholder
        Assert.Equal(
            [new TextSegment("a", TextOrigin.Typed), chip, new TextSegment("b", TextOrigin.Typed)],
            document.Segments);
        Assert.Equal(3, document.Length);
        Assert.Equal("a" + Chip + "b", document.FlatText);
    }

    /// <summary>
    ///     Test that chips can be inserted at the start and the end of the document.
    /// </summary>
    [Fact]
    public void ComposerDocument_InsertChip_AtStartAndEnd_PlacesChipsAtTheEnds()
    {
        // Arrange: typed text and two chips
        var document = new ComposerDocument();
        document.InsertText(0, "mid", TextOrigin.Typed);
        var first = new FolderChip(@"C:\data\first");
        var last = new MarkChip(Guid.NewGuid(), 1);

        // Act: insert one chip at the start and one at the end
        document.InsertChip(0, first);
        document.InsertChip(document.Length, last);

        // Assert: the chips surround the text
        Assert.Equal([first, new TextSegment("mid", TextOrigin.Typed), last], document.Segments);
    }

    /// <summary>
    ///     Test that the chips are listed in the order they have in the message.
    /// </summary>
    [Fact]
    public void ComposerDocument_Chips_MixedContent_AreEnumeratedInOrder()
    {
        // Arrange: text with three chips of different kinds
        var document = new ComposerDocument();
        var file = new FileChip(@"C:\a.txt");
        var image = new ImageChip(@"C:\b.png", isPasted: true);
        var mark = new MarkChip(Guid.NewGuid(), 2);
        document.InsertText(0, "x y z", TextOrigin.Typed);
        document.InsertChip(1, file);
        document.InsertChip(3, image);
        document.InsertChip(5, mark);

        // Act: read the chips
        var chips = document.Chips.ToList();

        // Assert: they come in message order
        Assert.Equal<ChipSegment>([file, image, mark], chips);
    }

    /// <summary>
    ///     Test that deleting a range across text and chips removes it and returns what was removed.
    /// </summary>
    [Fact]
    public void ComposerDocument_Delete_RangeAcrossTextAndChips_RemovesAndReturnsSegments()
    {
        // Arrange: "ab", a chip, "cd" (five positions)
        var document = new ComposerDocument();
        var chip = new FileChip(@"C:\a.txt");
        document.InsertText(0, "abcd", TextOrigin.Typed);
        document.InsertChip(2, chip);

        // Act: delete from offset 1 for three positions, "b", the chip, and "c"
        var removed = document.Delete(1, 3);

        // Assert: "ad" is left and the removed segments are returned in order
        Assert.Equal("ad", document.FlatText);
        Assert.Equal(
            [new TextSegment("b", TextOrigin.Typed), chip, new TextSegment("c", TextOrigin.Typed)],
            removed);
    }

    /// <summary>
    ///     Test that deleting a chip joins the text on both sides when it has the same origin.
    /// </summary>
    [Fact]
    public void ComposerDocument_Delete_SingleChip_JoinsNeighboringText()
    {
        // Arrange: "a", a chip, "b"
        var document = new ComposerDocument();
        document.InsertText(0, "ab", TextOrigin.Typed);
        document.InsertChip(1, new FileChip(@"C:\a.txt"));

        // Act: delete the chip
        var removed = document.Delete(1, 1);

        // Assert: the text is one segment again
        Assert.IsType<FileChip>(Assert.Single(removed));
        Assert.Equal(new TextSegment("ab", TextOrigin.Typed), Assert.Single(document.Segments));
    }

    /// <summary>
    ///     Test that deleting no positions changes nothing.
    /// </summary>
    [Fact]
    public void ComposerDocument_Delete_ZeroLength_ChangesNothing()
    {
        // Arrange: a document with text
        var document = new ComposerDocument();
        document.InsertText(0, "abc", TextOrigin.Typed);

        // Act: delete zero positions
        var removed = document.Delete(1, 0);

        // Assert: nothing was removed
        Assert.Empty(removed);
        Assert.Equal("abc", document.FlatText);
    }

    /// <summary>
    ///     Test that a slice cuts through text and leaves the document as it was.
    /// </summary>
    [Fact]
    public void ComposerDocument_GetSlice_InsideText_CutsTextWithoutChangingDocument()
    {
        // Arrange: dictated text
        var document = new ComposerDocument();
        document.InsertText(0, "abcdef", TextOrigin.Dictated);

        // Act: take a slice from the middle
        var slice = document.GetSlice(2, 3);

        // Assert: the slice holds the cut text with its origin and the document is unchanged
        Assert.Equal(new TextSegment("cde", TextOrigin.Dictated), Assert.Single(slice));
        Assert.Equal("abcdef", document.FlatText);
    }

    /// <summary>
    ///     Test that the chip placeholder and control characters are removed from text, that every kind of line break
    ///     becomes one line feed, and that tab and line feed stay.
    /// </summary>
    [Theory]
    [InlineData("a" + Chip + "b", "ab")]
    [InlineData("a\u0000b\u0007c", "abc")]
    [InlineData("a\u001bb", "ab")]
    [InlineData("a\tb\nc", "a\tb\nc")]
    [InlineData("plain text", "plain text")]
    [InlineData("\u0085", "")]
    [InlineData("a\r\nb", "a\nb")]
    [InlineData("a\rb", "a\nb")]
    [InlineData("a\u2028b\u2029c", "a\nb\nc")]
    [InlineData("a\r\n\r\nb", "a\n\nb")]
    public void ComposerDocument_SanitizeText_UnwantedCharacters_AreRemovedOrNormalized(string input, string expected)
    {
        // Arrange: text from outside the application (the theory data)

        // Act: clean it
        var cleaned = ComposerDocument.SanitizeText(input);

        // Assert: only the unwanted characters are gone and every line break is one line feed
        Assert.Equal(expected, cleaned);
    }

    /// <summary>
    ///     Test that text made only of the chip placeholder inserts nothing, so it cannot shift the chips.
    /// </summary>
    [Fact]
    public void ComposerDocument_InsertText_OnlyChipPlaceholders_InsertsNothing()
    {
        // Arrange: a document with a chip
        var document = new ComposerDocument();
        document.InsertChip(0, new FileChip(@"C:\a.txt"));

        // Act: insert text that is only placeholders
        var inserted = document.InsertText(0, Chip + Chip, TextOrigin.Dictated);

        // Assert: nothing was inserted and the one chip is still the only position
        Assert.Equal(string.Empty, inserted);
        Assert.Equal(1, document.Length);
    }

    /// <summary>
    ///     Test that text made into a segment is cleaned at once, so no segment holds a placeholder.
    /// </summary>
    [Fact]
    public void TextSegment_Constructor_TextWithPlaceholder_HoldsCleanedText()
    {
        // Arrange and Act: make a segment from text with a placeholder and a control character
        var segment = new TextSegment("a" + Chip + "b\u0001", TextOrigin.Typed);

        // Assert: the segment holds clean text and its length
        Assert.Equal("ab", segment.Text);
        Assert.Equal(2, segment.Length);
    }

    /// <summary>
    ///     Test that the flat text has one placeholder for each chip and the length is characters plus chips.
    /// </summary>
    [Fact]
    public void ComposerDocument_FlatText_TextAndChips_LengthIsCharactersPlusChips()
    {
        // Arrange: three characters and two chips
        var document = new ComposerDocument();
        document.InsertText(0, "abc", TextOrigin.Typed);
        document.InsertChip(0, new FileChip(@"C:\a.txt"));
        document.InsertChip(4, new MarkChip(Guid.NewGuid(), 1));

        // Act: read the flat text
        var flat = document.FlatText;

        // Assert: five positions, two of them placeholders
        Assert.Equal(5, document.Length);
        Assert.Equal(5, flat.Length);
        Assert.Equal(2, flat.Count(character => character == Chip[0]));
        Assert.Equal(Chip + "abc" + Chip, flat);
    }

    /// <summary>
    ///     Test that a snapshot does not change when the document changes afterwards.
    /// </summary>
    [Fact]
    public void ComposerDocument_Snapshot_AfterLaterEdits_StaysUnchanged()
    {
        // Arrange: a document with text and a chip, and its snapshot
        var document = new ComposerDocument();
        document.InsertText(0, "keep", TextOrigin.Typed);
        document.InsertChip(4, new FolderChip(@"C:\data"));
        var snapshot = document.Snapshot();

        // Act: change the document in every way
        document.InsertText(0, "more ", TextOrigin.Dictated);
        document.Delete(0, 3);
        document.Clear();

        // Assert: the snapshot still holds the first content
        Assert.Equal("keep" + Chip, snapshot.FlatText);
        Assert.Equal(5, snapshot.Length);
        Assert.IsType<FolderChip>(Assert.Single(snapshot.Chips));
        Assert.Equal(2, snapshot.Segments.Length);
        Assert.Equal(0, document.Length);
    }

    /// <summary>
    ///     Test that two snapshots of equal content are the same content and different content is not.
    /// </summary>
    [Fact]
    public void ComposerSnapshot_HasSameContentAs_EqualAndDifferentContent_ComparesSegments()
    {
        // Arrange: two documents with the same content and a third with other content
        var first = new ComposerDocument();
        var second = new ComposerDocument();
        var other = new ComposerDocument();
        first.InsertText(0, "same", TextOrigin.Typed);
        second.InsertText(0, "sa", TextOrigin.Typed);
        second.InsertText(2, "me", TextOrigin.Typed);
        other.InsertText(0, "same", TextOrigin.Dictated);

        // Act and Assert: equal content compares equal even though it was built differently; the origin matters
        Assert.True(first.Snapshot().HasSameContentAs(second.Snapshot()));
        Assert.False(first.Snapshot().HasSameContentAs(other.Snapshot()));
    }

    /// <summary>
    ///     Test that every operation rejects offsets and ranges outside the document.
    /// </summary>
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(4, 0)]
    [InlineData(0, -1)]
    [InlineData(2, 3)]
    public void ComposerDocument_InvalidOffsetOrRange_ThrowsArgumentOutOfRangeException(int offset, int length)
    {
        // Arrange: a document of three positions
        var document = new ComposerDocument();
        document.InsertText(0, "abc", TextOrigin.Typed);

        // Act and Assert: reading or deleting the range is rejected and the document is unchanged
        Assert.Throws<ArgumentOutOfRangeException>(() => document.GetSlice(offset, length));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Delete(offset, length));
        Assert.Equal("abc", document.FlatText);
    }

    /// <summary>
    ///     Test that insertion rejects an offset outside the document.
    /// </summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void ComposerDocument_InsertWithInvalidOffset_ThrowsArgumentOutOfRangeException(int offset)
    {
        // Arrange: a document of three positions
        var document = new ComposerDocument();
        document.InsertText(0, "abc", TextOrigin.Typed);

        // Act and Assert: inserting text or a chip at that offset is rejected
        Assert.Throws<ArgumentOutOfRangeException>(() => document.InsertText(offset, "x", TextOrigin.Typed));
        Assert.Throws<ArgumentOutOfRangeException>(() => document.InsertChip(offset, new FileChip(@"C:\a.txt")));
        Assert.Equal("abc", document.FlatText);
    }

    /// <summary>
    ///     Test that null arguments and null segments are rejected.
    /// </summary>
    [Fact]
    public void ComposerDocument_NullArguments_Throw()
    {
        // Arrange: an empty document
        var document = new ComposerDocument();

        // Act and Assert: null text, chip, segment list, and segment are rejected
        Assert.Throws<ArgumentNullException>(() => document.InsertText(0, null!, TextOrigin.Typed));
        Assert.Throws<ArgumentNullException>(() => document.InsertChip(0, null!));
        Assert.Throws<ArgumentNullException>(() => document.Insert(0, null!));
        Assert.Throws<ArgumentException>(() => document.Insert(0, [null!]));
        Assert.Throws<ArgumentNullException>(() => ComposerDocument.SanitizeText(null!));
    }

    /// <summary>
    ///     Test that clearing the document empties it and the live list of segments follows.
    /// </summary>
    [Fact]
    public void ComposerDocument_Clear_DocumentWithContent_EmptiesDocumentAndLiveSegments()
    {
        // Arrange: a document with content and its live segment list
        var document = new ComposerDocument();
        document.InsertText(0, "abc", TextOrigin.Typed);
        document.InsertChip(1, new FileChip(@"C:\a.txt"));
        var live = document.Segments;

        // Act: clear it
        document.Clear();

        // Assert: nothing is left, also in the list that was read before
        Assert.Equal(0, document.Length);
        Assert.Empty(document.Segments);
        Assert.Empty(live);
    }

    /// <summary>
    ///     Test that chips are named by the last part of their path and that mark chips are labeled by their number.
    /// </summary>
    [Fact]
    public void ComposerSegment_Chips_ReportNameAndLabel()
    {
        // Arrange: chips for a file, a folder with a trailing separator, a drive root, and a mark
        var file = new FileChip(Path.Combine(Path.GetTempPath(), "notes.txt"));
        var folder = new FolderChip(Path.Combine(Path.GetTempPath(), "photos") + Path.DirectorySeparatorChar);
        var root = new FolderChip(Path.GetPathRoot(Path.GetTempPath())!);
        var mark = new MarkChip(Guid.NewGuid(), 3);

        // Act and Assert: names are the last parts, the root is its own name, and the mark is labeled
        Assert.Equal("notes.txt", file.Name);
        Assert.Equal("photos", folder.Name);
        Assert.Equal(root.FullPath, root.Name);
        Assert.Equal("Mark 3", mark.Label);
        Assert.Equal(1, mark.Length);
        Assert.Throws<ArgumentException>(() => new FileChip(" "));
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarkChip(Guid.NewGuid(), 0));
    }
}
