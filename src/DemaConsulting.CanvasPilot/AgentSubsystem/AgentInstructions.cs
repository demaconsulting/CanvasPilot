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
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     How much of the knowledge and style the instructions of the copilot carry, which depends on how much room the model
///     has.
/// </summary>
public enum InstructionTier
{
    /// <summary>The whole index of the drawing knowledge, and the style with the example images of its kit.</summary>
    Full,

    /// <summary>The cards at the top of the index of the drawing knowledge, and the style without example images.</summary>
    Compact,
}

/// <summary>
///     Writes the instructions that the AI copilot is given for one project: the role prompt, the index of the drawing
///     knowledge, the facts of the project, and the style of the project.
/// </summary>
/// <remarks>
///     <para>
///         <b>Parts, in this order.</b> The role prompt (an embedded Markdown resource of at most
///         <see cref="RolePromptBudgetCharacters" /> characters, which does not change from project to project), the index of
///         the drawing knowledge, the facts of the project (name, folder, the head of the list of files, and the name and id
///         of the style kit), and the style of the kit as <see cref="StyleGuidance.Build(StyleKitEntry, bool)" /> writes it.
///         The parts that do not change come first, so that a provider that caches the start of a prompt can reuse it.
///     </para>
///     <para>
///         <b>Tiers.</b> A model with a context window of at least <see cref="FullTierMinimumContextTokens" /> tokens, and
///         every GitHub Copilot model, gets the <see cref="InstructionTier.Full" /> tier. A smaller window gets the
///         <see cref="InstructionTier.Compact" /> tier: the index lists the cards at the top only (the identifiers of the
///         others are found by asking for a card), the style has no example images, and fewer files are listed. The sizes are limits that the tests of the unit measure with the real
///         cards, the real built-in kits, and the real tools: <see cref="FullBudgetCharacters" /> and
///         <see cref="CompactBudgetCharacters" /> for the instructions, and <see cref="ToolSchemaBudgetCharacters" /> for the
///         words and schemas of all tools.
///     </para>
///     <para>
///         The text is the same for the same input, has line feeds as line breaks, and does not depend on the culture. The
///         unit holds no state and is safe for concurrent use. It writes no file and logs nothing.
///     </para>
/// </remarks>
public sealed class AgentInstructions
{
    /// <summary>
    ///     The smallest context window, in tokens, that gets the full tier.
    /// </summary>
    public const int FullTierMinimumContextTokens = AiProviderSettings.RecommendedMinimumContextTokens;

    /// <summary>
    ///     The most characters that the role prompt has.
    /// </summary>
    public const int RolePromptBudgetCharacters = 3_000;

    /// <summary>
    ///     The most characters that the instructions of the full tier have, which is about 5,000 tokens.
    /// </summary>
    public const int FullBudgetCharacters = 20_000;

    /// <summary>
    ///     The most characters that the instructions of the compact tier have, which is about 3,000 tokens.
    /// </summary>
    public const int CompactBudgetCharacters = 11_000;

    /// <summary>
    ///     The most characters that the names, words, and schemas of all the tools of the copilot have together.
    /// </summary>
    public const int ToolSchemaBudgetCharacters = 7_000;

    /// <summary>
    ///     The most files that the full tier lists.
    /// </summary>
    public const int FullListedFiles = 40;

    /// <summary>
    ///     The most files that the compact tier lists.
    /// </summary>
    public const int CompactListedFiles = 15;

    /// <summary>
    ///     The most characters that the paths in the list of files of the full tier have together, so that a project with
    ///     long names cannot crowd out the rest.
    /// </summary>
    public const int FullListedFileCharacters = 1_600;

    /// <summary>
    ///     The most characters that the paths in the list of files of the compact tier have together.
    /// </summary>
    public const int CompactListedFileCharacters = 500;

    /// <summary>
    ///     The most characters of a path or a name that is written; more is cut with an ellipsis.
    /// </summary>
    public const int MaxLineCharacters = 120;

    /// <summary>
    ///     The name of the embedded resource that holds the role prompt.
    /// </summary>
    internal const string RolePromptResourceName = "DemaConsulting.CanvasPilot.Agent.RolePrompt.md";

    /// <summary>
    ///     The role prompt that comes with the application, read once.
    /// </summary>
    private static readonly Lazy<string> EmbeddedRolePrompt = new(ReadEmbeddedRolePrompt);

    /// <summary>
    ///     The role prompt of this instance.
    /// </summary>
    private readonly string _rolePrompt;

    /// <summary>
    ///     Writes the index of the drawing knowledge.
    /// </summary>
    private readonly KnowledgePromptBuilder _prompts;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AgentInstructions" /> class.
    /// </summary>
    /// <param name="prompts">Writes the index of the drawing knowledge.</param>
    /// <param name="rolePrompt">
    ///     The role prompt, or <see langword="null" /> for the one that comes with the application.
    /// </param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="prompts" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">Thrown when the role prompt is empty or longer than <see cref="RolePromptBudgetCharacters" />.</exception>
    public AgentInstructions(KnowledgePromptBuilder prompts, string? rolePrompt = null)
    {
        ArgumentNullException.ThrowIfNull(prompts);

        var role = (rolePrompt ?? EmbeddedRolePrompt.Value).Replace("\r\n", "\n", StringComparison.Ordinal).Trim();
        if (role.Length == 0 || role.Length > RolePromptBudgetCharacters)
        {
            throw new ArgumentException(
                $"The role prompt must have 1 to {RolePromptBudgetCharacters} characters; it has {role.Length}.",
                nameof(rolePrompt));
        }

        _prompts = prompts;
        _rolePrompt = role;
    }

    /// <summary>
    ///     Gets the role prompt that is used.
    /// </summary>
    public string RolePrompt => _rolePrompt;

    /// <summary>
    ///     Chooses the tier for a provider and a context window.
    /// </summary>
    /// <param name="provider">The AI provider; every GitHub Copilot model gets the full tier.</param>
    /// <param name="contextWindowTokens">The context window of the model, in tokens; it matters for Ollama only.</param>
    /// <returns>
    ///     <see cref="InstructionTier.Full" /> for Copilot and for a window of at least
    ///     <see cref="FullTierMinimumContextTokens" /> tokens; otherwise <see cref="InstructionTier.Compact" />.
    /// </returns>
    public static InstructionTier ChooseTier(AiProviderKind provider, int contextWindowTokens)
    {
        return provider == AiProviderKind.GitHubCopilot || contextWindowTokens >= FullTierMinimumContextTokens
            ? InstructionTier.Full
            : InstructionTier.Compact;
    }

    /// <summary>
    ///     Writes the instructions for a project.
    /// </summary>
    /// <param name="facts">What is known about the open project.</param>
    /// <param name="tier">How much to write.</param>
    /// <returns>The instructions, without a final line feed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="facts" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="tier" /> is not a tier.</exception>
    public string Build(AgentProjectFacts facts, InstructionTier tier)
    {
        ArgumentNullException.ThrowIfNull(facts);
        if (!Enum.IsDefined(tier))
        {
            throw new ArgumentOutOfRangeException(nameof(tier), tier, "The tier is not a known tier.");
        }

        var full = tier == InstructionTier.Full;
        var text = new StringBuilder(_rolePrompt);

        // The drawing knowledge, which is the same for every project, comes before what is special to this one; the
        // compact tier lists the cards at the top only (depth zero), which is enough to ask for a card by its id
        text.Append("\n\n## Drawing knowledge\n");
        text.Append(_prompts.BuildIndex(full ? int.MaxValue : 0));

        text.Append("\n\n## This project\n");
        AppendFacts(text, facts, full ? FullListedFiles : CompactListedFiles, full ? FullListedFileCharacters : CompactListedFileCharacters);

        text.Append("\n\n## Style\n");
        AppendStyle(text, facts, full);

        return text.ToString();
    }

    /// <summary>
    ///     Reads the role prompt that is embedded in the assembly.
    /// </summary>
    /// <returns>The role prompt.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the resource is missing, which is a mistake of the build.</exception>
    private static string ReadEmbeddedRolePrompt()
    {
        using var stream = typeof(AgentInstructions).Assembly.GetManifestResourceStream(RolePromptResourceName)
            ?? throw new InvalidOperationException($"The resource '{RolePromptResourceName}' is not in the assembly.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    ///     Writes the name and folder of the project and the head of its list of files.
    /// </summary>
    /// <param name="text">The text to add to.</param>
    /// <param name="facts">The facts of the project.</param>
    /// <param name="listedFiles">The most files to list.</param>
    /// <param name="listedCharacters">The most characters that the listed paths have together.</param>
    private static void AppendFacts(StringBuilder text, AgentProjectFacts facts, int listedFiles, int listedCharacters)
    {
        text.Append("- Name: ").Append(OneLine(facts.Name)).Append('\n');
        text.Append("- Folder: ").Append(OneLine(facts.Folder)).Append('\n');

        var files = facts.Files.Where(file => !string.IsNullOrWhiteSpace(file)).ToList();
        if (files.Count == 0)
        {
            text.Append("- Files: none yet; create the first one with project_write.");
            return;
        }

        // Take paths from the head of the list while the number and the size allow it; at least one is always listed
        var listed = new List<string>();
        var characters = 0;
        foreach (var file in files.Take(listedFiles))
        {
            var path = OneLine(file.Replace('\\', '/'));
            if (listed.Count > 0 && characters + path.Length > listedCharacters)
            {
                break;
            }

            listed.Add(path);
            characters += path.Length;
        }

        text.Append("- Files (relative to the folder");
        text.Append(files.Count > listed.Count ? $", the first {listed.Count} of {files.Count}; call project_list for the rest" : string.Empty);
        text.Append("):");
        foreach (var path in listed)
        {
            text.Append("\n  - ").Append(path);
        }
    }

    /// <summary>
    ///     Writes the name and id of the style kit and the style itself, or says that the project has no style.
    /// </summary>
    /// <param name="text">The text to add to.</param>
    /// <param name="facts">The facts of the project.</param>
    /// <param name="includeExamples">Whether the style has the example images of its kit.</param>
    private static void AppendStyle(StringBuilder text, AgentProjectFacts facts, bool includeExamples)
    {
        if (facts.ActiveStyle is { } entry)
        {
            text.Append("Active style kit: ").Append(OneLine(entry.Kit.Name)).Append(" (id ").Append(OneLine(entry.Kit.Id)).Append(").\n");
            text.Append(StyleGuidance.Build(entry, includeExamples).TrimEnd('\n'));
        }
        else if (!string.IsNullOrWhiteSpace(facts.ActiveStyleId))
        {
            text.Append("The project names the style kit '").Append(OneLine(facts.ActiveStyleId)).Append("', which cannot be found. ");
            text.Append("Draw without a style and tell the user that the kit is missing.");
        }
        else
        {
            text.Append("The project has no style kit. Draw with a clean, plain look and tell the user that a style kit can be chosen.");
        }
    }

    /// <summary>
    ///     Makes a text fit on one line: every run of white space or control characters becomes one space, and a text longer
    ///     than <see cref="MaxLineCharacters" /> is cut with an ellipsis.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <returns>The line.</returns>
    private static string OneLine(string value)
    {
        var builder = new StringBuilder(value.Length);
        var space = false;
        foreach (var c in value.Trim())
        {
            if (char.IsWhiteSpace(c) || char.IsControl(c))
            {
                space = true;
                continue;
            }

            if (space)
            {
                builder.Append(' ');
                space = false;
            }

            builder.Append(c);
        }

        return builder.Length <= MaxLineCharacters ? builder.ToString() : builder.ToString(0, MaxLineCharacters - 3) + "...";
    }
}
