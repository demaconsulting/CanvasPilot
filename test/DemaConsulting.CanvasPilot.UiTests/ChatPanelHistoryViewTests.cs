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

using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for the chat history in the chat panel: the button that clears the chat history, and the rows of
///     a conversation that was restored from the history saved on this computer, which show words only. The panel, the
///     composer, the store, and the views of the rows are the real ones in a window; the copilot is a scripted fake.
/// </summary>
public sealed class ChatPanelHistoryViewTests
{
    /// <summary>
    ///     Test that the button that clears the chat history is shown, and that choosing it empties the transcript except
    ///     for a notice that says so, deletes the saved history, makes the copilot forget the conversation, and leaves the
    ///     composer able to send the next message.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_ClearChatHistoryButton_ClearsTranscriptDeletesHistoryAndSendStaysEnabled()
    {
        // Arrange: a conversation with one answered message, saved on this computer
        var historyRoot = Directory.CreateTempSubdirectory("canvaspilot-ui-history-").FullName;
        try
        {
            var store = new ChatHistoryStore(historyRoot);
            var agent = new ScriptedAgentService();
            using var harness = new ChatHarness(agent: agent, history: store);
            Assert.True(harness.OverflowButton.IsEffectivelyVisible);
            Assert.Equal("Clear chat history", harness.ClearHistoryItem.Header);
            harness.Type("Draw a box");
            harness.ClickSend();
            agent.Emit(new AgentEvent.TextDelta("Here is the box."), new AgentEvent.Completed());
            Assert.True(harness.WaitFor(() => !harness.Chat.IsCopilotBusy && harness.Replies.Count == 1));
            Assert.Single(harness.Entries);
            Assert.True(File.Exists(store.HistoryFilePath(harness.ProjectInfo)));
            harness.Type("The next one");

            // Act: choose Clear chat history
            harness.ClearHistory();

            // Assert: the conversation is gone from the screen except for the notice, and from the computer
            Assert.True(harness.WaitFor(() => harness.Notices.Count == 1));
            Assert.Empty(harness.Entries);
            Assert.Empty(harness.Replies);
            Assert.Equal(ChatPanelViewModel.ClearedText, Assert.Single(harness.Notices).FindControl<SelectableTextBlock>("MessageText")!.Text);
            Assert.False(Directory.Exists(store.FolderOf(harness.ProjectInfo)));
            Assert.Equal(1, agent.ResetCount);

            // Assert: the draft was kept and can be sent
            Assert.Equal("The next one", harness.Composer.Document.FlatText);
            Assert.True(harness.SendButton.IsEffectivelyEnabled);
        }
        finally
        {
            Directory.Delete(historyRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the button is there, and harmless, when nothing was said and there is no store and no copilot.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_ClearChatHistoryButton_NothingSaid_ShowsOnlyTheNotice()
    {
        // Arrange: a panel with neither a store nor a copilot
        using var harness = new ChatHarness();

        // Act
        harness.ClearHistory();

        // Assert
        Assert.True(harness.WaitFor(() => harness.Notices.Count == 1));
        Assert.Empty(harness.Entries);
    }

    /// <summary>
    ///     Test that a conversation restored from the saved history is shown as words: the message of the user without a
    ///     payload button or pictures, with its label and lines for the marks and attachments, and the reply and the tool
    ///     line as usual.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_RestoredHistory_ShowsTextRowsWithoutPayloadButtonOrPictures()
    {
        // Arrange: a saved conversation of the project, and a panel that opens it
        var historyRoot = Directory.CreateTempSubdirectory("canvaspilot-ui-history-").FullName;
        try
        {
            var store = new ChatHistoryStore(historyRoot);
            using var harness = new ChatHarness(
                history: store,
                prepare: project => store.Save(
                    project,
                    [
                        new ChatHistoryRow
                        {
                            Kind = ChatHistoryRowKind.User,
                            Utc = new DateTimeOffset(2026, 5, 6, 7, 8, 9, TimeSpan.Zero),
                            Text = "Why is [Mark 1] red?",
                            Marks = [new ChatHistoryMark(1, "check this")],
                            Attachments = ["attachments/logo.png"],
                        },
                        new ChatHistoryRow { Kind = ChatHistoryRowKind.Assistant, Text = "Because of the fill.", Reply = ChatHistoryReplyState.Completed },
                        new ChatHistoryRow { Kind = ChatHistoryRowKind.Tool, Tool = "project_write", Text = "Replaced `flow.svg`", Succeeded = true },
                    ]));

            // Assert: the message is words with the label and the lines for the mark and the attachment, and has no payload button
            var restored = Assert.Single(harness.RestoredEntries);
            Assert.Equal("Why is [Mark 1] red?", restored.FindControl<SelectableTextBlock>("MessageText")!.Text);
            Assert.Equal("Mark 1: check this\nAttachment: attachments/logo.png", restored.FindControl<SelectableTextBlock>("DetailsText")!.Text);
            Assert.Equal(RestoredEntryViewModel.RestoredNote, restored.FindControl<TextBlock>("RestoredNoteText")!.Text);
            Assert.Empty(harness.Entries);
            Assert.Empty(harness.FindAll<Button>("TranscriptViewPayloadButton"));
            Assert.Empty(harness.Window.GetVisualDescendants().OfType<ChipView>());

            // Assert: the answer and the tool line are the usual rows
            Assert.Equal("Because of the fill.", Assert.Single(harness.Replies).FindControl<SelectableTextBlock>("ReplyText")!.Text);
            Assert.Equal("Replaced flow.svg", Assert.Single(harness.ToolRows).FindControl<TextBlock>("SummaryText")!.Text);
            Assert.DoesNotContain(harness.FindAll<TextBlock>("ChatTranscriptEmpty"), text => text.IsEffectivelyVisible);
        }
        finally
        {
            Directory.Delete(historyRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that a conversation that was restored from the saved history, and is longer than the panel is high, opens
    ///     scrolled to its latest message and not to its oldest.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_RestoredHistory_OpensScrolledToTheLatestMessage()
    {
        // Arrange: a saved conversation of the project that is longer than the panel is high
        var store = new MemoryChatHistoryStore();

        // Act: a panel opens the project, which restores the conversation before the panel is laid out
        using var harness = new ChatHarness(history: store, prepare: project => store.Save(project, ChatHarness.LongConversation()));
        harness.Layout();

        // Assert: the transcript is at its end
        Assert.True(harness.Chat.Transcript.Count >= 40);
        Assert.True(harness.IsAtTheEndOfALongTranscript, $"Offset {harness.TranscriptScroll.Offset.Y} of {harness.TranscriptScroll.Extent.Height}.");
    }
}
