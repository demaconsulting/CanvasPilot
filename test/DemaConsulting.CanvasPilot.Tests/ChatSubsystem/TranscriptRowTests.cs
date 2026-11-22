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

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests of the rows of the transcript that the copilot adds to the sent messages of the user: the reply that streams,
///     the line about a tool, and the notice, and of the sentences that the tool rows say.
/// </summary>
public sealed class TranscriptRowTests
{
    /// <summary>
    ///     Test that a reply grows while it streams, announces each change, and keeps its text after it ended, when the
    ///     text is final only if the reply was completed.
    /// </summary>
    [Fact]
    public void AssistantReplyViewModel_StreamThenComplete_GrowsAnnouncesAndIsFinalOnlyWhenCompleted()
    {
        // Arrange: a reply that starts to stream
        var reply = new AssistantReplyViewModel();
        var changed = new List<string>();
        reply.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
        Assert.Equal((TranscriptRowKind.Assistant, string.Empty, AssistantReplyState.Streaming, true, false, false), (reply.Kind, reply.Text, reply.State, reply.IsStreaming, reply.IsComplete, reply.HasText));

        // Act: write
        reply.AppendText("Hello");
        reply.AppendText(string.Empty);
        reply.AppendText(" there");

        // Assert: the text grew and was announced; an empty piece changed nothing
        Assert.Equal("Hello there", reply.Text);
        Assert.True(reply.HasText);
        Assert.Equal(["Text", "HasText", "Text"], changed);

        // Act: complete, and then try to change it
        changed.Clear();
        reply.Complete();
        reply.AppendText("!");
        reply.Stop();
        reply.Fail();

        // Assert: it is final, and nothing changes after the end
        Assert.Equal((AssistantReplyState.Completed, true, false, string.Empty, "Hello there"), (reply.State, reply.IsComplete, reply.IsStreaming, reply.StatusLabel, reply.Text));
        Assert.Contains("State", changed);
        Assert.Single(changed, name => name == "State");
        Assert.Throws<ArgumentNullException>(() => reply.AppendText(null!));
    }

    /// <summary>
    ///     Test that a reply that was stopped or that failed keeps its text, is not final text for Markdown, and names what
    ///     became of it.
    /// </summary>
    [Theory]
    [InlineData(true, AssistantReplyState.Stopped, "Stopped")]
    [InlineData(false, AssistantReplyState.Failed, "Failed")]
    public void AssistantReplyViewModel_StopOrFail_KeepsTextAndNamesWhatBecameOfIt(bool stop, AssistantReplyState expected, string label)
    {
        // Arrange: a reply with text
        var reply = new AssistantReplyViewModel();
        reply.AppendText("partial **bol");

        // Act: end it
        if (stop)
        {
            reply.Stop();
        }
        else
        {
            reply.Fail();
        }

        reply.Complete();

        // Assert: the text is kept, the end is the first one, and the text is not final
        Assert.Equal((expected, label, "partial **bol", false), (reply.State, reply.StatusLabel, reply.Text, reply.IsComplete));
    }

    /// <summary>
    ///     Test that a reply made in a state, as a restored one is, is in that state and checks its text.
    /// </summary>
    [Fact]
    public void AssistantReplyViewModel_Restored_IsInTheGivenStateAndRefusesNoText()
    {
        // Act: make a restored reply
        var reply = new AssistantReplyViewModel("Earlier answer", AssistantReplyState.Completed);

        // Assert: it is final and cannot grow
        Assert.True(reply.IsComplete);
        reply.AppendText("more");
        Assert.Equal("Earlier answer", reply.Text);
        Assert.Throws<ArgumentNullException>(() => new AssistantReplyViewModel(null!, AssistantReplyState.Completed));
    }

    /// <summary>
    ///     Test that a tool row starts as running and working, ends with the result, keeps the report of a written file when
    ///     the tool worked, and shows the refusal when it did not, and that an ended row is not abandoned.
    /// </summary>
    [Fact]
    public void ToolActivityRowViewModel_StartFileReportAndFinish_KeepTheReportOnlyWhenTheToolWorked()
    {
        // Arrange: a running row
        var row = new ToolActivityRowViewModel("c1", "project_write", "path=x.svg", "Writing `x.svg`");
        var changed = new List<string>();
        row.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
        Assert.Equal((TranscriptRowKind.Tool, true, true, false, false, "c1", "project_write", "path=x.svg"), (row.Kind, row.IsRunning, row.Succeeded, row.HasImage, row.HasFileReport, row.CallId, row.ToolName, row.ArgumentSummary));

        // Act: the file was written, and then the tool answered
        row.ReportFile("Replaced `x.svg`");
        row.Finish("Wrote `x.svg`", succeeded: true, hasImage: true);

        // Assert: the report of the file stays, and the row ended with the picture marked
        Assert.Equal(("Replaced `x.svg`", false, true, true), (row.Summary, row.IsRunning, row.Succeeded, row.HasImage));
        Assert.Contains("Summary", changed);
        Assert.Contains("IsRunning", changed);

        // Act: a result that says the tool did not work replaces the report
        row.Finish("Could not write `x.svg`: refused", succeeded: false, hasImage: false);

        // Assert: the refusal is shown
        Assert.Equal(("Could not write `x.svg`: refused", false), (row.Summary, row.Succeeded));

        // Act: abandon a row that has ended
        row.Abandon("Writing `x.svg` (stopped)");

        // Assert: nothing changed
        Assert.Equal("Could not write `x.svg`: refused", row.Summary);
    }

    /// <summary>
    ///     Test that a restored tool row has ended, that a row can be abandoned once while it runs, and that the
    ///     arguments are checked.
    /// </summary>
    [Fact]
    public void ToolActivityRowViewModel_RestoredAndAbandoned_AreEndedAndArgumentsAreChecked()
    {
        // Act: make a restored row and a running one that is abandoned
        var restored = new ToolActivityRowViewModel("render_image", "Rendered `a.svg`", succeeded: true);
        var running = new ToolActivityRowViewModel("c2", "render_image", string.Empty, "Rendering a picture");
        running.Abandon("Rendering a picture (no result)");

        // Assert: the restored row has ended, and the abandoned row has failed
        Assert.Equal((false, true, string.Empty), (restored.IsRunning, restored.Succeeded, restored.CallId));
        Assert.Equal((false, false, "Rendering a picture (no result)"), (running.IsRunning, running.Succeeded, running.Summary));
        Assert.Throws<ArgumentException>(() => new ToolActivityRowViewModel(string.Empty, "t", string.Empty, "s"));
        Assert.Throws<ArgumentException>(() => new ToolActivityRowViewModel("c", string.Empty, string.Empty, "s"));
        Assert.Throws<ArgumentNullException>(() => new ToolActivityRowViewModel("c", "t", null!, "s"));
        Assert.Throws<ArgumentNullException>(() => new ToolActivityRowViewModel("c", "t", string.Empty, null!));
        Assert.Throws<ArgumentException>(() => new ToolActivityRowViewModel(string.Empty, "s", true));
        Assert.Throws<ArgumentNullException>(() => running.Finish(null!, true, false));
        Assert.Throws<ArgumentNullException>(() => running.ReportFile(null!));
        Assert.Throws<ArgumentNullException>(() => running.Abandon(null!));
    }

    /// <summary>
    ///     Test that a notice holds its message, its remedy, and whether the settings can help, and refuses an empty
    ///     message.
    /// </summary>
    [Fact]
    public void NoticeRowViewModel_ErrorWithRemedyAndInformation_HoldWhatTheyWereGiven()
    {
        // Act: make an error with a remedy and an information
        var error = new NoticeRowViewModel(NoticeSeverity.Error, "Ollama is not running.", "Start Ollama.", canOpenSettings: true);
        var information = new NoticeRowViewModel(NoticeSeverity.Information, "Stopped.");

        // Assert: each holds what it was given
        Assert.Equal((TranscriptRowKind.Notice, true, "Ollama is not running.", "Start Ollama.", true, true), (error.Kind, error.IsError, error.Message, error.Remedy, error.HasRemedy, error.CanOpenSettings));
        Assert.Equal((false, string.Empty, false, false), (information.IsError, information.Remedy, information.HasRemedy, information.CanOpenSettings));
        Assert.Throws<ArgumentException>(() => new NoticeRowViewModel(NoticeSeverity.Error, " "));
        Assert.Throws<ArgumentNullException>(() => new NoticeRowViewModel(NoticeSeverity.Error, "m", null!));
    }

    /// <summary>
    ///     Test that the rows of the user, the copilot, the tools, and the notices tell their kind, and that the row of a
    ///     sent message is still the entry of the transcript with its payload and its summary.
    /// </summary>
    [Fact]
    public void TranscriptRows_Kinds_TheEntryOfAMessageIsTheUserRow()
    {
        // Arrange: the entry of a message with a payload
        using var entry = new TranscriptEntryViewModel(ChatTestMessages.SentText("hello"), projectFolder: null, () => null);
        TranscriptRowViewModel[] rows =
        [
            entry,
            new AssistantReplyViewModel(),
            new ToolActivityRowViewModel("t", "ok", true),
            new NoticeRowViewModel(NoticeSeverity.Information, "n"),
        ];

        // Assert: each row tells what it is, and the entry still holds its payload
        Assert.Equal([TranscriptRowKind.User, TranscriptRowKind.Assistant, TranscriptRowKind.Tool, TranscriptRowKind.Notice], rows.Select(row => row.Kind));
        Assert.True(entry.HasPayload);
        Assert.Equal(string.Empty, entry.Summary);
        foreach (var row in rows.Skip(1))
        {
            row.Dispose();
        }
    }

    /// <summary>
    ///     Test that the value of an argument is found in the short description of the arguments, also when it holds a
    ///     comma, and that a missing or empty value gives the fallback of the tool.
    /// </summary>
    [Theory]
    [InlineData("render_image", "path=a, b.svg, size=3", "Rendering `a, b.svg`")]
    [InlineData("render_image", "size=3", "Rendering a picture")]
    [InlineData("render_image", "path=", "Rendering a picture")]
    [InlineData("project_read", "mode=x, path=notes/todo.txt", "Reading `notes/todo.txt`")]
    [InlineData("project_write", "", "Writing a file")]
    public void ToolActivityText_Started_FindsThePathInTheArguments(string tool, string arguments, string expected)
    {
        // Act / Assert: the sentence names the path, or the fallback
        Assert.Equal(expected, ToolActivityText.Started(tool, arguments));
    }

    /// <summary>
    ///     Test that the sentences of an unknown tool and of a failure without a reason end properly.
    /// </summary>
    [Fact]
    public void ToolActivityText_FinishedAndWritten_SayWhatHappened()
    {
        // Act / Assert: an unknown tool that failed, a known tool that failed without a reason, and the file sentences
        Assert.Equal("mystery failed: no way", ToolActivityText.Finished("mystery", string.Empty, false, "no way"));
        Assert.Equal("mystery failed.", ToolActivityText.Finished("mystery", string.Empty, false, string.Empty));
        Assert.Equal("Could not render `a.svg`.", ToolActivityText.Finished("render_image", "path=a.svg", false, string.Empty));
        Assert.Equal("Created `a.svg`", ToolActivityText.Written("a.svg", replaced: false));
        Assert.Equal("Replaced `a.svg`", ToolActivityText.Written("a.svg", replaced: true));
        Assert.Equal("Rendering a picture (stopped)", ToolActivityText.Abandoned("Rendering a picture", stopped: true));
        Assert.Equal("Rendering a picture (no result)", ToolActivityText.Abandoned("Rendering a picture", stopped: false));
    }
}
