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

using System.Collections.Frozen;
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     Builds the tools that the copilot may use for one project: confines them to the project folder with a path
///     policy, offers the built-in packs (the drawing knowledge and the style tools), adds the packs that the
///     application supplies per project (the project tools), adds the optional packs that the user chose,
///     and checks that the set is one that a model and its provider can be given.
/// </summary>
/// <remarks>
///     <para>
///         <b>Confinement.</b> The path policy has the project folder as its working directory and one read-write grant on
///         it that denies the application's own data folder (<c>.canvaspilot</c>) and every project file
///         (<c>*.canvaspilot</c>), so no tool can read or write them. The policy judges a path by its normalized spelling
///         and does not follow links (the S60 finding, pinned by a test of the AI library): a folder link inside the
///         project that leads outside it is permitted by the policy, so the project tools must refuse links themselves.
///     </para>
///     <para>
///         <b>Set check.</b> A set is refused, with an <see cref="InvalidOperationException" /> that names every problem,
///         when it has no tool, when two tools have the same name, when a name is one of the built-in tools of the Copilot
///         CLI (<see cref="CopilotBuiltInToolNames" />, which the CLI would silently take over), or when two packs have
///         the same prefix. (The AI library already refuses a tool whose name does not start with the prefix of its pack,
///         so a repeat between two packs cannot happen; the check stays as the last defense for a repeat inside one pack.)
///     </para>
///     <para>
///         <b>Optional packs.</b> The catalog of optional packs is empty in this version. It is a dictionary from a stable
///         identifier to an <see cref="OptionalPackEntry" />, and the identifiers the user chose are given to
///         <see cref="CreateTools" />, so that a later pack is one catalog entry and nothing else changes.
///     </para>
///     <para>
///         The registry holds only what it is given (the descriptions, the pack suppliers, the catalog) and is safe for
///         concurrent use as long as they are.
///     </para>
/// </remarks>
public sealed class AgentToolRegistry
{
    /// <summary>
    ///     The family prefix of the pack with the drawing knowledge.
    /// </summary>
    public const string CanvasNetPrefix = "canvasnet";

    /// <summary>
    ///     The family prefix of the pack with the style tools.
    /// </summary>
    public const string StylePrefix = "style";

    /// <summary>
    ///     The name pattern that hides the project file of the project from every tool.
    /// </summary>
    internal const string ProjectFilePattern = "*" + ProjectDocument.FileExtension;

    /// <summary>
    ///     The names of the tools that the Copilot CLI has built in. A tool of the application with one of these names would
    ///     be taken over by the CLI, so the registry refuses them. The list is the one of the AI library
    ///     (<c>CopilotAgentFactory.ReservedBuiltInToolNames</c> of version 0.1.0-beta.3), which is not public; a test
    ///     compares it with the library so that an upgrade that adds a name is noticed.
    /// </summary>
    public static readonly IReadOnlySet<string> CopilotBuiltInToolNames = new[]
    {
        "report_intent", "bash", "apply_patch", "task", "web_fetch", "read_bash", "list_powershell", "glob", "ask_user",
        "list_bash", "stop_powershell", "task_complete", "skill", "read_powershell", "grep", "write_powershell",
        "context_board", "edit", "view", "read_agent", "list_agents", "send_inbox", "exit_plan_mode", "powershell",
        "update_todo", "web_search", "write_agent", "str_replace_editor", "create", "stop_bash", "image_generation",
        "write_bash",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    ///     The descriptions of the drawing knowledge tools.
    /// </summary>
    private readonly IReadOnlyList<KnowledgeTool> _knowledgeTools;

    /// <summary>
    ///     The descriptions of the style tools.
    /// </summary>
    private readonly IReadOnlyList<KnowledgeTool> _styleTools;

    /// <summary>
    ///     Supplies the packs that depend on the project, or <see langword="null" /> when there are none.
    /// </summary>
    private readonly Func<AgentToolContext, IEnumerable<IToolPack>>? _projectPacks;

    /// <summary>
    ///     The catalog of optional packs.
    /// </summary>
    private readonly IReadOnlyDictionary<string, OptionalPackEntry> _optionalPacks;

    /// <summary>
    ///     The ceilings that the tools observe.
    /// </summary>
    private readonly ToolLimits _limits;

    /// <summary>
    ///     The abilities of the model and provider that the optional packs may need.
    /// </summary>
    private readonly HostCapabilities _hostCapabilities;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AgentToolRegistry" /> class.
    /// </summary>
    /// <param name="knowledgeTools">The descriptions of the drawing knowledge tools (<c>canvasnet_*</c>).</param>
    /// <param name="styleTools">The descriptions of the style tools (<c>style_*</c>).</param>
    /// <param name="projectPacks">
    ///     Supplies the packs that depend on the project (the project tools); <see langword="null" /> for
    ///     none.
    /// </param>
    /// <param name="optionalPacks">The catalog of optional packs; <see langword="null" /> for the empty catalog.</param>
    /// <param name="limits">The ceilings for the tools; <see langword="null" /> for the defaults of the AI library.</param>
    /// <param name="hostCapabilities">The abilities of the model and provider; none by default.</param>
    /// <exception cref="ArgumentNullException">Thrown when a list of descriptions is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when a description or a pack of the built-in kind cannot be used.</exception>
    public AgentToolRegistry(
        IEnumerable<KnowledgeTool> knowledgeTools,
        IEnumerable<KnowledgeTool> styleTools,
        Func<AgentToolContext, IEnumerable<IToolPack>>? projectPacks = null,
        IReadOnlyDictionary<string, OptionalPackEntry>? optionalPacks = null,
        ToolLimits? limits = null,
        HostCapabilities hostCapabilities = HostCapabilities.None)
    {
        ArgumentNullException.ThrowIfNull(knowledgeTools);
        ArgumentNullException.ThrowIfNull(styleTools);

        _knowledgeTools = [.. knowledgeTools];
        _styleTools = [.. styleTools];
        _projectPacks = projectPacks;
        _optionalPacks = optionalPacks ?? new Dictionary<string, OptionalPackEntry>();
        _limits = limits ?? ToolLimits.Default;
        _hostCapabilities = hostCapabilities;

        // Check the built-in descriptions now, so that a mistake is found when the application starts
        _ = new DescriptorToolPack(CanvasNetPrefix, _knowledgeTools);
        _ = new DescriptorToolPack(StylePrefix, _styleTools);
    }

    /// <summary>
    ///     Gets the identifiers of the optional packs that can be chosen, in order; empty in this version.
    /// </summary>
    public IReadOnlyList<string> OptionalPackIds => [.. _optionalPacks.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    ///     Gets the optional packs that can be chosen, by identifier.
    /// </summary>
    public IReadOnlyDictionary<string, OptionalPackEntry> OptionalPacks => _optionalPacks;

    /// <summary>
    ///     Creates the path policy that confines the tools to a project folder: the folder is the working directory and the
    ///     one grant, for reading and writing, with the application's data folder and the project files denied.
    /// </summary>
    /// <remarks>
    ///     The policy does not follow links, so a link inside the project that leads outside it is permitted by the policy;
    ///     the tools that read or write files must check for links themselves.
    /// </remarks>
    /// <param name="projectFolder">The full path of the project folder.</param>
    /// <param name="limits">The ceilings for the tools; <see langword="null" /> for the defaults of the AI library.</param>
    /// <returns>The policy.</returns>
    /// <exception cref="ArgumentException">Thrown when the folder is empty or is not a full path.</exception>
    public static PathPolicy CreatePolicy(string projectFolder, ToolLimits? limits = null)
    {
        // A relative or empty folder would anchor the tools to whatever the current directory happens to be
        if (string.IsNullOrWhiteSpace(projectFolder) || !Path.IsPathFullyQualified(projectFolder))
        {
            throw new ArgumentException("The project folder must be a full path.", nameof(projectFolder));
        }

        // The folder is granted without a trailing separator, and its own data folder and project files are denied
        var folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectFolder));
        var grant = PathRule.ReadWrite(folder, [ProjectDocument.DataFolderName, ProjectFilePattern]);
        return new PathPolicy(folder, [grant], limits ?? ToolLimits.Default);
    }

    /// <summary>
    ///     Creates the tools for a project.
    /// </summary>
    /// <param name="context">The project that the tools are built for.</param>
    /// <param name="optionalPackIds">The identifiers of the optional packs the user chose; may be empty.</param>
    /// <returns>The tools, in the order of their packs: drawing knowledge, style, project packs, optional packs.</returns>
    /// <exception cref="ArgumentNullException">Thrown when an argument is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">
    ///     Thrown when the project folder is not a full path or an identifier is not in the catalog.
    /// </exception>
    /// <exception cref="InvalidOperationException">Thrown when the set of tools is not valid (see the class remarks).</exception>
    public IReadOnlyList<AIFunction> CreateTools(AgentToolContext context, IReadOnlySet<string> optionalPackIds)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(optionalPackIds);

        // An identifier that is not in the catalog is a mistake of the caller, named with the ones that are known
        var unknown = optionalPackIds.Where(id => !_optionalPacks.ContainsKey(id)).Order(StringComparer.Ordinal).ToList();
        if (unknown.Count > 0)
        {
            var known = _optionalPacks.Count == 0 ? "none" : string.Join(", ", OptionalPackIds);
            throw new ArgumentException(
                $"The optional tool pack '{string.Join("', '", unknown)}' does not exist (known: {known}).",
                nameof(optionalPackIds));
        }

        // The policy comes first, because every pack is given the same one
        var policy = CreatePolicy(context.ProjectFolder, _limits);

        // The packs, in order: the built-in packs, then the packs of the project, then the optional ones by identifier
        var packs = new List<IToolPack>
        {
            new DescriptorToolPack(CanvasNetPrefix, _knowledgeTools),
            new DescriptorToolPack(StylePrefix, _styleTools),
        };
        if (_projectPacks is not null)
        {
            packs.AddRange(_projectPacks(context));
        }

        packs.AddRange(optionalPackIds.Order(StringComparer.Ordinal).Select(id => _optionalPacks[id].Factory(context)));

        // Two packs with one prefix would have the tool names of one pack in the other's family
        var repeated = packs.GroupBy(pack => pack.FamilyPrefix, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        if (repeated.Count > 0)
        {
            throw new InvalidOperationException($"The tool set is not valid: the prefix '{string.Join("', '", repeated)}' is used by more than one pack.");
        }

        // The library composes the packs under the policy and the abilities of the host
        var builder = new ToolPackBuilder(policy).WithHostCapabilities(_hostCapabilities);
        foreach (var pack in packs)
        {
            builder.Add(pack);
        }

        var tools = builder.Build();
        Validate(tools);
        return tools;
    }

    /// <summary>
    ///     Checks a set of tools and names every problem found.
    /// </summary>
    /// <param name="tools">The tools to check.</param>
    /// <exception cref="InvalidOperationException">Thrown when the set is empty, repeats a name, or uses a built-in name.</exception>
    private static void Validate(IReadOnlyList<AIFunction> tools)
    {
        var problems = new List<string>();

        // A copilot without tools could not draw anything
        if (tools.Count == 0)
        {
            problems.Add("it has no tool");
        }

        // A repeated name is answered by one of the tools only, and which one is not defined
        var repeated = tools.GroupBy(tool => tool.Name, StringComparer.Ordinal).Where(group => group.Count() > 1).Select(group => group.Key).ToList();
        if (repeated.Count > 0)
        {
            problems.Add($"the name '{string.Join("', '", repeated)}' is used by more than one tool");
        }

        // The CLI answers its own tools of these names instead of ours
        var builtIn = tools.Select(tool => tool.Name).Where(CopilotBuiltInToolNames.Contains).Distinct(StringComparer.Ordinal).ToList();
        if (builtIn.Count > 0)
        {
            problems.Add($"the name '{string.Join("', '", builtIn)}' is the name of a built-in tool of the Copilot CLI");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("The tool set is not valid: " + string.Join("; ", problems) + ".");
        }
    }
}
