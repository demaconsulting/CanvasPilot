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

using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     A tool pack of the AI library that offers plain tool descriptions (<see cref="KnowledgeTool" />) as functions: it
///     is how the drawing knowledge and the style tools, which know nothing of the AI library, are handed to the model.
/// </summary>
/// <remarks>
///     <para>
///         The pack checks its descriptions when it is created, so a mistake shows up at the start of a conversation and
///         not in the middle of a turn: every name must follow the naming rule of the AI library and start with the
///         family prefix of the pack and an underscore, and no name may repeat. Each function is wrapped with the limits
///         of the path policy (<c>AsGuarded</c>), so an answer that is too long is refused by the library and not sent
///         to the model.
///     </para>
///     <para>
///         The descriptions do not read files, so the path policy is used only for its limits. The pack holds no state
///         and is safe for concurrent use.
///     </para>
/// </remarks>
public sealed class DescriptorToolPack : IToolPack
{
    /// <summary>
    ///     The tools that the pack offers.
    /// </summary>
    private readonly IReadOnlyList<KnowledgeTool> _descriptors;

    /// <summary>
    ///     Initializes a new instance of the <see cref="DescriptorToolPack" /> class.
    /// </summary>
    /// <param name="prefix">The family prefix of the pack, which every tool name starts with, followed by an underscore.</param>
    /// <param name="descriptors">The tool descriptions to offer; may be empty.</param>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the prefix is empty, a name breaks the naming rule or does not start with the prefix, a name
    ///     repeats, or a description cannot be used (see <see cref="DescriptorFunction" />).
    /// </exception>
    public DescriptorToolPack(string prefix, IEnumerable<KnowledgeTool> descriptors)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(descriptors);
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("A tool pack must have a family prefix.", nameof(prefix));
        }

        FamilyPrefix = prefix;
        _descriptors = [.. descriptors];

        // Check every description now: the name rule of the library, the prefix, repeats, and that a function can be made
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var descriptor in _descriptors)
        {
            // A function is made only to prove that the description can be used; it is made again for each project
            _ = new DescriptorFunction(descriptor);
            ToolName.Validate(descriptor.Name);
            if (!descriptor.Name.StartsWith(prefix + "_", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"The tool '{descriptor.Name}' must start with the prefix '{prefix}_' of its pack.",
                    nameof(descriptors));
            }

            if (!seen.Add(descriptor.Name))
            {
                throw new ArgumentException($"The tool name '{descriptor.Name}' is used twice in the pack '{prefix}'.", nameof(descriptors));
            }
        }
    }

    /// <inheritdoc />
    public string FamilyPrefix { get; }

    /// <inheritdoc />
    public HostCapabilities RequiredCapabilities => HostCapabilities.None;

    /// <inheritdoc />
    public IEnumerable<AIFunction> CreateTools(PathPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        // Each description becomes one function, limited by the ceilings of the policy
        return _descriptors.Select(descriptor => new DescriptorFunction(descriptor).AsGuarded(policy.Limits)).ToList();
    }
}
