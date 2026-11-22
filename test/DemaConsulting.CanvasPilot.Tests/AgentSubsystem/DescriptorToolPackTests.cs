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

using System.Text.Json;
using System.Text.Json.Nodes;
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="DescriptorToolPack" /> and the function it makes of a tool description: that the schema
///     and the words of a description come out as the function of the AI library, that a call writes its arguments as the
///     JSON text the description reads, that a failure is an answer and not an exception, and that a pack refuses
///     descriptions the model could not be given.
/// </summary>
public sealed class DescriptorToolPackTests : IDisposable
{
    /// <summary>
    ///     The folder that the policy of a test is made for.
    /// </summary>
    private readonly string _folder = Directory.CreateTempSubdirectory("canvaspilot-descriptor-tests-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        Directory.Delete(_folder, true);
    }

    /// <summary>
    ///     Test that the schema of a description comes out unchanged as the schema of the function, before and after the
    ///     function is wrapped with the limits of the AI library, and that the name and the words do too.
    /// </summary>
    [Fact]
    public void DescriptorToolPack_CreateTools_SchemaAndWordsOfTheDescription_ComeOutAsTheFunction()
    {
        // Arrange: a description with a schema that has a nested description, and a pack for it
        var descriptor = ToolTestDoubles.Describe("demo_echo", _ => "ok") with { Description = "Echoes the text it is given." };
        var pack = new DescriptorToolPack("demo", [descriptor]);

        // Act: make the function of the pack, and a bare one for the unwrapped view
        var tool = Assert.Single(pack.CreateTools(Policy()));
        var bare = new DescriptorFunction(descriptor);

        // Assert: the schema is the same JSON as the text of the description, for both the bare and the guarded function
        var expected = JsonNode.Parse(ToolTestDoubles.TextSchema);
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(bare.JsonSchema.GetRawText())));
        Assert.True(JsonNode.DeepEquals(expected, JsonNode.Parse(tool.JsonSchema.GetRawText())));
        Assert.Equal("demo_echo", tool.Name);
        Assert.Equal("Echoes the text it is given.", tool.Description);
        Assert.Equal("demo", pack.FamilyPrefix);
        Assert.Equal(HostCapabilities.None, pack.RequiredCapabilities);
    }

    /// <summary>
    ///     Test that a call whose arguments arrive as JSON elements, as a streamed call does, gives the function of the
    ///     description the same JSON, and that the answer comes back as the text the function gave.
    /// </summary>
    [Fact]
    public async Task DescriptorToolPack_Invoke_JsonElementArguments_ReachTheDescriptionAsJsonAndTheAnswerComesBack()
    {
        // Arrange: a description that records its calls and a function made of it
        var calls = new List<string>();
        var tool = Assert.Single(new DescriptorToolPack("demo", [ToolTestDoubles.Recording("demo_echo", calls, "the answer")]).CreateTools(Policy()));
        using var document = JsonDocument.Parse("""{"text":"hello \"quoted\" é"}""");
        var arguments = new AIFunctionArguments { ["text"] = document.RootElement.GetProperty("text").Clone() };

        // Act: call the function
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert: the description got one call with the argument as JSON, and the answer is its text
        var call = Assert.Single(calls);
        Assert.Equal("hello \"quoted\" é", ToolTestDoubles.Parse(call)["text"].GetString());
        Assert.Contains("the answer", ResultText(result), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a call whose arguments arrive as plain values, as a provider library delivers them, is written as the
    ///     JSON of the values: a string, a number, a boolean, and a list.
    /// </summary>
    [Fact]
    public async Task DescriptorToolPack_Invoke_PlainValueArguments_AreWrittenAsJson()
    {
        // Arrange: a function over a recording description
        var calls = new List<string>();
        var tool = Assert.Single(new DescriptorToolPack("demo", [ToolTestDoubles.Recording("demo_echo", calls)]).CreateTools(Policy()));
        var arguments = new AIFunctionArguments
        {
            ["text"] = "abc",
            ["count"] = 3,
            ["flag"] = true,
            ["items"] = new[] { "a", "b" },
        };

        // Act: call the function
        await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert: each value is JSON of its own kind
        var parsed = ToolTestDoubles.Parse(Assert.Single(calls));
        Assert.Equal("abc", parsed["text"].GetString());
        Assert.Equal(3, parsed["count"].GetInt32());
        Assert.True(parsed["flag"].GetBoolean());
        Assert.Equal(["a", "b"], parsed["items"].EnumerateArray().Select(item => item.GetString()!).ToArray());
    }

    /// <summary>
    ///     Test that a call without arguments gives the description an empty JSON object, which is how the tools of the
    ///     application recognize "no arguments".
    /// </summary>
    [Fact]
    public async Task DescriptorToolPack_Invoke_NoArguments_GivesAnEmptyJsonObject()
    {
        // Arrange: a function over a recording description
        var calls = new List<string>();
        var tool = Assert.Single(new DescriptorToolPack("demo", [ToolTestDoubles.Recording("demo_echo", calls)]).CreateTools(Policy()));

        // Act: call it with no arguments at all
        await tool.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        // Assert: the description was given {} and nothing else
        Assert.Equal("{}", Assert.Single(calls));
    }

    /// <summary>
    ///     Test that a function of a description that throws is answered with a sentence that names the tool and the type
    ///     of the exception, and does not repeat the text of the exception.
    /// </summary>
    [Fact]
    public async Task DescriptorToolPack_Invoke_DescriptionThrows_IsAnAnswerThatNamesToolAndTypeOnly()
    {
        // Arrange: a description whose function throws an exception with a text that must not reach the model
        var tool = Assert.Single(
            new DescriptorToolPack(
                "demo",
                [ToolTestDoubles.Describe("demo_fail", _ => throw new InvalidOperationException(@"C:\secret\path was not found"))]).CreateTools(Policy()));

        // Act: call it
        var result = await tool.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        // Assert: the answer is a sentence with the tool and the exception type, without the text of the exception
        var text = ResultText(result);
        Assert.Contains("demo_fail", text, StringComparison.Ordinal);
        Assert.Contains(nameof(InvalidOperationException), text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that arguments that cannot be written as JSON are answered with a sentence and that the function of the
    ///     description is not called.
    /// </summary>
    [Fact]
    public async Task DescriptorToolPack_Invoke_UnwritableArguments_IsAnAnswerAndTheDescriptionIsNotCalled()
    {
        // Arrange: a recording description and an argument whose value throws when it is written
        var calls = new List<string>();
        var tool = Assert.Single(new DescriptorToolPack("demo", [ToolTestDoubles.Recording("demo_echo", calls)]).CreateTools(Policy()));
        var arguments = new AIFunctionArguments { ["text"] = new ToolTestDoubles.Unwritable() };

        // Act: call the function
        var result = await tool.InvokeAsync(arguments, TestContext.Current.CancellationToken);

        // Assert: an answer that names the tool, no call of the description, and no text of the exception
        var text = ResultText(result);
        Assert.Contains("demo_echo", text, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", text, StringComparison.Ordinal);
        Assert.Empty(calls);
    }

    /// <summary>
    ///     Test that a function of a description that answers nothing gives an empty answer and not an exception.
    /// </summary>
    [Fact]
    public async Task DescriptorToolPack_Invoke_DescriptionAnswersNull_IsAnEmptyAnswer()
    {
        // Arrange: a description whose function answers null although it should not
        var tool = Assert.Single(new DescriptorToolPack("demo", [ToolTestDoubles.Describe("demo_null", _ => null!)]).CreateTools(Policy()));

        // Act: call it
        var result = await tool.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);

        // Assert: the answer is empty text
        Assert.Equal(string.Empty, ResultText(result));
    }

    /// <summary>
    ///     Test that a call with a token that is already canceled throws the cancellation, because a stopped turn must stop
    ///     and is not a failure of the tool, and that the description is not called.
    /// </summary>
    [Fact]
    public async Task DescriptorToolPack_Invoke_CanceledToken_ThrowsCancellationAndDoesNotCall()
    {
        // Arrange: a recording description and a token that is canceled
        var calls = new List<string>();
        var tool = Assert.Single(new DescriptorToolPack("demo", [ToolTestDoubles.Recording("demo_echo", calls)]).CreateTools(Policy()));
        using var source = new CancellationTokenSource();
        await source.CancelAsync();

        // Act / Assert: the call throws the cancellation, and the description never ran
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await tool.InvokeAsync(new AIFunctionArguments(), source.Token));
        Assert.Empty(calls);
    }

    /// <summary>
    ///     Test that the functions of a pack are limited by the ceilings of the policy: an answer longer than the ceiling is
    ///     refused with a message that names the reason, and one within the ceiling is delivered.
    /// </summary>
    [Fact]
    public async Task DescriptorToolPack_Invoke_AnswerOverTheCeiling_IsRefusedByTheLimits()
    {
        // Arrange: a policy that allows only 100 characters per answer, and a description whose answer length is asked for
        var policy = AgentToolRegistry.CreatePolicy(_folder, new ToolLimits(maxResultCharacters: 100));
        var tool = Assert.Single(
            new DescriptorToolPack(
                "demo",
                [ToolTestDoubles.Describe("demo_long", json => new string('x', ToolTestDoubles.Parse(json)["length"].GetInt32()))]).CreateTools(policy));

        // Act: ask for a short answer and for a long one
        var small = await tool.InvokeAsync(new AIFunctionArguments { ["length"] = 10 }, TestContext.Current.CancellationToken);
        var large = await tool.InvokeAsync(new AIFunctionArguments { ["length"] = 5000 }, TestContext.Current.CancellationToken);

        // Assert: the short answer is delivered, and the long one is a refusal that names its reason and has no 5000 x
        Assert.Equal(new string('x', 10), ResultText(small));
        var refused = ResultText(large);
        Assert.Contains("ResourceTooLarge", refused, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('x', 200), refused, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a pack made of the real description of the drawing knowledge gives a function whose schema is the
    ///     schema of the description and that answers the index of the cards when it is called without arguments.
    /// </summary>
    [Fact]
    public async Task DescriptorToolPack_CreateTools_RealKnowledgeDescriptor_RoundTripsSchemaAndAnswer()
    {
        // Arrange: the real description over a small catalog
        var catalog = new FakeKnowledgeCatalog(FakeKnowledgeCatalog.Card("svg", "SVG Basics"));
        var descriptor = Assert.Single(KnowledgeTools.CreateAll(catalog));
        var tool = Assert.Single(new DescriptorToolPack("canvasnet", [descriptor]).CreateTools(Policy()));

        // Act: call it with no arguments, and with a topic
        var index = await tool.InvokeAsync(new AIFunctionArguments(), TestContext.Current.CancellationToken);
        var card = await tool.InvokeAsync(new AIFunctionArguments { ["topic"] = "svg" }, TestContext.Current.CancellationToken);

        // Assert: the schema is the one of the description, the index names the card, and the topic gives the card
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(KnowledgeTools.HelpParametersSchema), JsonNode.Parse(tool.JsonSchema.GetRawText())));
        Assert.Equal(KnowledgeTools.HelpToolName, tool.Name);
        Assert.Contains("svg", ResultText(index), StringComparison.Ordinal);
        Assert.Contains("A body.", ResultText(card), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that a pack refuses a prefix that is missing.
    /// </summary>
    [Fact]
    public void DescriptorToolPack_Constructor_BadPrefixOrNullArguments_AreRefused()
    {
        // Act / Assert: null arguments and an empty prefix are refused
        Assert.Throws<ArgumentNullException>(() => new DescriptorToolPack(null!, []));
        Assert.Throws<ArgumentNullException>(() => new DescriptorToolPack("demo", null!));
        Assert.Throws<ArgumentException>(() => new DescriptorToolPack(" ", []));
    }

    /// <summary>
    ///     Test that a pack refuses a name that does not start with the prefix, a name that breaks the naming rule of the AI
    ///     library, and a name that is used twice.
    /// </summary>
    [Fact]
    public void DescriptorToolPack_Constructor_BadNames_AreRefusedWithTheName()
    {
        // Act / Assert: each mistake names the tool, and a pack with no tool is accepted
        var otherPrefix = Assert.Throws<ArgumentException>(() => new DescriptorToolPack("demo", [ToolTestDoubles.Plain("other_echo")]));
        Assert.Contains("other_echo", otherPrefix.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => new DescriptorToolPack("demo", [ToolTestDoubles.Plain("Demo_Echo")]));
        var twice = Assert.Throws<ArgumentException>(
            () => new DescriptorToolPack("demo", [ToolTestDoubles.Plain("demo_echo"), ToolTestDoubles.Plain("demo_echo")]));
        Assert.Contains("demo_echo", twice.Message, StringComparison.Ordinal);
        Assert.Empty(new DescriptorToolPack("demo", []).CreateTools(Policy()));
    }

    /// <summary>
    ///     Test that a pack refuses a description with a schema that is not JSON, a schema that is not an object, and a
    ///     description that has no words or no function.
    /// </summary>
    [Fact]
    public void DescriptorToolPack_Constructor_UnusableDescriptions_AreRefused()
    {
        // Arrange: a good description to make the bad ones of
        var good = ToolTestDoubles.Plain("demo_echo");

        // Act / Assert: each unusable description is refused with an argument exception that names the tool
        var notJson = Assert.Throws<ArgumentException>(() => new DescriptorToolPack("demo", [good with { ParametersSchema = "{not json" }]));
        Assert.Contains("demo_echo", notJson.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => new DescriptorToolPack("demo", [good with { ParametersSchema = "[1,2]" }]));
        Assert.Throws<ArgumentException>(() => new DescriptorToolPack("demo", [good with { ParametersSchema = string.Empty }]));
        Assert.Throws<ArgumentException>(() => new DescriptorToolPack("demo", [good with { Description = " " }]));
        Assert.Throws<ArgumentException>(() => new DescriptorToolPack("demo", [good with { Invoke = null! }]));
        Assert.Throws<ArgumentException>(() => new DescriptorToolPack("demo", [good with { Name = string.Empty }]));
    }

    /// <summary>
    ///     Test that asking a pack for its tools without a policy is refused.
    /// </summary>
    [Fact]
    public void DescriptorToolPack_CreateTools_NullPolicy_IsRefused()
    {
        // Act / Assert: the policy is needed for the limits
        Assert.Throws<ArgumentNullException>(() => new DescriptorToolPack("demo", []).CreateTools(null!));
    }

    /// <summary>
    ///     Makes the policy of the test folder.
    /// </summary>
    /// <returns>The policy.</returns>
    private PathPolicy Policy()
    {
        return AgentToolRegistry.CreatePolicy(_folder);
    }

    /// <summary>
    ///     Reads the text of the result of a call, whatever shape the AI library gave it.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <returns>The text.</returns>
    private static string ResultText(object? result)
    {
        return result switch
        {
            null => string.Empty,
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString() ?? string.Empty,
            JsonElement element => element.GetRawText(),
            TextContent content => content.Text,
            _ => result.ToString() ?? string.Empty,
        };
    }
}
