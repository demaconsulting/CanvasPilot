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

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Test doubles for the tool registry and the descriptor tool pack: tool descriptions that record their calls, a tool
///     pack that records the policy it is given, and a function with a name that the naming rule of the AI library does
///     not see.
/// </summary>
internal static class ToolTestDoubles
{
    /// <summary>
    ///     A small JSON Schema with one string argument.
    /// </summary>
    internal const string TextSchema =
        """{"type":"object","properties":{"text":{"type":"string","description":"Some text."}},"required":["text"],"additionalProperties":false}""";

    /// <summary>
    ///     Makes a tool description whose function records the arguments it is given and answers with a fixed text.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="calls">Receives the JSON text of every call.</param>
    /// <param name="answer">The answer.</param>
    /// <param name="schema">The JSON Schema of the arguments.</param>
    /// <returns>The description.</returns>
    internal static KnowledgeTool Recording(string name, List<string> calls, string answer = "ok", string schema = TextSchema)
    {
        return new KnowledgeTool(
            name,
            "The tool " + name + ".",
            schema,
            json =>
            {
                calls.Add(json);
                return answer;
            });
    }

    /// <summary>
    ///     Makes a tool description with a function.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="invoke">The function that answers a call.</param>
    /// <returns>The description.</returns>
    internal static KnowledgeTool Describe(string name, Func<string, string> invoke)
    {
        return new KnowledgeTool(name, "The tool " + name + ".", TextSchema, invoke);
    }

    /// <summary>
    ///     Makes a tool description that is not used.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <returns>The description.</returns>
    internal static KnowledgeTool Plain(string name)
    {
        return Describe(name, _ => "ok");
    }

    /// <summary>
    ///     Reads the arguments that a recorded call carried, as a dictionary of the JSON values.
    /// </summary>
    /// <param name="json">The JSON text of the call.</param>
    /// <returns>The arguments by name.</returns>
    internal static Dictionary<string, JsonElement> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().ToDictionary(property => property.Name, property => property.Value.Clone());
    }

    /// <summary>
    ///     A tool pack with a fixed prefix and fixed tools, which records the policy it is given.
    /// </summary>
    /// <param name="prefix">The family prefix.</param>
    /// <param name="functions">The functions of the pack.</param>
    /// <param name="required">The capabilities that the pack needs.</param>
    internal sealed class FakePack(string prefix, IReadOnlyList<AIFunction> functions, HostCapabilities required = HostCapabilities.None)
        : IToolPack
    {
        /// <summary>
        ///     Gets the policy that the pack was last given, or <see langword="null" /> when it was never asked.
        /// </summary>
        public PathPolicy? LastPolicy { get; private set; }

        /// <inheritdoc />
        public string FamilyPrefix => prefix;

        /// <inheritdoc />
        public HostCapabilities RequiredCapabilities => required;

        /// <inheritdoc />
        public IEnumerable<AIFunction> CreateTools(PathPolicy policy)
        {
            LastPolicy = policy;
            return functions;
        }
    }

    /// <summary>
    ///     A function with a name that is not checked by the naming rule of the AI library, which a pack of the
    ///     application could hand over by mistake.
    /// </summary>
    /// <param name="name">The name of the function.</param>
    internal sealed class RawFunction(string name) : AIFunction
    {
        /// <inheritdoc />
        public override string Name => name;

        /// <inheritdoc />
        public override string Description => "A raw function.";

        /// <inheritdoc />
        protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
        {
            return ValueTask.FromResult<object?>("ok");
        }
    }

    /// <summary>
    ///     An argument value that cannot be written as JSON.
    /// </summary>
    internal sealed class Unwritable
    {
        /// <summary>
        ///     Gets a value; reading it always throws.
        /// </summary>
        public string Value => throw new InvalidOperationException("secret detail of the failure of " + GetType().Name);
    }
}
