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

using System.Text;
using System.Text.Json;

namespace DemaConsulting.CanvasPilot.KnowledgeSubsystem;

/// <summary>
///     Describes the tools that give an assistant the drawing knowledge: today one tool, <c>canvasnet_help</c>, that
///     answers with the index of the cards, a card, an example, or the result of a search.
/// </summary>
/// <remarks>
///     <para>
///         The tool is a plain description (<see cref="KnowledgeTool" />) with a function that takes the arguments as a
///         JSON text and gives the answer as text, so it can be handed to the tool mechanism of any provider of a language
///         model without the unit depending on one. The function never throws: an argument that cannot be used, such as
///         text that is not JSON, a value that is not a string, or the name of a card that does not exist, is answered with
///         a message that says what to send instead, because an assistant corrects itself from a message and cannot from
///         an exception. The answers are the same for the same arguments and the same cards, and the function holds no
///         state, so it is safe for concurrent use.
///     </para>
/// </remarks>
public static class KnowledgeTools
{
    /// <summary>
    ///     The name of the tool that gives the drawing knowledge.
    /// </summary>
    public const string HelpToolName = "canvasnet_help";

    /// <summary>
    ///     What the tool is for and how to call it.
    /// </summary>
    internal const string HelpToolDescription =
        "Reference for drawing with CanvasNet (SVG, Mermaid, PlantUML, OpenSCAD, Vega-Lite, and how to compose them) "
        + "and for working in a CanvasPilot project. Call it with no arguments to get the index of the cards. "
        + "Use topic to read a card, example to get the text of an example, and query to search the cards by words.";

    /// <summary>
    ///     The JSON Schema of the arguments of the tool.
    /// </summary>
    internal const string HelpParametersSchema =
        """
        {"type":"object","properties":{"topic":{"type":"string","description":"Card id or path such as svg/framed-figure; names and aliases work, case does not matter."},"example":{"type":"string","description":"Example name, or card-id#name when several cards have an example of that name. With topic, only that card and the cards below it are searched."},"query":{"type":"string","description":"Words to search for in titles, summaries, tags, aliases, and ids."}},"additionalProperties":false}
        """;

    /// <summary>
    ///     The most cards that are suggested when a topic or an example is not found.
    /// </summary>
    private const int SuggestionCount = 5;

    /// <summary>
    ///     The most characters of an argument that an answer repeats.
    /// </summary>
    private const int EchoLimit = 80;

    /// <summary>
    ///     The line that ends the answers that do not find a card.
    /// </summary>
    private const string IndexHint = "Call with no arguments for the full index.";

    /// <summary>
    ///     Creates the tool that gives the drawing knowledge.
    /// </summary>
    /// <param name="catalog">The catalog that holds the cards.</param>
    /// <param name="search">The search over the same cards.</param>
    /// <param name="prompts">The writer of the texts of the answers, over the same cards.</param>
    /// <returns>The tool.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    public static KnowledgeTool CreateHelpTool(IKnowledgeCatalog catalog, KnowledgeSearch search, KnowledgePromptBuilder prompts)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(prompts);

        return new KnowledgeTool(
            HelpToolName,
            HelpToolDescription,
            HelpParametersSchema,
            argumentsJson => Answer(argumentsJson, catalog, search, prompts));
    }

    /// <summary>
    ///     Creates every tool that gives the drawing knowledge, with the search and the writer of texts made from the
    ///     catalog.
    /// </summary>
    /// <param name="catalog">The catalog that holds the cards.</param>
    /// <returns>The tools; today only <c>canvasnet_help</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog" /> is <see langword="null" />.</exception>
    public static IReadOnlyList<KnowledgeTool> CreateAll(IKnowledgeCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return [CreateHelpTool(catalog, new KnowledgeSearch(catalog), new KnowledgePromptBuilder(catalog))];
    }

    /// <summary>
    ///     Answers a call of the tool; never throws.
    /// </summary>
    /// <param name="argumentsJson">The arguments as a JSON text, or nothing for the index.</param>
    /// <param name="catalog">The catalog that holds the cards.</param>
    /// <param name="search">The search over the cards.</param>
    /// <param name="prompts">The writer of the texts.</param>
    /// <returns>The answer.</returns>
    private static string Answer(string? argumentsJson, IKnowledgeCatalog catalog, KnowledgeSearch search, KnowledgePromptBuilder prompts)
    {
        try
        {
            if (!TryReadArguments(argumentsJson, out var topic, out var example, out var query, out var error))
            {
                return error;
            }

            if (example is not null)
            {
                return AnswerExample(example, topic, catalog, search);
            }

            if (topic is not null)
            {
                return AnswerTopic(topic, catalog, search, prompts);
            }

            return query is not null ? AnswerQuery(query, search) : prompts.BuildIndex();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // The tool answers an assistant, which can recover from a message but not from an exception
            return Error("the tool could not answer (" + ex.GetType().Name + ")");
        }
    }

    /// <summary>
    ///     Reads the arguments of a call.
    /// </summary>
    /// <param name="argumentsJson">The arguments as a JSON text.</param>
    /// <param name="topic">The topic, or <see langword="null" /> when there is none.</param>
    /// <param name="example">The example, or <see langword="null" /> when there is none.</param>
    /// <param name="query">The query, or <see langword="null" /> when there is none.</param>
    /// <param name="error">The message to answer when the arguments cannot be used.</param>
    /// <returns><see langword="true" /> when the arguments can be used; otherwise <see langword="false" />.</returns>
    private static bool TryReadArguments(
        string? argumentsJson,
        out string? topic,
        out string? example,
        out string? query,
        out string error)
    {
        topic = null;
        example = null;
        query = null;
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return true;
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (root.ValueKind != JsonValueKind.Object)
            {
                error = Error("the arguments must be a JSON object");
                return false;
            }

            // A property that is not a string is refused; an empty string or null is the same as no property
            var values = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var name in new[] { "topic", "example", "query" })
            {
                if (!root.TryGetProperty(name, out var property) || property.ValueKind == JsonValueKind.Null)
                {
                    continue;
                }

                if (property.ValueKind != JsonValueKind.String)
                {
                    error = Error($"'{name}' must be a string");
                    return false;
                }

                var text = property.GetString()?.Trim();
                values[name] = string.IsNullOrEmpty(text) ? null : text;
            }

            topic = values.GetValueOrDefault("topic");
            example = values.GetValueOrDefault("example");
            query = values.GetValueOrDefault("query");
            return true;
        }
        catch (JsonException)
        {
            error = Error("the arguments are not valid JSON");
            return false;
        }
    }

    /// <summary>
    ///     Answers a request for a card.
    /// </summary>
    /// <param name="topic">The identifier or path of the card.</param>
    /// <param name="catalog">The catalog that holds the cards.</param>
    /// <param name="search">The search over the cards.</param>
    /// <param name="prompts">The writer of the texts.</param>
    /// <returns>The card, or the cards that are closest to the topic.</returns>
    private static string AnswerTopic(string topic, IKnowledgeCatalog catalog, KnowledgeSearch search, KnowledgePromptBuilder prompts)
    {
        var card = catalog.Find(topic);
        if (card is null)
        {
            return NoMatch("card", topic, search);
        }

        var text = prompts.BuildCard(card);
        return card.Examples.Count == 0
            ? text
            : text + $"\n\nTo read the text of an example, call {HelpToolName} with example set to its name, or to {card.Id}#name.";
    }

    /// <summary>
    ///     Answers a request for an example.
    /// </summary>
    /// <param name="example">The name of the example, or <c>card-id#name</c>.</param>
    /// <param name="topic">The card to look in, or <see langword="null" /> to look in every card.</param>
    /// <param name="catalog">The catalog that holds the cards.</param>
    /// <param name="search">The search over the cards.</param>
    /// <returns>The example, the choices when there are several, or the cards that are closest to the name.</returns>
    private static string AnswerExample(string example, string? topic, IKnowledgeCatalog catalog, KnowledgeSearch search)
    {
        string? scope = null;
        if (topic is not null)
        {
            var scopeCard = catalog.Find(topic);
            if (scopeCard is null)
            {
                return NoMatch("card", topic, search);
            }

            scope = scopeCard.Id;
        }

        var found = catalog.FindExamples(example, scope);
        switch (found.Count)
        {
            case 0:
                return NoMatch("example", example, search);

            case 1:
                return KnowledgePromptBuilder.BuildExample(found[0].CardId, found[0].Example);

            default:
                var builder = new StringBuilder();
                builder.Append("Several cards have an example named '").Append(Echo(example)).Append("'. Ask again with one of:");
                foreach (var reference in found)
                {
                    builder.Append("\n- ").Append(reference.CardId).Append('#').Append(reference.Example.Name)
                        .Append(": ").Append(reference.Example.Description);
                }

                return builder.ToString();
        }
    }

    /// <summary>
    ///     Answers a search.
    /// </summary>
    /// <param name="query">The words to search for.</param>
    /// <param name="search">The search over the cards.</param>
    /// <returns>The cards that were found, or a message that none matches.</returns>
    private static string AnswerQuery(string query, KnowledgeSearch search)
    {
        var results = search.Search(query);
        return results.Count == 0
            ? $"No card matches '{Echo(query)}'. {IndexHint}"
            : KnowledgePromptBuilder.BuildSearchResults(results);
    }

    /// <summary>
    ///     Writes the answer for a card or an example that does not exist: what was not found and the cards that fit the
    ///     words best.
    /// </summary>
    /// <param name="kind">What was asked for: <c>card</c> or <c>example</c>.</param>
    /// <param name="text">What was asked for, as the caller wrote it.</param>
    /// <param name="search">The search over the cards.</param>
    /// <returns>The message.</returns>
    private static string NoMatch(string kind, string text, KnowledgeSearch search)
    {
        var closest = search.Search(text, SuggestionCount);
        var message = kind == "card"
            ? $"No card matches '{Echo(text)}'."
            : $"No example is named '{Echo(text)}'.";
        return closest.Count == 0
            ? $"{message} {IndexHint}"
            : $"{message} Closest cards:\n{KnowledgePromptBuilder.BuildSearchResults(closest).Split('\n', 2)[1]}\n{IndexHint}";
    }

    /// <summary>
    ///     Writes a message for arguments that cannot be used.
    /// </summary>
    /// <param name="reason">What is wrong.</param>
    /// <returns>The message, which names the three arguments.</returns>
    private static string Error(string reason)
    {
        return $"Error: {reason}. Call {HelpToolName} with no arguments for the index, or with a JSON object that has "
               + "the string arguments topic (a card), example (an example), or query (words to search for).";
    }

    /// <summary>
    ///     Shortens a text that an answer repeats, so that a long argument is not copied into the answer.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, cut to <see cref="EchoLimit" /> characters with an ellipsis when it is longer.</returns>
    private static string Echo(string text)
    {
        return text.Length <= EchoLimit ? text : text[..EchoLimit] + "...";
    }
}
