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
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests for what the chat panel shows of the AI copilot: the streamed answer and its Markdown when it is
///     whole, the indicator that the copilot works and the Stop button, the rows of tools, the error rows with the remedy
///     and the button that opens the settings, and the banner of the readiness. The panel, the composer, and the views of
///     the rows are the real ones in a window; the copilot is a scripted fake that answers when the test says so.
/// </summary>
public sealed class ChatPanelCopilotViewTests
{
    /// <summary>
    ///     Test that the answer appears as plain selectable text while it streams and is drawn from its Markdown when the
    ///     turn completes.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_CopilotAnswer_StreamsPlainTextThenMarkdownWhenComplete()
    {
        // Arrange: a message that was sent
        var agent = new ScriptedAgentService();
        using var harness = new ChatHarness(agent: agent);
        harness.Type("Draw a box");
        harness.ClickSend();

        // Act: the copilot starts to answer
        agent.Emit(new AgentEvent.TextDelta("Here is **the box**"));
        Assert.True(harness.WaitFor(() => harness.Replies.Count == 1));

        // Assert: the text appears as it streams, selectable and plain, and the Markdown is not shown yet
        var reply = Assert.Single(harness.Replies);
        var text = reply.FindControl<SelectableTextBlock>("ReplyText")!;
        var markdown = reply.FindControl<MarkdownBlocksPanel>("ReplyMarkdown")!;
        Assert.Equal("Here is **the box**", text.Text);
        Assert.True(text.IsEffectivelyVisible);
        Assert.False(markdown.IsEffectivelyVisible);

        // Act: the turn completes
        agent.Emit(new AgentEvent.Completed());
        Assert.True(harness.WaitFor(() => !harness.Chat.IsCopilotBusy));

        // Assert: the final text is drawn from its Markdown, so the asterisks are gone, and the plain text is hidden
        Assert.True(markdown.IsEffectivelyVisible);
        Assert.False(text.IsEffectivelyVisible);
        var drawn = Assert.IsType<SelectableTextBlock>(Assert.Single(markdown.Children));
        Assert.Equal("Here is the box", string.Concat(drawn.Inlines!.OfType<Avalonia.Controls.Documents.Run>().Select(run => run.Text)));
    }

    /// <summary>
    ///     Test that the panel shows that the copilot works and offers Stop while a turn runs, blocks sending but not
    ///     writing, and returns to normal when the turn has ended.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_CopilotWorking_ShowsIndicatorAndStopAndBlocksSend()
    {
        // Arrange: nothing runs yet
        var agent = new ScriptedAgentService();
        using var harness = new ChatHarness(agent: agent);
        Assert.False(harness.BusyRow.IsEffectivelyVisible);
        harness.Type("Draw a box");
        harness.ClickSend();

        // Act: the turn runs, and the user writes the next message
        Assert.True(harness.WaitFor(() => harness.BusyRow.IsEffectivelyVisible));
        harness.Type("And a circle");

        // Assert: the indicator and Stop are shown, Send cannot be used, and the reason is held by the composer
        Assert.Equal(ChatPanelViewModel.WorkingText, harness.Find<TextBlock>("ChatBusyText").Text);
        Assert.True(harness.StopButton.IsEffectivelyEnabled);
        Assert.False(harness.SendButton.IsEffectivelyEnabled);
        Assert.Equal("And a circle", harness.Composer.Document.FlatText);
        Assert.Equal(ComposerSubsystem.ComposerViewModel.CopilotWorkingText, harness.Composer.SendBlockedReason);

        // Act: the turn ends
        agent.Emit(new AgentEvent.Completed());
        Assert.True(harness.WaitFor(() => !harness.BusyRow.IsEffectivelyVisible));

        // Assert: Send can be used again for what was written meanwhile
        Assert.True(harness.SendButton.IsEffectivelyEnabled);
        Assert.False(harness.StopButton.IsEffectivelyVisible);
    }

    /// <summary>
    ///     Test that pressing Stop cancels the turn, keeps the text that was written marked as stopped, and enables Send
    ///     again.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_Stop_CancelsTurnKeepsPartialTextAndEnablesSend()
    {
        // Arrange: a turn that has written some text
        var agent = new ScriptedAgentService();
        using var harness = new ChatHarness(agent: agent);
        harness.Type("Draw a box");
        harness.ClickSend();
        agent.Emit(new AgentEvent.TextDelta("Let me think about"));
        Assert.True(harness.WaitFor(() => harness.Replies.Count == 1));
        harness.Type("Never mind");
        Assert.False(harness.SendButton.IsEffectivelyEnabled);

        // Act: press Stop
        SettingsUiHelpers.Click(harness.Window, harness.StopButton);
        Assert.True(harness.WaitFor(() => !harness.Chat.IsCopilotBusy));

        // Assert: the turn was canceled, the text is kept and marked, Send can be used, and Stop is gone
        Assert.True(agent.LatestToken.IsCancellationRequested);
        var reply = Assert.Single(harness.Replies);
        Assert.Equal("Let me think about", reply.FindControl<SelectableTextBlock>("ReplyText")!.Text);
        Assert.Equal("Stopped", reply.FindControl<TextBlock>("StatusLabelText")!.Text);
        Assert.True(reply.FindControl<TextBlock>("StatusLabelText")!.IsEffectivelyVisible);
        Assert.True(harness.SendButton.IsEffectivelyEnabled);
        Assert.False(harness.BusyRow.IsEffectivelyVisible);
    }

    /// <summary>
    ///     Test that a failed turn shows an error row with what happened, the remedy, and the button that asks to open the
    ///     settings.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_ProviderFails_ShowsErrorRowWithRemedyAndOpenSettings()
    {
        // Arrange: a message, and a request for the settings is counted
        var agent = new ScriptedAgentService();
        using var harness = new ChatHarness(agent: agent);
        var requests = 0;
        harness.Chat.OpenSettingsRequested += (_, _) => requests++;
        harness.Type("Draw a box");
        harness.ClickSend();

        // Act: the provider fails
        agent.Emit(new AgentEvent.Failed(AgentFailureKind.ConnectionLost, "Ollama did not answer."));
        Assert.True(harness.WaitFor(() => harness.Notices.Count == 1));

        // Assert: the row says what happened and what to do, and offers the settings
        var notice = Assert.Single(harness.Notices);
        Assert.Equal("Ollama did not answer.", notice.FindControl<SelectableTextBlock>("MessageText")!.Text);
        var remedy = notice.FindControl<SelectableTextBlock>("RemedyText")!;
        Assert.True(remedy.IsEffectivelyVisible);
        Assert.False(string.IsNullOrWhiteSpace(remedy.Text));
        var button = notice.FindControl<Button>("OpenSettingsButton")!;
        Assert.True(button.IsEffectivelyVisible);

        // Act: choose "Open Settings"
        SettingsUiHelpers.Click(harness.Window, button);

        // Assert: the shell was asked to open the settings, and the composer is free again
        Assert.Equal(1, requests);
        Assert.False(harness.Composer.IsSendBlocked);
    }

    /// <summary>
    ///     Test that the tools that the copilot calls are lines in the transcript, between the parts of the answer, that say
    ///     what was done.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_ToolCalls_ShowToolRowsInOrder()
    {
        // Arrange: a message
        var agent = new ScriptedAgentService();
        using var harness = new ChatHarness(agent: agent);
        harness.Type("Draw a box");
        harness.ClickSend();

        // Act: the copilot talks, writes a file, renders it, and talks again
        agent.Emit(
            new AgentEvent.TextDelta("Drawing."),
            new AgentEvent.ToolStarted("c1", "project_write", "path=box.svg, content=(80 characters)"),
            new AgentEvent.FileWritten("box.svg", false),
            new AgentEvent.ToolFinished("c1", "project_write", true, "File 'box.svg' created.", false),
            new AgentEvent.ToolStarted("c2", "render_image", "path=box.svg"),
            new AgentEvent.ToolFinished("c2", "render_image", true, "Rendered box.svg.", true),
            new AgentEvent.TextDelta("Done."),
            new AgentEvent.Completed());
        Assert.True(harness.WaitFor(() => !harness.Chat.IsCopilotBusy && harness.Replies.Count == 2));

        // Assert: two tool lines, the first saying the file was created, and no line still running
        Assert.Equal(2, harness.ToolRows.Count);
        Assert.Equal("Created `box.svg`", harness.ToolRows[0].FindControl<TextBlock>("SummaryText")!.Text);
        Assert.All(harness.ToolRows, row => Assert.False(row.FindControl<PathIcon>("RunningSpinner")!.IsEffectivelyVisible));
        Assert.All(harness.ToolRows, row => Assert.True(row.FindControl<PathIcon>("SuccessIcon")!.IsEffectivelyVisible));
        Assert.Equal(["Drawing.", "Done."], harness.Replies.Select(reply => reply.DataContext).OfType<AssistantReplyViewModel>().Select(reply => reply.Text));
    }

    /// <summary>
    ///     Test that the banner shows the sentence of a provider that cannot be used, including the sentence of GitHub
    ///     Copilot that is not signed in, offers the settings and a new check, and goes away when the provider is ready.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_Readiness_ShowsTheSentenceOffersSettingsAndHidesWhenReady()
    {
        // Arrange: the Copilot that is not signed in is configured
        var agent = new ScriptedAgentService
        {
            Readiness = new AgentReadinessReport(
                AgentReadinessStatus.CopilotNotSignedIn,
                CopilotRuntimeStarter.NotSignedInMessage),
        };
        using var harness = new ChatHarness(agent: agent);
        var requests = 0;
        harness.Chat.OpenSettingsRequested += (_, _) => requests++;

        // Assert: the check was made when the panel was shown, and the banner says what to do
        Assert.True(harness.WaitFor(() => harness.Banner.IsEffectivelyVisible));
        Assert.Equal(CopilotRuntimeStarter.NotSignedInMessage, harness.Find<SelectableTextBlock>("ChatReadinessMessage").Text);

        // Act: choose "Open Settings" in the banner
        SettingsUiHelpers.Click(harness.Window, harness.Find<Button>("ChatReadinessOpenSettingsButton"));

        // Assert: the shell was asked to open the settings
        Assert.Equal(1, requests);

        // Act: the settings are fixed and the user checks again
        agent.Readiness = new AgentReadinessReport(AgentReadinessStatus.Ready, "Ready.");
        var calls = agent.ReadinessCalls;
        SettingsUiHelpers.Click(harness.Window, harness.Find<Button>("ChatReadinessCheckAgainButton"));

        // Assert: the check was repeated and the banner is gone
        Assert.True(harness.WaitFor(() => !harness.Banner.IsEffectivelyVisible));
        Assert.Equal(calls + 1, agent.ReadinessCalls);
    }

    /// <summary>
    ///     Test that a panel with no copilot shows no banner, no indicator, and no Stop button.
    /// </summary>
    [AvaloniaFact]
    public void ChatPanelView_NoCopilot_ShowsNoBannerIndicatorOrStop()
    {
        // Arrange / Act: a panel with no copilot
        using var harness = new ChatHarness();

        // Assert: nothing of the copilot is shown
        Assert.False(harness.Banner.IsEffectivelyVisible);
        Assert.False(harness.BusyRow.IsEffectivelyVisible);
        Assert.False(harness.StopButton.IsEffectivelyVisible);
    }
}
