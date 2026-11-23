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

using System.Net;
using System.Text;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests of the log of a turn of the copilot: the numbers and identifiers that tie its lines together, the model and
///     effort, the start and end of each tool call with the name, the path, and the counts of bytes, the facts of the
///     answer (the length of the text, whether the provider reasoned, the time to the first event and the longest gap, the
///     usage), the error codes, the cancel source, the lifecycle of the provider session with the file where the provider
///     recorded it, and the rule that none of the words of the user, the model, or a file ever reach the log.
/// </summary>
public sealed class AgentLoggingTests
{
    /// <summary>A text that stands for the words of a drawing, which must never reach the log.</summary>
    private const string SentinelContent = "SENTINEL-CONTENT-31ab";

    /// <summary>A text that stands for the words of the model, which must never reach the log.</summary>
    private const string SentinelReply = "SENTINEL-REPLY-72cd";

    /// <summary>
    ///     Reads the log lines that hold a text.
    /// </summary>
    /// <param name="log">The whole log.</param>
    /// <param name="text">What the lines must hold.</param>
    /// <returns>The lines.</returns>
    private static string[] Lines(string log, string text)
    {
        return [.. log.Split('\n').Where(line => line.Contains(text, StringComparison.Ordinal))];
    }

    /// <summary>
    ///     Test that the lines of a whole turn carry the number of the turn and the facts that the log is for: the model and
    ///     the effort, the history count, the start and the end of the call of a tool with its name, path, and counts of
    ///     bytes and its time, the length of the text, the reasoning flag, the times, the usage, and nothing of the content.
    /// </summary>
    [Fact]
    public async Task AgentService_Logging_Turn_CarriesIdsToolFactsAndAnswerFactsWithoutContent()
    {
        // Arrange: a model that talks, writes a file with a path and a content, and answers
        var token = TestContext.Current.CancellationToken;
        var client = new ScriptedChatClient(
            [
                new ChatResponseUpdate(ChatRole.Assistant, SentinelReply),
                new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new FunctionCallContent("w1", "project_write", new Dictionary<string, object?> { ["path"] = "art/flow.txt", ["content"] = SentinelContent })]),
                new ChatResponseUpdate(ChatRole.Assistant, [new UsageContent(new UsageDetails { InputTokenCount = 120, OutputTokenCount = 30 })]),
            ],
            [new ChatResponseUpdate(ChatRole.Assistant, "Done.")]);
        using var rig = new AgentServiceRig(
            client,
            settings: new AiProviderSettings
            {
                Provider = AiProviderKind.Ollama,
                ModelName = "gemma3:12b",
                ReasoningEffort = "high",
                OllamaEndpoint = "http://localhost:11434",
                ContextWindowTokens = 32768,
            });
        var request = new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Draw")) { RestoredHistory = [new AgentHistoryTurn(AgentHistoryRole.User, "earlier")] };

        // Act
        var events = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(request, token), token);
        var log = rig.ReadLog();

        // Assert: the turn line, with the model, the effort, and the history count
        Assert.IsType<AgentEvent.Completed>(events[^1]);
        Assert.Contains("Turn started: turn=1 provider=Ollama model=gemma3:12b effort=high historyTurns=1 digestAdded=True", log, StringComparison.Ordinal);

        // Assert: the start and the end of the call, with the name, the path, the bytes, and the time
        var argBytes = Encoding.UTF8.GetByteCount("art/flow.txt") + Encoding.UTF8.GetByteCount(SentinelContent);
        Assert.Contains($"Tool started: turn=1 call=w1 name=project_write path=art/flow.txt argBytes={argBytes}", log, StringComparison.Ordinal);
        var finished = Assert.Single(Lines(log, "Tool finished: turn=1 call=w1 name=project_write outcome=ok resultBytes="));
        Assert.Contains("hasImage=False milliseconds=", finished, StringComparison.Ordinal);

        // Assert: the end of the turn, with the text length, the reasoning flag, the times, and the usage
        var textChars = SentinelReply.Length + "Done.".Length;
        var ended = Assert.Single(Lines(log, "Turn ended: turn=1 outcome=Completed cancelSource=None"));
        Assert.Contains($"textChars={textChars} reasoning=False usageIn=120 usageOut=30", ended, StringComparison.Ordinal);
        Assert.Contains("firstEventMs=", ended, StringComparison.Ordinal);
        Assert.Contains("maxGapMs=", ended, StringComparison.Ordinal);

        // Assert: nothing of the words reached the log
        Assert.DoesNotContain(SentinelContent, log, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelReply, log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the turn number counts up for the life of the service, and that a turn that is refused because another
    ///     runs does not use a number.
    /// </summary>
    [Fact]
    public async Task AgentService_Logging_TurnNumbers_CountUpForTheLifeOfTheService()
    {
        // Arrange
        var token = TestContext.Current.CancellationToken;
        using var rig = new AgentServiceRig(new ProgrammableChatClient((_, _) => ProgrammableChatClient.Say("ok")));

        // Act
        for (var i = 0; i < 3; i++)
        {
            _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Go")), token), token);
        }

        var log = rig.ReadLog();

        // Assert
        Assert.Contains("Turn started: turn=1 ", log, StringComparison.Ordinal);
        Assert.Contains("Turn started: turn=2 ", log, StringComparison.Ordinal);
        Assert.Contains("Turn ended: turn=3 outcome=Completed", log, StringComparison.Ordinal);
        Assert.DoesNotContain("turn=4", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a provider that reports reasoning is logged as having reasoned, with no word of it.
    /// </summary>
    [Fact]
    public async Task AgentService_Logging_Reasoning_IsAFlagWithoutWords()
    {
        // Arrange: a model that reasons and then answers
        var token = TestContext.Current.CancellationToken;
        var client = new ScriptedChatClient(
        [
            new ChatResponseUpdate(ChatRole.Assistant, [new TextReasoningContent(SentinelReply)]),
            new ChatResponseUpdate(ChatRole.Assistant, "Answer."),
        ]);
        using var rig = new AgentServiceRig(client);

        // Act
        _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Go")), token), token);
        var log = rig.ReadLog();

        // Assert
        Assert.Contains("reasoning=True", log, StringComparison.Ordinal);
        Assert.Contains("usageIn=n/a usageOut=n/a", log, StringComparison.Ordinal);
        Assert.DoesNotContain(SentinelReply, log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the lifecycle of the provider session is logged with its identifier: the run that created it, the runs
    ///     that resumed it, that the handle was released after each, the events file of a turn that failed or was canceled
    ///     (and not of one that completed), and the identifier on the release of the conversation with its reason.
    /// </summary>
    [Fact]
    public async Task AgentService_Logging_ProviderSession_HasIdsRunKindsEventsFileAndReleaseReason()
    {
        // Arrange: a provider that gives its session an identifier at its first run, and a model that answers twice and then fails
        var token = TestContext.Current.CancellationToken;
        var client = new ProgrammableChatClient((number, _) => number switch
        {
            1 => ProgrammableChatClient.Say("one"),
            2 => ProgrammableChatClient.Say("two"),
            _ => ProgrammableChatClient.Fail(new HttpRequestException("The server said no.", null, HttpStatusCode.InternalServerError)),
        });
        using var rig = new AgentServiceRig(client);
        var calls = new Dictionary<AgentSession, int>();
        var eventsFile = Path.Combine(rig.Root, "copilot", "session-state", "sess-1", "events.jsonl");
        rig.Factory.Describe = session =>
        {
            lock (calls)
            {
                var count = calls.GetValueOrDefault(session);
                calls[session] = count + 1;

                // The runtime gives the session its identifier when the first run creates it
                return count == 0 ? AgentSessionInfo.None : new AgentSessionInfo("sess-1", eventsFile);
            }
        };

        // Act: three turns (the third fails), then a reset
        for (var i = 0; i < 3; i++)
        {
            _ = await AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Go")), token), token);
        }

        rig.Service.ResetConversation();
        await AgentServiceRig.WaitUntilAsync(() => rig.ReadLog().Contains("Conversation released", StringComparison.Ordinal), token);
        var log = rig.ReadLog();

        // Assert: created by the first run and resumed by the next, the handle released each time
        Assert.Contains("Provider session: turn=1 id=sess-1 run=created handleReleased=True", log, StringComparison.Ordinal);
        Assert.Contains("Provider session: turn=2 id=sess-1 run=resumed handleReleased=True", log, StringComparison.Ordinal);

        // Assert: the events file only for the turn that failed, with the error code of the failure
        var eventsLine = Assert.Single(Lines(log, "Provider session events:"));
        Assert.Contains($"turn=3 id=sess-1 file={eventsFile}", eventsLine, StringComparison.Ordinal);
        Assert.Contains("Turn failed: provider=Ollama kind=Unexpected source=HttpRequestException turn=3 httpStatus=500", log, StringComparison.Ordinal);
        Assert.Contains("Turn ended: turn=3 outcome=Failed:Unexpected", log, StringComparison.Ordinal);

        // Assert: the release names the reason and the session
        Assert.Contains("Conversation released: provider=Ollama reason=reset session=sess-1", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a canceled turn names the cancel source and the events file, and that the elapsed time of the turn
    ///     and the time to the first event are logged on the clock that the service was given.
    /// </summary>
    [Fact]
    public async Task AgentService_Logging_CanceledTurn_NamesTheSourceAndTheEventsFile()
    {
        // Arrange: a turn that says something and then waits, on a provider with a session identifier
        var token = TestContext.Current.CancellationToken;
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var never = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var client = new ProgrammableChatClient((_, ct) => ProgrammableChatClient.Hang(entered, never.Task, "late", ct));
        using var rig = new AgentServiceRig(client);
        var eventsFile = Path.Combine(rig.Root, "events.jsonl");
        rig.Factory.Describe = _ => new AgentSessionInfo("sess-9", eventsFile);
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var turn = AgentServiceRig.CollectAsync(rig.Service.RunTurnAsync(new AgentTurnRequest(AgentSubsystemTests.CreatePayload("Go")), stop.Token), token);
        await entered.Task.WaitAsync(token);

        // Act
        await stop.CancelAsync();
        _ = await turn.WaitAsync(TimeSpan.FromSeconds(30), token);
        var log = rig.ReadLog();

        // Assert
        Assert.Contains("Turn ended: turn=1 outcome=Canceled cancelSource=User", log, StringComparison.Ordinal);
        Assert.Contains($"Provider session events: turn=1 id=sess-9 file={eventsFile}", log, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a refused tool and a tool that failed are logged with their outcome, that a path with control
    ///     characters stays on one line and is cut when it is long, and that a call without a path says so.
    /// </summary>
    [Fact]
    public async Task AgentEventMapper_Logging_Tools_HaveOutcomesAndSafePaths()
    {
        // Arrange: a log, and calls whose paths are odd
        var folder = Directory.CreateTempSubdirectory("CanvasPilotToolLog-");
        try
        {
            var logger = new RollingFileLogger(folder.FullName);
            var mapper = new AgentEventMapper(AiProviderKind.Ollama, "gemma3", logger, TestContext.Current.CancellationToken, turnNumber: 7);
            var longPath = new string('p', 300);
            static AgentResponseUpdate Update(params AIContent[] contents) => new(ChatRole.Assistant, [.. contents]);

            // Act
            mapper.Map(Update(
                new FunctionCallContent("a", "project_read", new Dictionary<string, object?> { ["path"] = "one\ntwo.svg" }),
                new FunctionCallContent("b", "project_read", new Dictionary<string, object?> { ["path"] = longPath }),
                new FunctionCallContent("c", "style_get", null),
                new FunctionCallContent("d", "project_write", new Dictionary<string, object?> { ["path"] = "d.svg" })));
            mapper.Map(Update(
                new FunctionResultContent("a", "Denied (outside the project)"),
                new FunctionResultContent("b", "ok") { Exception = new InvalidOperationException(SentinelContent) },
                new FunctionResultContent("c", "guidance")));
            RollingFileLogger.Flush();
            var log = await File.ReadAllTextAsync(logger.ActiveFilePath, TestContext.Current.CancellationToken);

            // Assert
            Assert.Contains("Tool started: turn=7 call=a name=project_read path=one two.svg argBytes=", log, StringComparison.Ordinal);
            Assert.Contains($"call=b name=project_read path={new string('p', 120)}... argBytes=300", log, StringComparison.Ordinal);
            Assert.Contains("call=c name=style_get path=(none) argBytes=0", log, StringComparison.Ordinal);
            Assert.Contains("Tool finished: turn=7 call=a name=project_read outcome=refused", log, StringComparison.Ordinal);
            Assert.Contains("Tool finished: turn=7 call=b name=project_read outcome=failed", log, StringComparison.Ordinal);
            Assert.Contains("Tool finished: turn=7 call=c name=style_get outcome=ok resultBytes=8", log, StringComparison.Ordinal);
            Assert.DoesNotContain(SentinelContent, log, StringComparison.Ordinal);
        }
        finally
        {
            folder.Delete(recursive: true);
        }
    }
}
