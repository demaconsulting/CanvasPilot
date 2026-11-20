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

using DemaConsulting.CanvasNet.Viewer.Help;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="KnowledgeCatalog" />, on the catalog of the application, which is the CanvasNet help
///     cards extended with the cards that are embedded in this assembly and then filtered.
/// </summary>
public sealed class KnowledgeCatalogTests
{
    /// <summary>
    ///     The identifiers of the cards of CanvasPilot, root first.
    /// </summary>
    private static readonly string[] CanvasPilotCardIds =
    [
        "canvaspilot",
        "canvaspilot/project-layout",
        "canvaspilot/deliverables",
        "canvaspilot/marks",
        "canvaspilot/preview"
    ];

    /// <summary>
    ///     The identifiers of the CanvasNet cards that carry the hosting tag, which the filter removes.
    /// </summary>
    private static readonly string[] HostingCardIds =
    [
        "errors", "files", "pdf", "pptx", "vsdx", "raster", "positions", "positions/selectors", "svg/options",
        "svg/flatten", "scad/options", "scad/views", "mermaid/options", "plantuml/options", "vega-lite/options",
        "vega-lite/params"
    ];

    /// <summary>
    ///     Reads the text of an embedded card or example of CanvasPilot.
    /// </summary>
    /// <param name="logicalName">The logical name of the resource.</param>
    /// <returns>The text, with line feeds and without trailing line feeds.</returns>
    private static string ReadEmbedded(string logicalName)
    {
        using var stream = typeof(KnowledgeCatalog).Assembly.GetManifestResourceStream(logicalName)
                           ?? throw new InvalidOperationException($"The resource '{logicalName}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n');
    }

    /// <summary>
    ///     Validates that no card of the application carries the hosting tag.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Default_NoCardCarriesAnExcludedTag()
    {
        // Arrange: the catalog of the application
        var catalog = KnowledgeCatalog.Default;

        // Act / Assert: the tags of the cards do not include the excluded ones, and the exclusion is the hosting tag
        Assert.Equal(["hosting"], KnowledgeCatalog.ExcludedTags);
        Assert.NotEmpty(catalog.Cards);
        Assert.All(catalog.Cards, card => Assert.DoesNotContain("hosting", card.Tags));
    }

    /// <summary>
    ///     Validates that every CanvasNet card about what the host controls is absent and that the other cards remain.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Default_HostingCardsAreAbsentAndAuthoringCardsRemain()
    {
        // Arrange: the catalog of the application
        var catalog = KnowledgeCatalog.Default;
        var ids = catalog.Cards.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        // Act / Assert: the cards of the hosting tag are not found, and the cards for writing each format are
        Assert.All(HostingCardIds, id => Assert.DoesNotContain(id, ids));
        Assert.All(HostingCardIds, id => Assert.Null(catalog.Find(id)));
        Assert.Contains("svg/framed-figure", ids);
        Assert.Contains("mermaid/flowchart", ids);
        Assert.Contains("plantuml/composition", ids);
        Assert.Contains("scad/anchors", ids);
        Assert.Contains("vega-lite/data", ids);
        Assert.Contains("composition", ids);
        Assert.Equal(
            HelpCatalog.Default.Cards.Count - HostingCardIds.Length + CanvasPilotCardIds.Length,
            catalog.Cards.Count);
    }

    /// <summary>
    ///     Validates that the visible cards below a hidden card are roots of the catalog.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Default_VisibleCardsBelowAHiddenCardArePromotedToRoots()
    {
        // Arrange: the catalog of the application, where the errors card is hidden but its two children are not
        var catalog = KnowledgeCatalog.Default;

        // Act
        var codes = catalog.Find("errors/codes");
        var nested = catalog.Find("svg/framed-figure");

        // Assert: the child of the hidden card has no parent and is a root; a card below a visible card keeps its parent
        Assert.NotNull(codes);
        Assert.Null(codes.ParentId);
        Assert.Contains(codes, catalog.Roots);
        Assert.NotNull(nested);
        Assert.Equal("svg", nested.ParentId);
        Assert.DoesNotContain(nested, catalog.Roots);
    }

    /// <summary>
    ///     Validates that an unknown tag of the filter is refused.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Create_UnknownExcludedTag_ThrowsArgumentException()
    {
        // Arrange: nothing

        // Act / Assert: a tag that CanvasNet does not know is refused
        Assert.Throws<ArgumentException>(() => KnowledgeCatalog.Create(
            HelpCatalog.Default,
            typeof(KnowledgeCatalog).Assembly,
            KnowledgeCatalog.ResourcePrefix,
            ["no-such-tag"]));
    }

    /// <summary>
    ///     Validates that a missing argument is refused.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Create_NullArguments_ThrowArgumentNullException()
    {
        // Arrange: nothing

        // Act / Assert: each argument is required
        Assert.Throws<ArgumentNullException>(() => KnowledgeCatalog.Create(
            null!,
            typeof(KnowledgeCatalog).Assembly,
            KnowledgeCatalog.ResourcePrefix,
            []));
        Assert.Throws<ArgumentNullException>(() => KnowledgeCatalog.Create(
            HelpCatalog.Default,
            null!,
            KnowledgeCatalog.ResourcePrefix,
            []));
        Assert.Throws<ArgumentNullException>(() => KnowledgeCatalog.Create(
            HelpCatalog.Default,
            typeof(KnowledgeCatalog).Assembly,
            null!,
            []));
        Assert.Throws<ArgumentNullException>(() => KnowledgeCatalog.Create(
            HelpCatalog.Default,
            typeof(KnowledgeCatalog).Assembly,
            KnowledgeCatalog.ResourcePrefix,
            null!));
    }

    /// <summary>
    ///     Validates that the cards of CanvasPilot are one tree after the cards of CanvasNet, and that every card file
    ///     of the folder is a card.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Default_HasTheCanvasPilotCardsAsOneTreeAfterTheLibraryRoots()
    {
        // Arrange: the catalog of the application and the card files that are embedded
        var catalog = KnowledgeCatalog.Default;
        var prefix = KnowledgeCatalog.ResourcePrefix + "/";
        var embedded = typeof(KnowledgeCatalog).Assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
            .Select(n => n[prefix.Length..^".md".Length].Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToList();

        // Act
        var root = catalog.Find("canvaspilot");
        var inTree = catalog.Cards.Where(c => c.Id == "canvaspilot" || c.Id.StartsWith("canvaspilot/", StringComparison.Ordinal))
            .Select(c => c.Id)
            .Order(StringComparer.Ordinal)
            .ToList();

        // Assert: every embedded file is a card of the tree, the children are the files of the folder, the tree is last
        Assert.NotNull(root);
        Assert.Equal(embedded, inTree);
        Assert.Equal(CanvasPilotCardIds.Order(StringComparer.Ordinal), inTree);
        Assert.Equal(
            CanvasPilotCardIds.Skip(1).Order(StringComparer.Ordinal),
            root.ChildIds.Order(StringComparer.Ordinal));
        Assert.Equal("canvaspilot", catalog.Roots[^1].Id);
        Assert.Equal(CanvasPilotCardIds, catalog.Cards.TakeLast(CanvasPilotCardIds.Length).Select(c => c.Id).ToList());
        Assert.All(catalog.GetChildren(root), child => Assert.Equal("canvaspilot", child.ParentId));
    }

    /// <summary>
    ///     Validates that every card has a title, a summary, a body, and a scope tag, and that the related cards exist.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Default_EveryCardIsComplete()
    {
        // Arrange: the catalog of the application
        var catalog = KnowledgeCatalog.Default;
        var ids = catalog.Cards.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);

        // Act / Assert: each card has what a reader needs, and its links go to cards of the catalog
        Assert.Equal(catalog.Cards.Count, ids.Count);
        Assert.All(catalog.Cards, card =>
        {
            Assert.False(string.IsNullOrWhiteSpace(card.Title));
            Assert.False(string.IsNullOrWhiteSpace(card.Summary));
            Assert.NotEmpty(card.Tags);
            Assert.All(card.ChildIds, id => Assert.Contains(id, ids));
            Assert.All(card.SeeAlsoIds, id => Assert.Contains(id, ids));
            Assert.Equal(card.Id[(card.Id.LastIndexOf('/') + 1)..], card.Name);
        });
        Assert.All(catalog.Cards.Where(c => c.Id.StartsWith("canvaspilot", StringComparison.Ordinal)), card =>
        {
            Assert.False(string.IsNullOrWhiteSpace(card.Body));
            Assert.Contains(card.Tags, t => t is "authoring" or "composition" or "diagnostics" or "file-access");
        });
    }

    /// <summary>
    ///     Validates that a card is found by its path, with a slash or a space, by its name or alias, in any case.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Find_PathNameAliasAndCase_FindTheCard()
    {
        // Arrange: the catalog of the application
        var catalog = KnowledgeCatalog.Default;

        // Act
        var byPath = catalog.Find("svg/framed-figure");
        var bySpaces = catalog.Find("svg framed-figure");
        var byAlias = catalog.Find("svg/framed");
        var byUpperCase = catalog.Find("SVG/Framed-Figure");
        var byProjectAlias = catalog.Find("project");

        // Assert: every way finds the card
        Assert.NotNull(byPath);
        Assert.Equal("svg/framed-figure", byPath.Id);
        Assert.Same(byPath, bySpaces);
        Assert.Same(byPath, byAlias);
        Assert.Same(byPath, byUpperCase);
        Assert.NotNull(byProjectAlias);
        Assert.Equal("canvaspilot", byProjectAlias.Id);
    }

    /// <summary>
    ///     Validates that a path that names no card, or a hidden card, finds nothing, and that a missing path is refused.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Find_UnknownHiddenOrBlankPath_FindsNothing()
    {
        // Arrange: the catalog of the application

        // Act / Assert: unknown, hidden, and blank paths find no card, and null is refused
        Assert.Null(KnowledgeCatalog.Default.Find("no/such/card"));
        Assert.Null(KnowledgeCatalog.Default.Find("files"));
        Assert.Null(KnowledgeCatalog.Default.Find("scad/views"));
        Assert.Null(KnowledgeCatalog.Default.Find("  "));
        Assert.Throws<ArgumentNullException>(() => KnowledgeCatalog.Default.Find(null!));
    }

    /// <summary>
    ///     Validates that the children and the related cards of a card are the cards its identifiers name, and that a
    ///     missing card is refused.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_GetChildrenAndGetSeeAlso_ReturnTheNamedCardsInOrder()
    {
        // Arrange: the root card of CanvasPilot
        var catalog = KnowledgeCatalog.Default;
        var root = catalog.Find("canvaspilot");
        Assert.NotNull(root);

        // Act
        var children = catalog.GetChildren(root);
        var related = catalog.GetSeeAlso(root);

        // Assert: the cards are those of the identifiers, in order; a leaf has no children; null is refused
        Assert.Equal(root.ChildIds, children.Select(c => c.Id).ToList());
        Assert.Equal(["svg/composition", "composition"], related.Select(c => c.Id).ToList());
        Assert.Empty(catalog.GetChildren(children[0]));
        Assert.Throws<ArgumentNullException>(() => catalog.GetChildren(null!));
        Assert.Throws<ArgumentNullException>(() => catalog.GetSeeAlso(null!));
    }

    /// <summary>
    ///     Validates that every link of every body is written as the identifier of a card, so that no body holds the
    ///     bracket syntax of CanvasNet.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Default_BodiesHaveLinksWrittenAsCardIds()
    {
        // Arrange: the catalog of the application, and the card of CanvasPilot that links to a card and an example
        var catalog = KnowledgeCatalog.Default;
        var root = catalog.Find("canvaspilot");
        var deliverables = catalog.Find("canvaspilot/deliverables");
        Assert.NotNull(root);
        Assert.NotNull(deliverables);

        // Act / Assert: a card link is its identifier in code quotes, an example link names the example and its card,
        // and no body holds a link of the CanvasNet syntax outside the code that is quoted
        Assert.Contains("see `canvaspilot/marks` for how to read them", Flat(root.Body), StringComparison.Ordinal);
        Assert.Contains("Print a complete one with example `composite` of `canvaspilot/deliverables`.", Flat(deliverables.Body), StringComparison.Ordinal);
        Assert.All(catalog.Cards, card => Assert.DoesNotMatch(@"\[\[[A-Za-z0-9#]", WithoutCode(card.Body)));
    }

    /// <summary>
    ///     Validates that a link to a hidden card is written as plain text, not as a card identifier.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Default_LinksToHiddenCardsAreNotCardIds()
    {
        // Arrange: the library card about embedding files, which links to the visible card about composition and to the
        // hidden card about files
        var library = HelpCatalog.Default.Find("svg/embedding");
        var hidden = HelpCatalog.Default.Find("files");
        var card = KnowledgeCatalog.Default.Find("svg/embedding");
        Assert.NotNull(library);
        Assert.NotNull(hidden);
        Assert.NotNull(card);

        // Act
        var original = Flat(HelpCatalog.Default.FormatMarkdown(library, _ => "<link>"));
        var body = Flat(card.Body);

        // Assert: the link to the hidden card is the title of that card as plain text where the library writes a link,
        // and the link to a visible card is its identifier
        Assert.Contains("see <link>", original, StringComparison.Ordinal);
        Assert.Contains($"see {hidden.Title}", body, StringComparison.Ordinal);
        Assert.DoesNotContain("`files`", body, StringComparison.Ordinal);
        Assert.Contains("example `framed` of `svg/framed-figure`", Flat(KnowledgeCatalog.Default.Find("svg/framed-figure")!.Body), StringComparison.Ordinal);
        Assert.Contains("example `anchors` of `scad/anchors`", Flat(KnowledgeCatalog.Default.Find("svg/framed-figure")!.Body), StringComparison.Ordinal);
    }

    /// <summary>
    ///     Writes a text on one line, with a single space for every run of white space.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The text on one line.</returns>
    private static string Flat(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
    }

    /// <summary>
    ///     Removes the code blocks and the code spans of a Markdown text.
    /// </summary>
    /// <param name="markdown">The text.</param>
    /// <returns>The text without code.</returns>
    private static string WithoutCode(string markdown)
    {
        var withoutBlocks = System.Text.RegularExpressions.Regex.Replace(
            markdown,
            "```.*?```",
            string.Empty,
            System.Text.RegularExpressions.RegexOptions.Singleline);
        return System.Text.RegularExpressions.Regex.Replace(withoutBlocks, "`[^`\n]*`", string.Empty);
    }

    /// <summary>
    ///     Validates that an example name that two trees share finds both, that a card prefix selects one, and that a
    ///     scope limits the search.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_FindExamples_SharedNameCardPrefixAndScope_SelectTheExamples()
    {
        // Arrange: the catalog of the application, where the name "sequence" is an example of Mermaid and of PlantUML
        var catalog = KnowledgeCatalog.Default;

        // Act
        var all = catalog.FindExamples("sequence");
        var mermaid = catalog.FindExamples("mermaid/sequence#sequence");
        var scoped = catalog.FindExamples("sequence", "plantuml");
        var upper = catalog.FindExamples("SEQUENCE", "mermaid");

        // Assert: two cards hold it, the prefix and the scope each select one, and the case does not matter
        Assert.Equal(2, all.Count);
        Assert.Contains(all, e => e.CardId.StartsWith("mermaid/", StringComparison.Ordinal));
        Assert.Contains(all, e => e.CardId.StartsWith("plantuml", StringComparison.Ordinal));
        Assert.Single(mermaid);
        Assert.StartsWith("mermaid/", mermaid[0].CardId, StringComparison.Ordinal);
        Assert.Single(scoped);
        Assert.StartsWith("plantuml", scoped[0].CardId, StringComparison.Ordinal);
        Assert.Single(upper);
        Assert.Equal("sequence", upper[0].Example.Name);
    }

    /// <summary>
    ///     Validates that an unknown example, an unknown card, and a missing name find nothing or are refused.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_FindExamples_UnknownNameOrCard_FindsNothing()
    {
        // Arrange: the catalog of the application

        // Act / Assert: nothing is found for an unknown example, an unknown card prefix, or an unknown scope
        Assert.Empty(KnowledgeCatalog.Default.FindExamples("no-such-example"));
        Assert.Empty(KnowledgeCatalog.Default.FindExamples("no/such/card#framed"));
        Assert.Empty(KnowledgeCatalog.Default.FindExamples("framed", "no/such/card"));
        Assert.Empty(KnowledgeCatalog.Default.FindExamples("composite", "svg"));
        Assert.Throws<ArgumentNullException>(() => KnowledgeCatalog.Default.FindExamples(null!));
    }

    /// <summary>
    ///     Validates that the examples of CanvasPilot are the embedded files, in the card that names them.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_FindExamples_CanvasPilotExamples_AreTheEmbeddedFiles()
    {
        // Arrange: the catalog of the application
        var catalog = KnowledgeCatalog.Default;

        // Act
        var composite = catalog.FindExamples("composite");
        var payload = catalog.FindExamples("canvaspilot/deliverable#composite");
        var marks = catalog.FindExamples("mark-payload");

        // Assert: one example each, in its card, with the content of the embedded file
        Assert.Single(composite);
        Assert.Equal("canvaspilot/deliverables", composite[0].CardId);
        Assert.Equal("snippet", composite[0].Example.Kind);
        Assert.Equal("xml", composite[0].Example.Language);
        Assert.Equal("composite.svg", composite[0].Example.FileName);
        Assert.Equal(
            ReadEmbedded(KnowledgeCatalog.ResourcePrefix + ".Examples.canvaspilot-composite.svg"),
            composite[0].Example.Content);
        Assert.Single(payload);
        Assert.Single(marks);
        Assert.Equal("canvaspilot/marks", marks[0].CardId);
        Assert.Equal("json", marks[0].Example.Language);
        Assert.Equal(
            ReadEmbedded(KnowledgeCatalog.ResourcePrefix + ".Examples.canvaspilot-mark-payload.json"),
            marks[0].Example.Content);
    }

    /// <summary>
    ///     Validates that the examples of CanvasNet that remain are those of the library, unchanged.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Default_LibraryExamples_EqualTheLibraryFiles()
    {
        // Arrange: the library catalog and the catalog of the application
        var library = HelpCatalog.Default;
        var catalog = KnowledgeCatalog.Default;

        // Act / Assert: every example of a card that remains has the content, kind, language, and file of the library
        foreach (var card in catalog.Cards.Where(c => library.Find(c.Id) is not null))
        {
            var original = library.Find(card.Id);
            Assert.NotNull(original);
            Assert.Equal(original.Examples.Count, card.Examples.Count);
            for (var i = 0; i < card.Examples.Count; i++)
            {
                Assert.Equal(original.Examples[i].Name, card.Examples[i].Name);
                Assert.Equal(original.Examples[i].Kind, card.Examples[i].Kind);
                Assert.Equal(original.Examples[i].Language, card.Examples[i].Language);
                Assert.Equal(original.Examples[i].FileName, card.Examples[i].FileName);
                Assert.Equal(original.Examples[i].Description, card.Examples[i].Description);
                Assert.Equal(original.Examples[i].Content, card.Examples[i].Content);
            }
        }

        Assert.NotEmpty(catalog.Cards.SelectMany(c => c.Examples));
    }

    /// <summary>
    ///     Validates that the types the unit offers refer to no help type of CanvasNet, and that the lists of its cards
    ///     cannot be changed.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Default_PublicTypesReferToNoHelpTypeOfCanvasNetAndCannotBeChanged()
    {
        // Arrange: the public types of the subsystem that describe the knowledge, and the catalog
        var types = new[]
        {
            typeof(IKnowledgeCatalog), typeof(KnowledgeCatalog), typeof(KnowledgeCard), typeof(KnowledgeExample),
            typeof(KnowledgeExampleReference)
        };
        var catalog = KnowledgeCatalog.Default;
        var card = catalog.Find("canvaspilot/marks");
        Assert.NotNull(card);

        // Act: collect the types that the public members mention
        var mentioned = types
            .SelectMany(t => t.GetMembers(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly))
            .SelectMany(MentionedTypes)
            .SelectMany(Expand)
            .Distinct()
            .ToList();

        // Assert: none is a type of the help of CanvasNet, and none of the lists can be added to
        Assert.NotEmpty(mentioned);
        Assert.DoesNotContain(mentioned, t => (t.Namespace ?? string.Empty).StartsWith("DemaConsulting.CanvasNet", StringComparison.Ordinal));
        Assert.Throws<NotSupportedException>(() => ((IList<KnowledgeCard>)catalog.Cards).Add(card));
        Assert.Throws<NotSupportedException>(() => ((IList<KnowledgeCard>)catalog.Roots).Add(card));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)card.Tags).Add("x"));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)card.ChildIds).Add("x"));
        Assert.Throws<NotSupportedException>(() => ((IList<KnowledgeExample>)card.Examples).Add(card.Examples[0]));
    }

    /// <summary>
    ///     Gets the types that a member mentions.
    /// </summary>
    /// <param name="member">The member.</param>
    /// <returns>The types of its value, its parameters, or its return.</returns>
    private static IEnumerable<Type> MentionedTypes(System.Reflection.MemberInfo member)
    {
        return member switch
        {
            System.Reflection.PropertyInfo p => [p.PropertyType, .. p.GetIndexParameters().Select(i => i.ParameterType)],
            System.Reflection.MethodBase m => [(m as System.Reflection.MethodInfo)?.ReturnType ?? typeof(void), .. m.GetParameters().Select(i => i.ParameterType)],
            System.Reflection.FieldInfo f => [f.FieldType],
            _ => []
        };
    }

    /// <summary>
    ///     Gets a type and the types it is made of, such as the arguments of a generic list.
    /// </summary>
    /// <param name="type">The type.</param>
    /// <returns>The type, its element type, and its generic arguments, all the way down.</returns>
    private static IEnumerable<Type> Expand(Type type)
    {
        yield return type;
        foreach (var inner in type.GetGenericArguments().Concat(type.HasElementType && type.GetElementType() is { } e ? [e] : []).SelectMany(Expand))
        {
            yield return inner;
        }
    }

    /// <summary>
    ///     Validates that what the cards of CanvasPilot say about the application is what the code says.
    /// </summary>
    [Fact]
    public void KnowledgeCatalog_Default_CanvasPilotCards_AgreeWithTheApplication()
    {
        // Arrange: the cards that state facts that the code defines
        var catalog = KnowledgeCatalog.Default;
        var root = catalog.Find("canvaspilot")?.Body;
        var layout = catalog.Find("canvaspilot/project-layout")?.Body;
        var marks = catalog.Find("canvaspilot/marks")?.Body;
        Assert.NotNull(root);
        Assert.NotNull(layout);
        Assert.NotNull(marks);

        // Act / Assert: the project file extension, the data folder, the attachments folder, the mark kinds, and the
        // reference forms of the message are stated as the code has them
        Assert.Contains($"extension `{ProjectDocument.FileExtension}`", root.Replace("\n", " ", StringComparison.Ordinal));
        Assert.Contains($"hidden data folder named `{ProjectDocument.DataFolderName}`", root.Replace("\n", " ", StringComparison.Ordinal));
        Assert.Contains($"data folder named `{ProjectDocument.DataFolderName}`", layout.Replace("\n", " ", StringComparison.Ordinal));
        Assert.Contains($"`{AttachmentStore.AttachmentsFolderName}` folder", layout);
        Assert.All(Enum.GetNames<MarkKind>(), kind => Assert.Contains($"`{kind.ToLowerInvariant()}`", marks));
        Assert.Contains("`[Mark n]`", marks);
        Assert.Contains("`[Attachment n: name]`", marks);
    }
}
