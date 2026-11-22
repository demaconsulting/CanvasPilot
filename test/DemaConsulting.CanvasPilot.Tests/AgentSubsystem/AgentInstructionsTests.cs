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

using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
using Microsoft.Extensions.AI;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="AgentInstructions" />: the role prompt, the choice of the tier, the parts of the full and
///     the compact instructions with the real cards and the real kits that come with the program, the facts of the
///     project, and the sizes. The sizes are measured and not estimated: the instructions of every built-in kit stay within
///     the budget of their tier, and so do the words and schemas of all the tools of the copilot.
/// </summary>
public sealed class AgentInstructionsTests
{
    /// <summary>
    ///     The writer of the index over the real cards.
    /// </summary>
    private static readonly KnowledgePromptBuilder Prompts = new(KnowledgeCatalog.Default);

    /// <summary>
    ///     Gets the folder of the kits that come with the program, beside the test assembly.
    /// </summary>
    private static string BuiltInFolder => Path.Combine(AppContext.BaseDirectory, StyleKitCatalog.BuiltInFolderName);

    /// <summary>
    ///     Validates that the role prompt that comes with the application is within its budget and gives the copilot the
    ///     rules that the design names: draw with CanvasNet in the open project, look at the picture after each change, use
    ///     the style tools before and after drawing, use only the tools, use project-relative paths, never invent files, read
    ///     a file before replacing it, example images are readable with the picture tool only, and explain briefly.
    /// </summary>
    [Fact]
    public void AgentInstructions_RolePrompt_Embedded_IsWithinBudgetAndStatesTheRules()
    {
        // Arrange and act: the prompt of an instance made with the default
        var role = new AgentInstructions(Prompts).RolePrompt;

        // Assert: the size, and each rule by its key words
        Assert.InRange(role.Length, 500, AgentInstructions.RolePromptBudgetCharacters);
        Assert.DoesNotContain('\r', role);
        Assert.Contains("CanvasNet in the open project", role, StringComparison.Ordinal);
        Assert.Contains("look at the picture with render_image", role, StringComparison.Ordinal);
        Assert.Contains("Call style_get before you draw", role, StringComparison.Ordinal);
        Assert.Contains("Call style_check after", role, StringComparison.Ordinal);
        Assert.Contains("Use only the tools you are given", role, StringComparison.Ordinal);
        Assert.Contains("relative to the project folder", role, StringComparison.Ordinal);
        Assert.Contains("Never invent a file", role, StringComparison.Ordinal);
        Assert.Contains("read it with project_read", role, StringComparison.Ordinal);
        Assert.Contains("overwrites at once, with no backup", role, StringComparison.Ordinal);
        Assert.Contains("source control", role, StringComparison.Ordinal);
        Assert.Contains("with render_image only", role, StringComparison.Ordinal);
        Assert.Contains("Explain briefly", role, StringComparison.Ordinal);
        foreach (var tool in new[] { "canvasnet_help", "style_get", "style_check", "project_list", "project_read", "project_write", "render_image" })
        {
            Assert.Contains(tool, role, StringComparison.Ordinal);
        }
    }

    /// <summary>
    ///     Validates that a role prompt that is given is used, with its line breaks made the same, and that a prompt that is
    ///     empty or longer than the budget, and a missing writer of the index, are refused.
    /// </summary>
    [Fact]
    public void AgentInstructions_Constructor_RolePromptAndPrompts_AreCheckedAndUsed()
    {
        // Arrange and act: a prompt of the largest size and one that is one character too long
        var largest = new string('r', AgentInstructions.RolePromptBudgetCharacters);
        var given = new AgentInstructions(Prompts, "  Draw.\r\nWell.  ").RolePrompt;

        // Assert
        Assert.Equal("Draw.\nWell.", given);
        Assert.Equal(largest, new AgentInstructions(Prompts, largest).RolePrompt);
        Assert.Throws<ArgumentException>(() => new AgentInstructions(Prompts, largest + "r"));
        Assert.Throws<ArgumentException>(() => new AgentInstructions(Prompts, "  \r\n "));
        Assert.Throws<ArgumentNullException>(() => new AgentInstructions(null!));
    }

    /// <summary>
    ///     Validates the tier for the providers and window sizes: every Copilot model and a window of 16,384 tokens or more
    ///     get the full tier, a smaller window the compact tier.
    /// </summary>
    /// <param name="provider">The provider.</param>
    /// <param name="window">The context window in tokens.</param>
    /// <param name="full">Whether the tier is the full tier.</param>
    [Theory]
    [InlineData(AiProviderKind.GitHubCopilot, 4096, true)]
    [InlineData(AiProviderKind.GitHubCopilot, 32768, true)]
    [InlineData(AiProviderKind.Ollama, 32768, true)]
    [InlineData(AiProviderKind.Ollama, 16384, true)]
    [InlineData(AiProviderKind.Ollama, 16383, false)]
    [InlineData(AiProviderKind.Ollama, 8192, false)]
    [InlineData(AiProviderKind.Ollama, 512, false)]
    public void AgentInstructions_ChooseTier_ProviderAndWindow_FullFromSixteenThousandTokensOrCopilot(AiProviderKind provider, int window, bool full)
    {
        // Act
        var tier = AgentInstructions.ChooseTier(provider, window);

        // Assert
        Assert.Equal(full ? InstructionTier.Full : InstructionTier.Compact, tier);
        Assert.Equal(16384, AgentInstructions.FullTierMinimumContextTokens);
    }

    /// <summary>
    ///     Validates the full tier on a real kit: the role prompt, then the complete index of the cards, then the facts of
    ///     the project, then the name and id of the kit and its style with the example images, in that order.
    /// </summary>
    [Fact]
    public void AgentInstructions_Build_FullTier_HasRoleIndexFactsAndStyleInOrder()
    {
        // Arrange: a project with three files and the kit clean
        var instructions = new AgentInstructions(Prompts);
        var entry = BuiltIn("clean");
        var facts = new AgentProjectFacts("Brochure", @"C:\Work\Brochure", ["a.svg", @"art\b.mmd", "notes.txt"], entry, entry.Kit.Id);

        // Act
        var text = instructions.Build(facts, InstructionTier.Full);

        // Assert: every part is there, once, in order
        var role = text.IndexOf(instructions.RolePrompt, StringComparison.Ordinal);
        var index = text.IndexOf(Prompts.BuildIndex(), StringComparison.Ordinal);
        var project = text.IndexOf("## This project", StringComparison.Ordinal);
        var style = text.IndexOf("## Style", StringComparison.Ordinal);
        Assert.Equal(0, role);
        Assert.True(index > role, "the index follows the role prompt");
        Assert.True(project > index, "the facts follow the index");
        Assert.True(style > project, "the style follows the facts");
        Assert.Contains("- Name: Brochure\n", text, StringComparison.Ordinal);
        Assert.Contains(@"- Folder: C:\Work\Brochure" + "\n", text, StringComparison.Ordinal);
        Assert.Contains("- Files (relative to the folder):\n  - a.svg\n  - art/b.mmd\n  - notes.txt", text, StringComparison.Ordinal);
        Assert.Contains($"Active style kit: {entry.Kit.Name} (id {entry.Kit.Id}).", text, StringComparison.Ordinal);
        Assert.Contains(StyleGuidance.Build(entry).TrimEnd('\n'), text, StringComparison.Ordinal);
        Assert.Contains("## Example images", text, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', text);
        Assert.False(text.EndsWith('\n'));
    }

    /// <summary>
    ///     Validates the compact tier on the same project: the same role prompt, an index of the top-level cards only, fewer
    ///     files, and the style without the example images.
    /// </summary>
    [Fact]
    public void AgentInstructions_Build_CompactTier_HasTopLevelIndexAndNoExamples()
    {
        // Arrange: a project with 30 files and the kit clean
        var instructions = new AgentInstructions(Prompts);
        var entry = BuiltIn("clean");
        var files = Enumerable.Range(0, 30).Select(i => $"drawing-{i:00}.svg").ToList();
        var facts = new AgentProjectFacts("Brochure", @"C:\Work\Brochure", files, entry, entry.Kit.Id);

        // Act
        var full = instructions.Build(facts, InstructionTier.Full);
        var compact = instructions.Build(facts, InstructionTier.Compact);

        // Assert: the compact text has the shallow index, the style without examples, and the shorter list of files
        Assert.NotEqual(Prompts.BuildIndex(), Prompts.BuildIndex(0));
        Assert.StartsWith(instructions.RolePrompt, compact, StringComparison.Ordinal);
        Assert.Contains(Prompts.BuildIndex(0), compact, StringComparison.Ordinal);
        Assert.DoesNotContain(Prompts.BuildIndex(), compact, StringComparison.Ordinal);
        Assert.Contains(Prompts.BuildIndex(), full, StringComparison.Ordinal);
        Assert.DoesNotContain("## Example images", compact, StringComparison.Ordinal);
        Assert.Contains("## Palette", compact, StringComparison.Ordinal);
        Assert.Contains("the first 15 of 30; call project_list for the rest", compact, StringComparison.Ordinal);
        Assert.Contains("drawing-14.svg", compact, StringComparison.Ordinal);
        Assert.DoesNotContain("drawing-15.svg", compact, StringComparison.Ordinal);
        Assert.DoesNotContain("the first 40", full, StringComparison.Ordinal);
        Assert.Contains("drawing-29.svg", full, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates the list of files: a list longer than the limit is cut and says so, a name is made one line and cut
    ///     with an ellipsis when it is too long, a blank name is left out, backslashes become slashes, and a project with no
    ///     file says so.
    /// </summary>
    [Fact]
    public void AgentInstructions_Build_Files_AreLimitedCleanedAndCountedHonestly()
    {
        // Arrange: 45 files, one with a line break in its name, one very long, and a blank one
        var instructions = new AgentInstructions(Prompts);
        var files = new List<string> { "first\nsecond.svg", new string('x', 200) + ".svg", "  " };
        files.AddRange(Enumerable.Range(0, 42).Select(i => $"f{i:00}.svg"));

        // Act
        var many = instructions.Build(new AgentProjectFacts("P", @"C:\P", files, null, null), InstructionTier.Full);
        var none = instructions.Build(new AgentProjectFacts("P", @"C:\P", [], null, null), InstructionTier.Full);

        // Assert
        Assert.Contains("the first 40 of 44; call project_list for the rest", many, StringComparison.Ordinal);
        Assert.Contains("\n  - first second.svg\n", many, StringComparison.Ordinal);
        Assert.Contains("\n  - " + new string('x', AgentInstructions.MaxLineCharacters - 3) + "...\n", many, StringComparison.Ordinal);
        Assert.Contains("f37.svg", many, StringComparison.Ordinal);
        Assert.DoesNotContain("f38.svg", many, StringComparison.Ordinal);
        Assert.Contains("- Files: none yet; create the first one with project_write.", none, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the paths of the list of files stop when their sizes together reach the limit of the tier, with the
    ///     count in the sentence, and that at least one path is always listed.
    /// </summary>
    [Fact]
    public void AgentInstructions_Build_LongNames_StopTheListAtTheCharacterLimit()
    {
        // Arrange: 50 files with names of 100 characters
        var instructions = new AgentInstructions(Prompts);
        var files = Enumerable.Range(0, 50).Select(i => $"{i:00}" + new string('n', 98)).ToList();
        var facts = new AgentProjectFacts("P", @"C:\P", files, null, null);

        // Act
        var full = instructions.Build(facts, InstructionTier.Full);
        var compact = instructions.Build(facts, InstructionTier.Compact);
        var single = instructions.Build(new AgentProjectFacts("P", @"C:\P", [new string('s', 600)], null, null), InstructionTier.Compact);

        // Assert: 16 paths fit 1,600 characters and 5 fit 500; a single long path is cut to a line and listed
        Assert.Contains("the first 16 of 50;", full, StringComparison.Ordinal);
        Assert.Contains("the first 5 of 50;", compact, StringComparison.Ordinal);
        Assert.Contains("\n  - " + new string('s', AgentInstructions.MaxLineCharacters - 3) + "...", single, StringComparison.Ordinal);
        Assert.Contains("- Files (relative to the folder):", single, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates what the instructions say when the project has no style kit, and when it names a kit that cannot be
    ///     found.
    /// </summary>
    [Fact]
    public void AgentInstructions_Build_NoStyleOrMissingStyle_SaysSo()
    {
        // Arrange
        var instructions = new AgentInstructions(Prompts);

        // Act
        var none = instructions.Build(new AgentProjectFacts("P", @"C:\P", [], null, null), InstructionTier.Full);
        var missing = instructions.Build(new AgentProjectFacts("P", @"C:\P", [], null, "team-kit"), InstructionTier.Compact);

        // Assert
        Assert.EndsWith("The project has no style kit. Draw with a clean, plain look and tell the user that a style kit can be chosen.", none, StringComparison.Ordinal);
        Assert.EndsWith("The project names the style kit 'team-kit', which cannot be found. Draw without a style and tell the user that the kit is missing.", missing, StringComparison.Ordinal);
        Assert.DoesNotContain("Active style kit", none, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Validates that the same input gives the same text and that a missing argument or an unknown tier is refused.
    /// </summary>
    [Fact]
    public void AgentInstructions_Build_SameInput_SameTextAndBadArgumentsAreRefused()
    {
        // Arrange
        var instructions = new AgentInstructions(Prompts);
        var entry = BuiltIn("clean");
        var facts = new AgentProjectFacts("Brochure", @"C:\Work\Brochure", ["a.svg"], entry, entry.Kit.Id);

        // Act and assert
        Assert.Equal(instructions.Build(facts, InstructionTier.Full), new AgentInstructions(new KnowledgePromptBuilder(KnowledgeCatalog.Default)).Build(facts, InstructionTier.Full));
        Assert.Throws<ArgumentNullException>(() => instructions.Build(null!, InstructionTier.Full));
        Assert.Throws<ArgumentOutOfRangeException>(() => instructions.Build(facts, (InstructionTier)7));
    }

    /// <summary>
    ///     Validates the size of the instructions of the full tier, measured with the real cards, the role prompt that comes
    ///     with the application, every kit that comes with the program, and the longest list of files: at most 20,000
    ///     characters.
    /// </summary>
    [Fact]
    public void AgentInstructions_Build_FullTier_EveryBuiltInKitAndLongestFileList_FitsTwentyThousandCharacters()
    {
        // Arrange and act
        var sizes = MeasureEveryBuiltInKit(InstructionTier.Full);

        // Assert: the largest of the kits fits, and the message names the sizes so that a failure shows them
        var largest = sizes.MaxBy(pair => pair.Value);
        Assert.True(
            largest.Value <= AgentInstructions.FullBudgetCharacters,
            $"The full instructions are {largest.Value} characters for the kit {largest.Key}; the budget is {AgentInstructions.FullBudgetCharacters}.");
        Assert.True(sizes.Count >= 3, "at least the three kits of the first release are measured");
    }

    /// <summary>
    ///     Validates the size of the instructions of the compact tier, measured the same way: at most 11,000 characters.
    /// </summary>
    [Fact]
    public void AgentInstructions_Build_CompactTier_EveryBuiltInKitAndLongestFileList_FitsElevenThousandCharacters()
    {
        // Arrange and act
        var sizes = MeasureEveryBuiltInKit(InstructionTier.Compact);

        // Assert
        var largest = sizes.MaxBy(pair => pair.Value);
        Assert.True(
            largest.Value <= AgentInstructions.CompactBudgetCharacters,
            $"The compact instructions are {largest.Value} characters for the kit {largest.Key}; the budget is {AgentInstructions.CompactBudgetCharacters}.");
        Assert.True(sizes.Count >= 3, "at least the three kits of the first release are measured");
    }

    /// <summary>
    ///     Validates the size of the tool text: the names, words, and schemas of the drawing knowledge tool, the two style
    ///     tools, and the four project tools together are at most 7,000 characters, measured on the functions that the
    ///     registry builds.
    /// </summary>
    [Fact]
    public void AgentInstructions_ToolSchemas_AllToolsOfTheCopilot_FitSevenThousandCharacters()
    {
        // Arrange: the registry over the real tools, with stand-ins for what the style tools read
        var styleTools = StyleTools.CreateAll(
            new FakeActiveStyle(),
            Substitute.For<IStyleKitCatalog>(),
            () => null,
            () => null,
            Substitute.For<IImageRenderService>());
        var projectTools = new ProjectTools(Substitute.For<IImageRenderService>());
        var registry = new AgentToolRegistry(KnowledgeTools.CreateAll(KnowledgeCatalog.Default), styleTools, projectTools.CreatePacks);
        var folder = Directory.CreateTempSubdirectory("canvaspilot-instructions-").FullName;
        try
        {
            // Act
            var tools = registry.CreateTools(new AgentToolContext(folder), new HashSet<string>());
            var total = tools.Sum(tool => tool.Name.Length + tool.Description.Length + tool.JsonSchema.GetRawText().Length);

            // Assert: all seven tools are counted, and the total is within the budget
            Assert.Equal(7, tools.Count);
            Assert.True(
                total <= AgentInstructions.ToolSchemaBudgetCharacters,
                $"The tool text is {total} characters; the budget is {AgentInstructions.ToolSchemaBudgetCharacters}.");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    ///     Builds the instructions of a tier for every kit that comes with the program, with the longest list of the longest
    ///     file names, and measures them.
    /// </summary>
    /// <param name="tier">The tier.</param>
    /// <returns>The number of characters, by the id of the kit.</returns>
    private static Dictionary<string, int> MeasureEveryBuiltInKit(InstructionTier tier)
    {
        var instructions = new AgentInstructions(Prompts);
        var files = Enumerable.Range(0, 100).Select(i => $"drawings/{i:000}-" + new string('n', 150) + ".svg").ToList();
        var sizes = new Dictionary<string, int>();
        foreach (var folder in Directory.GetDirectories(BuiltInFolder))
        {
            var entry = BuiltIn(Path.GetFileName(folder));
            var facts = new AgentProjectFacts(new string('p', 150), @"C:\Users\somebody\Documents\" + new string('f', 150), files, entry, entry.Kit.Id);
            sizes[entry.Kit.Id] = instructions.Build(facts, tier).Length;
        }

        return sizes;
    }

    /// <summary>
    ///     Gets a kit that comes with the program, read from the folder beside the test assembly.
    /// </summary>
    /// <param name="id">The id of the kit.</param>
    /// <returns>The entry, with the place <see cref="StyleKitLocation.BuiltIn" />.</returns>
    private static StyleKitEntry BuiltIn(string id)
    {
        var folder = Path.Combine(BuiltInFolder, id);
        var result = StyleKitSerializer.ReadFolder(folder);
        Assert.True(result.Succeeded, string.Join("; ", result.Issues));
        return new StyleKitEntry(result.Kit!, StyleKitLocation.BuiltIn, folder, null, true, false, null, []);
    }
}
