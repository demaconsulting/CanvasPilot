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
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the Knowledge subsystem: the catalog of the application, with the cards of CanvasNet and of
///     CanvasPilot, as the units that are built on it see it.
/// </summary>
public sealed class KnowledgeSubsystemTests
{
    /// <summary>
    ///     The names of packages of language models and agent libraries that the Knowledge subsystem must not depend on.
    /// </summary>
    private static readonly string[] LanguageModelPackageNames =
    [
        "Microsoft.Copilot.SDK",
        "GitHub.Copilot.SDK",
        "OllamaSharp",
        "Microsoft.Extensions.AI",
        "AgentKit",
        "Microsoft.SemanticKernel",
        "OpenAI",
        "Azure.AI",
        "Anthropic",
        "LLamaSharp"
    ];

    /// <summary>
    ///     Validates that the catalog of the application offers the cards of CanvasNet for drawing, the cards of
    ///     CanvasPilot for working in a project, and nothing that the host program controls.
    /// </summary>
    [Fact]
    public void KnowledgeSubsystem_Default_OffersDrawingCardsOfCanvasNetAndCanvasPilotWithoutHostingCards()
    {
        // Arrange: the catalog of the application
        var catalog = KnowledgeCatalog.Default;
        var ids = catalog.Cards.Select(c => c.Id).ToList();

        // Act
        var roots = catalog.Roots.Select(c => c.Id).ToList();

        // Assert: a card for every drawing format, the cards of CanvasPilot last, no card of the hosting tag
        Assert.All(new[] { "svg", "mermaid", "plantuml", "scad", "vega-lite", "composition", "canvaspilot" }, id => Assert.Contains(id, roots));
        Assert.Equal("canvaspilot", roots[^1]);
        Assert.Contains("canvaspilot/marks", ids);
        Assert.DoesNotContain(catalog.Cards, c => c.Tags.Contains("hosting"));
        Assert.DoesNotContain("files", ids);
        Assert.DoesNotContain("svg/options", ids);
    }

    /// <summary>
    ///     Validates that the catalog and its cards can be read from several threads at the same time, and that the
    ///     catalog of the application is one shared value.
    /// </summary>
    [Fact]
    public async Task KnowledgeSubsystem_Default_IsOneSharedCatalogReadableFromManyThreads()
    {
        // Arrange: many readers
        var readers = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(() => KnowledgeCatalog.Default))
            .ToArray();

        // Act
        var catalogs = await Task.WhenAll(readers);

        // Assert: every reader got the same catalog
        Assert.All(catalogs, c => Assert.Same(KnowledgeCatalog.Default, c));
    }

    /// <summary>
    ///     Validates that the search finds, in the catalog of the application, the cards that answer the questions of a
    ///     user and of an assistant, with the cards of CanvasPilot among them, and finds nothing for words that no card
    ///     has.
    /// </summary>
    [Fact]
    public void KnowledgeSubsystem_Search_QuestionsAboutDrawing_FindTheCardsThatAnswerThem()
    {
        // Arrange: the search over the catalog of the application
        var search = new KnowledgeSearch(KnowledgeCatalog.Default);

        // Act
        var gantt = search.Search("gantt chart");
        var marks = search.Search("how do I read the marks of a message");
        var layout = search.Search("project folder layout");
        var nothing = search.Search("xylophone");

        // Assert: the card of the format, the cards of CanvasPilot, and an empty answer
        Assert.Equal("mermaid/gantt", gantt[0].Id);
        Assert.Contains(marks, r => r.Id == "canvaspilot/marks");
        Assert.Equal("canvaspilot/project-layout", layout[0].Id);
        Assert.Empty(nothing);
    }

    /// <summary>
    ///     Validates that the library of the subsystem refers to no package of a language model or agent, by its
    ///     referenced assemblies and by the packages that its dependency file lists.
    /// </summary>
    [Fact]
    public void KnowledgeSubsystem_Assembly_ReferencesNoLanguageModelPackage()
    {
        // Arrange: the assembly of the application library, the packages that the application library and the test
        // project resolve (the dependency file of the test project lists the packages of the project it references), and
        // the project file of the application library
        var assembly = typeof(KnowledgeCatalog).Assembly;
        var references = assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty).ToList();
        var dependencyFile = Path.Combine(AppContext.BaseDirectory, "DemaConsulting.CanvasPilot.Tests.deps.json");
        Assert.True(File.Exists(dependencyFile), "The dependency file of the test project is missing.");
        var projectFile = FindProjectFile();

        // Act
        using var document = JsonDocument.Parse(File.ReadAllText(dependencyFile));
        var packages = document.RootElement.GetProperty("libraries").EnumerateObject()
            .Select(p => p.Name)
            .ToList();
        var declared = File.ReadAllLines(projectFile)
            .Where(l => l.Contains("PackageReference", StringComparison.Ordinal))
            .ToList();

        // Assert: no referenced assembly, resolved package, or declared package is a language model or agent library
        Assert.NotEmpty(references);
        Assert.NotEmpty(packages);
        Assert.NotEmpty(declared);
        foreach (var name in LanguageModelPackageNames)
        {
            Assert.DoesNotContain(references, r => r.StartsWith(name, StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(packages, p => p.StartsWith(name + "/", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(declared, l => l.Contains($"\"{name}", StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>
    ///     Finds the project file of the application library by walking up from the folder of the tests.
    /// </summary>
    /// <returns>The full path of the project file.</returns>
    private static string FindProjectFile()
    {
        var folder = new DirectoryInfo(AppContext.BaseDirectory);
        while (folder is not null)
        {
            var candidate = Path.Combine(folder.FullName, "src", "DemaConsulting.CanvasPilot", "DemaConsulting.CanvasPilot.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            folder = folder.Parent;
        }

        throw new InvalidOperationException("The project file of the application library was not found.");
    }
}
