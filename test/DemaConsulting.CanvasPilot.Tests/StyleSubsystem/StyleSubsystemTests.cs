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
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Subsystem tests for the Style subsystem: the units of the subsystem working together on temporary folders, and
///     the rule that the subsystem depends on no language model or agent library.
/// </summary>
public sealed class StyleSubsystemTests : IDisposable
{
    /// <summary>
    ///     The names of packages of language models and agent libraries that the Style subsystem must not depend on.
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
    ///     Temporary folder removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-style-").FullName;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Validates that a kit that is built in memory, checked, written to a folder with its example files, and read
    ///     back from that folder is the same kit, with its examples found and its warnings reported: the format that the
    ///     other units of the subsystem share.
    /// </summary>
    [Fact]
    public void StyleSubsystem_DefineKits_WrittenKitFolder_IsReadBackWithItsExamplesAndWarnings()
    {
        // Arrange: a kit with a font that is not bundled, and a folder named as the kit with two example files
        var kit = new StyleKit(
            "team-look",
            "Team Look",
            "The look of the team.",
            new Dictionary<string, string> { ["background"] = "#FFFFFF", ["text"] = "#111827", ["primary"] = "#0EA5E9", ["highlight"] = "#F43F5E" },
            new StyleFonts("Inter, sans-serif", "sans-serif", "monospace"),
            2,
            8,
            12,
            new StyleMermaid("base", new Dictionary<string, string> { ["primaryColor"] = "#0EA5E9" }),
            """{"range":{"category":["#0EA5E9"]}}""",
            new StyleGuidanceText("Keep margins wide.", ["Use the palette."], ["Use shadows."]),
            [new StyleSizePreset("Slide", 1920, 1080, null, false)],
            [new StyleExample("examples/logo.svg", string.Empty, "The logo.")]);
        var folder = Directory.CreateDirectory(Path.Combine(_root, "styles", "team-look")).FullName;
        Directory.CreateDirectory(Path.Combine(folder, "examples"));
        File.WriteAllText(Path.Combine(folder, "examples", "logo.svg"), "<svg xmlns='http://www.w3.org/2000/svg'/>");
        File.WriteAllText(Path.Combine(folder, "examples", "chart.vl.json"), "{}");

        // Act: check the kit, write it, and read the folder
        var problems = StyleKitSerializer.Validate(kit);
        File.WriteAllText(Path.Combine(folder, StyleKitSerializer.FileName), StyleKitSerializer.Write(kit));
        var result = StyleKitSerializer.ReadFolder(folder);

        // Assert: one warning about the font and none about anything else, no error, and the kit read back whole
        Assert.Equal(["fonts.heading"], problems.Select(p => p.Path));
        Assert.True(result.Succeeded, string.Join("; ", result.Errors));
        Assert.Equal(["fonts.heading"], result.Warnings.Select(w => w.Path));
        var read = result.Kit!;
        Assert.Equal(StyleKitSerializer.Write(kit), StyleKitSerializer.Write(read));
        Assert.Equal(["examples/chart.vl.json", "examples/logo.svg"], read.Examples.Select(e => e.RelativePath));
        Assert.Equal(["#FFFFFF", "#111827", "#0EA5E9", "#F43F5E"], read.Palette.Values);
        Assert.Equal("The logo.", read.Examples.Single(e => e.RelativePath == "examples/logo.svg").Caption);
        Assert.True(File.Exists(read.Examples[0].FullPath));
    }

    /// <summary>
    ///     Validates that the library of the subsystem refers to no package of a language model or agent, by its
    ///     referenced assemblies and by the packages that its dependency file lists.
    /// </summary>
    [Fact]
    public void StyleSubsystem_Assembly_ReferencesNoLanguageModelPackage()
    {
        // Arrange: the assembly of the application library, the packages that the application library and the test
        // project resolve (the dependency file of the test project lists the packages of the project it references), and
        // the project file of the application library
        var assembly = typeof(StyleKitSerializer).Assembly;
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
