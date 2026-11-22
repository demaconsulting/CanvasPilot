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
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Makes a function of the AI library out of a plain tool description (<see cref="KnowledgeTool" />): the name and
///     the words of the description are kept, the JSON Schema of the arguments is the one the description holds, and a
///     call turns the arguments the model sent into the JSON text that the description's function reads.
/// </summary>
/// <remarks>
///     <para>
///         A model sends the arguments of a call in whatever form its provider uses (JSON elements from a streamed call,
///         plain values from a provider library), so the function writes them all out with one serializer and the
///         description's function always reads JSON. The answer goes back as text, unchanged.
///     </para>
///     <para>
///         A call never throws: a failure of the description's function, or arguments that cannot be written as JSON,
///         is answered with a sentence that names the tool, because a model corrects itself from an answer and not from
///         an exception. The one exception that is let through is the cancellation of the turn. The text of an
///         exception is not repeated (it can hold a path); only its type name is. The function holds no state, so it is
///         safe for concurrent use as long as the description's function is.
///     </para>
/// </remarks>
internal sealed class DescriptorFunction : AIFunction
{
    /// <summary>
    ///     The tool that this function answers for.
    /// </summary>
    private readonly KnowledgeTool _tool;

    /// <summary>
    ///     The JSON Schema of the arguments, parsed once.
    /// </summary>
    private readonly JsonElement _schema;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DescriptorFunction" /> class.
    /// </summary>
    /// <param name="tool">The tool description.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="tool" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the name, the description, or the function of the tool is missing, or when the schema is not a JSON
    ///     object.
    /// </exception>
    public DescriptorFunction(KnowledgeTool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);

        // A tool without a name, words, or a function cannot be offered to a model
        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            throw new ArgumentException("A tool must have a name.", nameof(tool));
        }

        if (string.IsNullOrWhiteSpace(tool.Description))
        {
            throw new ArgumentException($"The tool '{tool.Name}' must have a description.", nameof(tool));
        }

        if (tool.Invoke is null)
        {
            throw new ArgumentException($"The tool '{tool.Name}' must have a function that answers a call.", nameof(tool));
        }

        _tool = tool;
        _schema = ParseSchema(tool);
    }

    /// <inheritdoc />
    public override string Name => _tool.Name;

    /// <inheritdoc />
    public override string Description => _tool.Description;

    /// <inheritdoc />
    public override JsonElement JsonSchema => _schema;

    /// <summary>
    ///     Writes the arguments of a call as the JSON text that the function of a tool description reads.
    /// </summary>
    /// <param name="arguments">The arguments the model sent; <see langword="null" /> means none.</param>
    /// <returns>A JSON object text; <c>{}</c> when there are no arguments.</returns>
    /// <exception cref="NotSupportedException">Thrown when a value cannot be written as JSON.</exception>
    internal static string ToArgumentsJson(IEnumerable<KeyValuePair<string, object?>>? arguments)
    {
        // No arguments is an empty object, which every description's function understands as "the default answer"
        if (arguments is null)
        {
            return "{}";
        }

        return JsonSerializer.Serialize(arguments.ToDictionary(pair => pair.Key, pair => pair.Value), AIJsonUtilities.DefaultOptions);
    }

    /// <inheritdoc />
    protected override async ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        // A stopped turn stops the call before the function runs
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            // The function of the description is plain synchronous code that can take a while (the style check), so it
            // runs on a pool thread and does not hold the thread of the turn
            var json = ToArgumentsJson(arguments);
            var answer = await Task.Run(() => _tool.Invoke(json), cancellationToken).ConfigureAwait(false);
            return ToolResult.Text(answer ?? string.Empty);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The turn was stopped: that is not a failure of the tool, so it is not turned into an answer
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Only the type of the exception is named, because its text can hold a path of the user's computer
            return ToolResult.Text($"The tool '{_tool.Name}' could not answer ({ex.GetType().Name}).");
        }
    }

    /// <summary>
    ///     Reads the JSON Schema of a tool description.
    /// </summary>
    /// <param name="tool">The tool description.</param>
    /// <returns>A detached copy of the root of the schema, which is a JSON object.</returns>
    /// <exception cref="ArgumentException">Thrown when the schema is not JSON or is not a JSON object.</exception>
    private static JsonElement ParseSchema(KnowledgeTool tool)
    {
        try
        {
            using var document = JsonDocument.Parse(tool.ParametersSchema ?? string.Empty);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new ArgumentException($"The argument schema of the tool '{tool.Name}' must be a JSON object.", nameof(tool));
            }

            // The document is disposed with this method, so the schema is cloned out of it
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new ArgumentException($"The argument schema of the tool '{tool.Name}' is not valid JSON.", nameof(tool), ex);
        }
    }
}
