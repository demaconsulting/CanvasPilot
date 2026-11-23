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
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DemaConsulting.CanvasPilot.AgentSubsystem;
using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     The demonstration content that the screenshot tests draw: the project "Release Overview" with its pictures, the
///     conversation about it, the other recent projects of the welcome screen, and the style kits of the user. Everything
///     is made on the disk when it is needed and the content is the same on every run, so a new drawing differs from the
///     committed one only when the application changed.
/// </summary>
internal static class ScreenshotDemo
{
    /// <summary>
    ///     The name of the main demonstration project.
    /// </summary>
    public const string ProjectName = "Release Overview";

    /// <summary>
    ///     The name of the model that the provider pill shows.
    /// </summary>
    public const string ModelName = "claude-sonnet-4.5";

    /// <summary>
    ///     The picture of the main demonstration project that the main window shows.
    /// </summary>
    public const string MainPicture = "overview.svg";

    /// <summary>
    ///     The name of the project of the example screenshot.
    /// </summary>
    public const string ExampleProjectName = "Continuous Compliance";

    /// <summary>
    ///     The name of the model that the provider pill shows in the example screenshot.
    /// </summary>
    public const string ExampleModelName = "claude-opus-5.5";

    /// <summary>
    ///     The picture of the example project, which the copilot drew in a real session from one request: a one-slide overview
    ///     of a continuous-compliance pipeline.
    /// </summary>
    public const string ExamplePicture = "compliance-pipeline.svg";

    /// <summary>
    ///     The request of the user in the example screenshot, the same words that were sent to the real copilot.
    /// </summary>
    public const string ExampleRequest =
        "Create a one-slide overview (16:9, 1600×900) of a continuous-compliance pipeline for a regulated software team: "
        + "code commit → build and test → requirements trace → review evidence → release documents (PDF), with a short title, "
        + "one-line subtitle, clean icons per stage, and a small legend of the tools (ReqStream, ReviewMark, BuildMark, SonarMark). "
        + "Professional, corporate look.";

    /// <summary>
    ///     The time that the status bar of the main windows tells for the save of the project file, as a local wall-clock time
    ///     so that it is written the same in every time zone. A real save tells the time of the run, which would change the
    ///     drawing every minute.
    /// </summary>
    public static readonly DateTimeOffset SavedTime = new(new DateTime(2026, 3, 12, 14, 32, 0, DateTimeKind.Local));

    /// <summary>
    ///     The render time in milliseconds that the status bar of the main windows tells for the picture. A real render tells
    ///     the time that it took, which differs on every run.
    /// </summary>
    public const long RenderMilliseconds = 84;

    /// <summary>
    ///     The two marks of the main window, in the pixels of <see cref="MainPicture" />: the ellipse around the decision of
    ///     the flow and the rectangle around the two tallest bars.
    /// </summary>
    public static readonly (RenderPoint From, RenderPoint To) EllipseMark = (new RenderPoint(255, 163), new RenderPoint(424, 266));

    /// <summary>
    ///     The rectangle mark of the main window.
    /// </summary>
    public static readonly (RenderPoint From, RenderPoint To) RectangleMark = (new RenderPoint(727, 159), new RenderPoint(853, 417));

    /// <summary>
    ///     Creates a project and fills it with the files of the given writer.
    /// </summary>
    /// <param name="parentFolder">The folder that holds the project folder.</param>
    /// <param name="name">The project name.</param>
    /// <param name="fill">Writes the files into the project folder.</param>
    /// <returns>The project that was created.</returns>
    public static async Task<ProjectInfo> CreateProjectAsync(string parentFolder, string name, Action<string> fill)
    {
        Directory.CreateDirectory(parentFolder);
        var created = await new ProjectService().CreateAsync(parentFolder, name, TestContext.Current.CancellationToken);
        var project = created.Project ?? throw new InvalidOperationException($"The demonstration project {name} was not created.");
        fill(project.FolderPath);
        return project;
    }

    /// <summary>
    ///     Fixes the time that the status bar tells for the save of the open project: the modified time in the project file is
    ///     set to <see cref="SavedTime" /> and the project is opened again, so that the status bar shows the same text on every
    ///     run. It is called before the explorer and the preview are used, because opening the project again resets them.
    /// </summary>
    /// <param name="composition">The application, with the project open.</param>
    /// <param name="project">The project that is open.</param>
    /// <param name="cancellationToken">The token of the test.</param>
    /// <returns>A task that completes when the project is open again.</returns>
    public static async Task FixSavedTimeAsync(ApplicationComposition composition, ProjectInfo project, CancellationToken cancellationToken)
    {
        var node = JsonNode.Parse(await File.ReadAllTextAsync(project.FilePath, cancellationToken))
            ?? throw new InvalidOperationException("The project file is empty.");
        node["modifiedUtc"] = SavedTime.ToUniversalTime();
        await File.WriteAllTextAsync(project.FilePath, node.ToJsonString(), cancellationToken);
        await composition.Projects.OpenAsync(project.FilePath, cancellationToken);
    }

    /// <summary>
    ///     Fixes the render time that the preview pane tells in the status bar to <see cref="RenderMilliseconds" />. It is
    ///     called after the last render and before the window is drawn.
    /// </summary>
    /// <param name="composition">The application.</param>
    public static void FixRenderTime(ApplicationComposition composition)
    {
        composition.Preview.RenderMilliseconds = RenderMilliseconds;
    }

    /// <summary>
    ///     Writes the files of the main demonstration project: the overview, the two pictures it is made of, a logo, and a
    ///     one-page PDF.
    /// </summary>
    /// <param name="folder">The project folder.</param>
    public static void WriteReleaseOverview(string folder)
    {
        File.WriteAllText(Path.Combine(folder, MainPicture), OverviewSvg, Encoding.UTF8);
        File.WriteAllText(Path.Combine(folder, "downloads.vl.json"), DownloadsSpec, Encoding.UTF8);
        File.WriteAllText(Path.Combine(folder, "release-flow.mmd"), ReleaseFlow, Encoding.UTF8);

        var attachments = Directory.CreateDirectory(Path.Combine(folder, "attachments")).FullName;
        WriteLogo(Path.Combine(attachments, "logo.png"));
        File.WriteAllBytes(Path.Combine(attachments, "roadmap.pdf"), MinimalPdf("Roadmap"));
    }

    /// <summary>
    ///     Writes the file of the example project: the picture that the copilot drew, read from the resources of the test
    ///     assembly, where it is kept as the data of the screenshot.
    /// </summary>
    /// <param name="folder">The project folder.</param>
    public static void WriteExampleProject(string folder)
    {
        using var resource = typeof(ScreenshotDemo).Assembly.GetManifestResourceStream("ScreenshotData." + ExamplePicture)
            ?? throw new InvalidOperationException($"The screenshot data {ExamplePicture} is not in the test assembly.");
        using var file = File.Create(Path.Combine(folder, ExamplePicture));
        resource.CopyTo(file);
    }

    /// <summary>
    ///     Writes a project for the welcome screen: one main picture, whose small picture the card shows, and enough small
    ///     pictures behind it for the number of pictures that the card tells.
    /// </summary>
    /// <param name="folder">The project folder.</param>
    /// <param name="mainPicture">The SVG text of the main picture, which sorts first by name.</param>
    /// <param name="pictures">The number of pictures that the project holds.</param>
    public static void WriteWelcomeProject(string folder, string mainPicture, int pictures)
    {
        // A card shows the picture that was changed last, so the small pictures get an older time and the main one a newer
        // time that is set explicitly: a file that is written a moment later can carry the same time stamp, and the card
        // would then show one of the plain small pictures, which is a blank card
        var written = DateTime.UtcNow;
        for (var number = 2; number <= pictures; number++)
        {
            var small = Path.Combine(folder, $"z-{number}.svg");
            File.WriteAllText(small, SmallPicture, Encoding.UTF8);
            File.SetLastWriteTimeUtc(small, written.AddMinutes(-10));
        }

        var main = Path.Combine(folder, "a-main.svg");
        File.WriteAllText(main, mainPicture, Encoding.UTF8);
        File.SetLastWriteTimeUtc(main, written);
    }

    /// <summary>
    ///     Writes the two style kits of the user into the folder that the catalog reads the user's kits from.
    /// </summary>
    /// <param name="userFolder">The folder of the user's style kits.</param>
    public static void WriteUserKits(string userFolder)
    {
        WriteKit(userFolder, "paper", "Paper", "Warm paper and ink for printed reports.", "#FBF7F0", "#FFFFFF", "#1F2937", "#6B7280", "#B45309", "#0F766E", "#BE123C");
        WriteKit(userFolder, "team-blue", "Team Blue", "The calm blue look of the product team.", "#F8FAFC", "#E2E8F0", "#0F172A", "#475569", "#2563EB", "#0891B2", "#F59E0B");
    }

    /// <summary>
    ///     Makes the provider pill of a window show a provider that is ready, with the model of the demonstration. A
    ///     composition without the copilot can never be ready, so the pill is given a panel of its own whose copilot says
    ///     that it is.
    /// </summary>
    /// <param name="composition">The application.</param>
    /// <param name="modelName">The name of the model that the pill shows.</param>
    /// <returns>The things that the pill is built over, which the caller disposes when the window is closed.</returns>
    public static async Task<IDisposable> ShowReadyProviderAsync(ApplicationComposition composition, string modelName = ModelName)
    {
        composition.Settings.Update(settings => settings with
        {
            AiProvider = settings.AiProvider with { Provider = AiProviderKind.GitHubCopilot, ModelName = modelName },
        });

        var marks = new MarkRegistry();
        var dictation = new ScriptedDictationService();
        var composer = new ComposerViewModel(new TextPayloadBuilder(), marks, dictation);
        var annotation = new AnnotationOverlayViewModel(marks, new FixedPictureSource(), _ => { });
        var chat = new ChatPanelViewModel(composer, marks, annotation, new StubMarkImageComposer(), dictation, agent: new ScriptedAgentService());
        await chat.RefreshReadinessAsync();
        composition.Shell.ProviderPill = new ProviderPillViewModel(chat, composition.Settings, composition.Shell.OpenSettingsCommand);
        return new Owned(chat, annotation, composer);
    }

    /// <summary>
    ///     Puts the conversation of the demonstration into the transcript of the chat: two requests, the tools that the copilot
    ///     used, and its answers.
    /// </summary>
    /// <param name="chat">The chat panel.</param>
    /// <param name="projectFolder">The folder of the project.</param>
    public static void FillTranscript(ChatPanelViewModel chat, string projectFolder)
    {
        var today = DateTime.Today;
        var clock = new ManualClock();

        chat.Transcript.Add(UserRow("Draw our release flow as a Mermaid diagram.", today.AddHours(10).AddMinutes(41), projectFolder));
        chat.Transcript.Add(ToolRow("project_write", "release-flow.mmd", clock, TimeSpan.Zero));
        chat.Transcript.Add(ToolRow("render_image", "release-flow.mmd", clock, TimeSpan.Zero));
        chat.Transcript.Add(new AssistantReplyViewModel(
            "I drew a five-step flow: Plan, Build, Tests pass?, Package MSI, and Release. A failed test loops back to Build.",
            AssistantReplyState.Completed));

        chat.Transcript.Add(UserRow("Put it beside the downloads chart on one page.", today.AddHours(10).AddMinutes(43), projectFolder));
        chat.Transcript.Add(ToolRow("project_read", "downloads.vl.json", clock, TimeSpan.Zero));
        chat.Transcript.Add(ToolRow("render_image", "overview.svg", clock, TimeSpan.FromMilliseconds(800)));
        chat.Transcript.Add(new AssistantReplyViewModel(
            "overview.svg now shows the flow and the downloads chart side by side. Mark what you want changed, then tell me what to do.",
            AssistantReplyState.Completed));
    }

    /// <summary>
    ///     Puts the conversation of the example screenshot into the transcript of the chat, as the real session showed it: one
    ///     request, the ten tools that the copilot used (with the titles and times that the rows showed), its short answer, and
    ///     the line under the answer that says what the turn cost, made as the program makes it, from the figures that the
    ///     provider reported (134,094 tokens in, 15,512 out, 5,032 reasoning, 15 premium requests, 2 min 37 s).
    /// </summary>
    /// <param name="chat">The chat panel.</param>
    /// <param name="projectFolder">The folder of the project.</param>
    public static void FillExampleTranscript(ChatPanelViewModel chat, string projectFolder)
    {
        var clock = new ManualClock();

        chat.Transcript.Add(UserRow(ExampleRequest, DateTime.Today.AddHours(14).AddMinutes(12), projectFolder));
        chat.Transcript.Add(ToolRow("style_get", string.Empty, clock, TimeSpan.FromMilliseconds(600)));
        chat.Transcript.Add(ToolRow("canvasnet_help", string.Empty, clock, TimeSpan.FromMilliseconds(600)));
        chat.Transcript.Add(ToolRow("canvasnet_help", string.Empty, clock, TimeSpan.FromMilliseconds(600)));
        chat.Transcript.Add(ToolRow("canvasnet_help", string.Empty, clock, TimeSpan.Zero));
        var written = ToolRow("project_write", ExamplePicture, clock, TimeSpan.Zero);
        written.ReportFile(ToolActivityText.Written(ExamplePicture, replaced: false));
        chat.Transcript.Add(written);
        chat.Transcript.Add(ToolRow("render_image", ExamplePicture, clock, TimeSpan.FromMilliseconds(2500)));
        chat.Transcript.Add(ToolRow("project_read", ExamplePicture, clock, TimeSpan.Zero));
        chat.Transcript.Add(ToolRow("style_check", string.Empty, clock, TimeSpan.Zero));
        var replaced = ToolRow("project_write", ExamplePicture, clock, TimeSpan.Zero);
        replaced.ReportFile(ToolActivityText.Written(ExamplePicture, replaced: true));
        chat.Transcript.Add(replaced);
        chat.Transcript.Add(ToolRow("render_image", ExamplePicture, clock, TimeSpan.FromMilliseconds(300)));

        var reply = new AssistantReplyViewModel(
            "I made compliance-pipeline.svg (1600 × 900): five stage cards with icons, a title, a loop line, and the tool "
            + "legend. Mark anything you want changed.",
            AssistantReplyState.Completed);
        var usage = new TurnUsage();
        usage.Add(new AgentEvent.Usage(134_094, 15_512, ReasoningTokens: 5_032, Multiplier: 15, Model: ExampleModelName));
        reply.SetUsageCaption(usage.Format(TimeSpan.FromSeconds(157), stopped: false));
        chat.Transcript.Add(reply);
    }

    /// <summary>
    ///     Makes the row of a message that the user sent.
    /// </summary>
    /// <param name="text">The words of the message.</param>
    /// <param name="sent">The local time at which the message was sent.</param>
    /// <param name="projectFolder">The folder of the project.</param>
    /// <returns>The row.</returns>
    private static TranscriptEntryViewModel UserRow(string text, DateTime sent, string projectFolder)
    {
        var snapshot = new ComposerSnapshot([new TextSegment(text, TextOrigin.Typed)]);
        var message = new SentMessage(snapshot, [], [], new object(), new DateTimeOffset(sent));
        return new TranscriptEntryViewModel(message, projectFolder, () => null);
    }

    /// <summary>
    ///     Makes the row of a tool that finished.
    /// </summary>
    /// <param name="tool">The name of the tool.</param>
    /// <param name="file">The file that the tool worked on, or an empty text for a tool that works on no file.</param>
    /// <param name="clock">The clock that times the call.</param>
    /// <param name="took">How long the call took.</param>
    /// <returns>The row.</returns>
    private static ToolActivityRowViewModel ToolRow(string tool, string file, ManualClock clock, TimeSpan took)
    {
        var argument = file.Length == 0 ? string.Empty : $"path={file}";
        var row = new ToolActivityRowViewModel(Guid.NewGuid().ToString("N"), tool, argument, ToolActivityText.Started(tool, argument), clock);
        clock.Advance(took);
        row.Finish(ToolActivityText.Finished(tool, argument, succeeded: true, string.Empty), succeeded: true, hasImage: tool == "render_image");
        return row;
    }

    /// <summary>
    ///     Writes one style kit of the user, in the file format of the program.
    /// </summary>
    /// <param name="userFolder">The folder of the user's style kits.</param>
    /// <param name="id">The identifier of the kit, which is also its folder name.</param>
    /// <param name="name">The name of the kit.</param>
    /// <param name="description">The description of the kit.</param>
    /// <param name="colors">The background, surface, text, muted, primary, secondary, and accent colors.</param>
    private static void WriteKit(string userFolder, string id, string name, string description, params string[] colors)
    {
        var folder = Directory.CreateDirectory(Path.Combine(userFolder, id)).FullName;
        var json = $$"""
            {
              "formatVersion": 1,
              "id": "{{id}}",
              "name": "{{name}}",
              "description": "{{description}}",
              "palette": {
                "background": "{{colors[0]}}",
                "surface": "{{colors[1]}}",
                "text": "{{colors[2]}}",
                "muted": "{{colors[3]}}",
                "primary": "{{colors[4]}}",
                "secondary": "{{colors[5]}}",
                "accent": "{{colors[6]}}"
              },
              "fonts": { "heading": "sans-serif", "body": "sans-serif", "monospace": "monospace" },
              "stroke": { "width": 2 },
              "cornerRadius": 8,
              "spacing": 16,
              "examples": {
                "examples/title-card.svg": "A title card with a heading, a line of text, and an accent bar.",
                "examples/flow.mmd": "A flowchart of the release steps.",
                "examples/chart.vl.json": "A bar chart of the downloads per month."
              }
            }
            """;
        File.WriteAllText(Path.Combine(folder, "style.json"), json, Encoding.UTF8);

        // The examples of the kit are drawn as small pictures in the manager
        var examples = Directory.CreateDirectory(Path.Combine(folder, "examples")).FullName;
        File.WriteAllText(Path.Combine(examples, "title-card.svg"), TitleCard(colors), Encoding.UTF8);
        File.WriteAllText(Path.Combine(examples, "flow.mmd"), ReleaseFlow, Encoding.UTF8);
        File.WriteAllText(Path.Combine(examples, "chart.vl.json"), DownloadsSpec, Encoding.UTF8);
    }

    /// <summary>
    ///     Makes the title card example of a kit, in the colors of the kit.
    /// </summary>
    /// <param name="colors">The background, surface, text, muted, primary, secondary, and accent colors.</param>
    /// <returns>The SVG text.</returns>
    private static string TitleCard(string[] colors) =>
        $"""
        <svg xmlns="http://www.w3.org/2000/svg" width="640" height="360" viewBox="0 0 640 360" font-family="Arial, Helvetica, sans-serif">
          <rect width="640" height="360" fill="{colors[0]}"/>
          <rect x="60" y="210" width="120" height="8" rx="4" fill="{colors[6]}"/>
          <text x="60" y="170" font-size="44" font-weight="bold" fill="{colors[2]}">Release 1.0</text>
          <text x="60" y="260" font-size="20" fill="{colors[3]}">How we ship, and who is using it</text>
          <circle cx="520" cy="150" r="70" fill="{colors[4]}"/>
          <circle cx="570" cy="210" r="40" fill="{colors[5]}"/>
        </svg>
        """;

    /// <summary>
    ///     Draws the logo of the demonstration project as a PNG file: a rounded square with a gradient.
    /// </summary>
    /// <param name="path">The file to write.</param>
    private static void WriteLogo(string path)
    {
        var logo = new Border
        {
            Width = 128,
            Height = 128,
            CornerRadius = new CornerRadius(28),
            Background = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
                GradientStops = [new GradientStop(Color.Parse("#4F46E5"), 0), new GradientStop(Color.Parse("#06B6D4"), 0.6), new GradientStop(Color.Parse("#F472B6"), 1)],
            },
        };
        logo.Measure(new Size(128, 128));
        logo.Arrange(new Rect(0, 0, 128, 128));
        using var bitmap = new RenderTargetBitmap(new PixelSize(128, 128));
        bitmap.Render(logo);
        bitmap.Save(path, new PngBitmapEncoderOptions());
    }

    /// <summary>
    ///     Makes a PDF file of one page that holds a line of text.
    /// </summary>
    /// <param name="title">The line of text.</param>
    /// <returns>The bytes of the file.</returns>
    private static byte[] MinimalPdf(string title)
    {
        var content = $"BT /F1 32 Tf 72 700 Td ({title}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];

        // Write the objects, remembering where each starts, and close with the table that lists the places
        var text = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var index = 0; index < objects.Length; index++)
        {
            offsets.Add(text.Length);
            text.Append(index + 1).Append(" 0 obj\n").Append(objects[index]).Append("\nendobj\n");
        }

        var tableStart = text.Length;
        text.Append("xref\n0 ").Append(objects.Length + 1).Append("\n0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            text.Append(offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture)).Append(" 00000 n \n");
        }

        text.Append("trailer\n<< /Size ").Append(objects.Length + 1).Append(" /Root 1 0 R >>\nstartxref\n").Append(tableStart).Append("\n%%EOF\n");
        return Encoding.ASCII.GetBytes(text.ToString());
    }

    /// <summary>
    ///     The overview picture: the release flow and the downloads chart side by side on one page.
    /// </summary>
    public const string OverviewSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="900" height="500" viewBox="0 0 900 500" font-family="Arial, Helvetica, sans-serif">
          <rect width="900" height="500" fill="#ffffff"/>
          <text x="40" y="52" font-size="28" font-weight="bold" fill="#1e293b">Release 1.0 at a glance</text>
          <text x="40" y="80" font-size="14" fill="#64748b">How we ship, and who is using it</text>

          <rect x="30" y="120" width="422" height="340" rx="14" fill="#f8fafc" stroke="#e2e8f0"/>
          <text x="50" y="152" font-size="13" font-weight="bold" fill="#1e293b">Release flow</text>
          <defs>
            <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto-start-reverse">
              <path d="M0,0 L10,5 L0,10 z" fill="#475569"/>
            </marker>
          </defs>
          <g stroke="#475569" stroke-width="1" fill="none" marker-end="url(#arrow)">
            <path d="M132,221 H158"/>
            <path d="M240,221 H283"/>
            <path d="M340,258 V316"/>
            <path d="M291,338 H246"/>
            <path d="M340,184 V170 H200 V200" marker-end="url(#arrow)"/>
          </g>
          <text x="262" y="166" font-size="10" fill="#64748b">no</text>
          <text x="347" y="292" font-size="10" fill="#64748b">yes</text>
          <g font-size="11" text-anchor="middle">
            <rect x="52" y="202" width="80" height="38" rx="8" fill="#eef2ff" stroke="#4f46e5"/>
            <text x="92" y="226" fill="#3730a3">Plan</text>
            <rect x="160" y="202" width="80" height="38" rx="8" fill="#eef2ff" stroke="#4f46e5"/>
            <text x="200" y="226" fill="#3730a3">Build</text>
            <path d="M340,184 L395,221 L340,258 L285,221 z" fill="#ecfeff" stroke="#0891b2"/>
            <text x="340" y="225" fill="#155e75">Tests pass?</text>
            <rect x="293" y="319" width="94" height="38" rx="8" fill="#eef2ff" stroke="#4f46e5"/>
            <text x="340" y="343" fill="#3730a3">Package MSI</text>
            <rect x="160" y="319" width="84" height="38" rx="8" fill="#4f46e5"/>
            <text x="202" y="343" fill="#ffffff">Release</text>
          </g>

          <rect x="470" y="120" width="400" height="340" rx="14" fill="#f8fafc" stroke="#e2e8f0"/>
          <text x="490" y="152" font-size="13" font-weight="bold" fill="#1e293b">Downloads per month</text>
          <g font-size="11" fill="#94a3b8" text-anchor="end">
            <text x="527" y="414">0</text>
            <text x="527" y="354">200</text>
            <text x="527" y="294">400</text>
            <text x="527" y="234">600</text>
            <text x="527" y="174">800</text>
          </g>
          <g stroke="#e2e8f0">
            <path d="M535,170 H858"/>
            <path d="M535,230 H858"/>
            <path d="M535,290 H858"/>
            <path d="M535,350 H858"/>
            <path d="M535,410 H858"/>
          </g>
          <g fill="#5046e5">
            <rect x="545" y="353" width="40" height="57"/>
            <rect x="610" y="317" width="40" height="93"/>
            <rect x="674" y="269" width="40" height="141"/>
            <rect x="738" y="215" width="40" height="195"/>
            <rect x="803" y="173" width="40" height="237"/>
          </g>
          <g font-size="11" fill="#94a3b8" text-anchor="middle">
            <text x="565" y="429">Jul</text>
            <text x="630" y="429">Aug</text>
            <text x="694" y="429">Sep</text>
            <text x="758" y="429">Oct</text>
            <text x="823" y="429">Nov</text>
          </g>
        </svg>
        """;

    /// <summary>
    ///     The chart of the downloads, as a Vega-Lite specification.
    /// </summary>
    private const string DownloadsSpec = """
        {
          "$schema": "https://vega.github.io/schema/vega-lite/v5.json",
          "width": 360,
          "height": 200,
          "title": "Downloads per month",
          "data": {
            "values": [
              { "month": "Jul", "downloads": 190 },
              { "month": "Aug", "downloads": 310 },
              { "month": "Sep", "downloads": 470 },
              { "month": "Oct", "downloads": 650 },
              { "month": "Nov", "downloads": 790 }
            ]
          },
          "mark": "bar",
          "encoding": {
            "x": { "field": "month", "type": "ordinal", "sort": null },
            "y": { "field": "downloads", "type": "quantitative" }
          }
        }
        """;

    /// <summary>
    ///     The release flow, as a Mermaid diagram.
    /// </summary>
    private const string ReleaseFlow = """
        flowchart LR
            Plan --> Build --> Tests{Tests pass?}
            Tests -- no --> Build
            Tests -- yes --> Package[Package MSI] --> Release
        """;

    /// <summary>
    ///     A small picture that fills the projects of the welcome screen with the pictures that their cards count.
    /// </summary>
    private const string SmallPicture = """
        <svg xmlns="http://www.w3.org/2000/svg" width="200" height="120"><rect width="200" height="120" fill="#eef2ff"/></svg>
        """;

    /// <summary>
    ///     The picture of the "Quarterly Review" project: bars with a trend line.
    /// </summary>
    public const string ReviewSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="640" height="360" viewBox="0 0 640 360">
          <rect width="640" height="360" fill="#ffffff"/>
          <g fill="#06b6d4">
            <rect x="40" y="200" width="56" height="130"/>
            <rect x="140" y="160" width="56" height="170"/>
            <rect x="240" y="180" width="56" height="150"/>
            <rect x="340" y="100" width="56" height="230"/>
            <rect x="440" y="130" width="56" height="200"/>
          </g>
          <polyline points="30,150 120,100 220,110 320,50 420,70 520,60" fill="none" stroke="#db2777" stroke-width="5" stroke-linejoin="round"/>
          <path d="M30,330 H560" stroke="#cbd5e1"/>
        </svg>
        """;

    /// <summary>
    ///     The picture of the "Architecture Sketch" project: three boxes joined by lines.
    /// </summary>
    public const string ArchitectureSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="640" height="360" viewBox="0 0 640 360" font-family="Arial, Helvetica, sans-serif" font-size="18" text-anchor="middle">
          <rect width="640" height="360" fill="#f8fafc"/>
          <g stroke="#475569" stroke-width="2" fill="none">
            <path d="M320,100 V150 M320,150 H170 V220 M320,150 H470 V220"/>
          </g>
          <rect x="250" y="50" width="140" height="50" rx="12" fill="#eef2ff" stroke="#4f46e5" stroke-width="2"/>
          <text x="320" y="82" fill="#3730a3">Web app</text>
          <rect x="100" y="220" width="140" height="50" rx="12" fill="#eef2ff" stroke="#4f46e5" stroke-width="2"/>
          <text x="170" y="252" fill="#3730a3">API</text>
          <rect x="400" y="220" width="140" height="50" rx="12" fill="#ecfeff" stroke="#0891b2" stroke-width="2"/>
          <text x="470" y="252" fill="#155e75">Database</text>
        </svg>
        """;

    /// <summary>
    ///     The picture of the "Onboarding Flow" project: three steps in a row.
    /// </summary>
    public const string OnboardingSvg = """
        <svg xmlns="http://www.w3.org/2000/svg" width="640" height="360" viewBox="0 0 640 360" font-family="Arial, Helvetica, sans-serif" font-size="18" text-anchor="middle">
          <rect width="640" height="360" fill="#f8fafc"/>
          <defs>
            <marker id="arrow" viewBox="0 0 10 10" refX="9" refY="5" markerWidth="6" markerHeight="6" orient="auto">
              <path d="M0,0 L10,5 L0,10 z" fill="#475569"/>
            </marker>
          </defs>
          <g stroke="#475569" stroke-width="2" marker-end="url(#arrow)">
            <path d="M190,180 H235"/>
            <path d="M390,180 H435"/>
          </g>
          <rect x="60" y="150" width="130" height="60" rx="30" fill="#ecfeff" stroke="#0891b2" stroke-width="2"/>
          <text x="125" y="186" fill="#155e75">Hire</text>
          <rect x="240" y="150" width="150" height="60" rx="30" fill="#ecfeff" stroke="#0891b2" stroke-width="2"/>
          <text x="315" y="186" fill="#155e75">Train</text>
          <rect x="440" y="150" width="140" height="60" rx="30" fill="#0e7490"/>
          <text x="510" y="186" fill="#ffffff">Ship</text>
        </svg>
        """;

    /// <summary>
    ///     A clock that only moves when it is told to, so that the time a tool call took is the time the demonstration says.
    /// </summary>
    private sealed class ManualClock : TimeProvider
    {
        /// <summary>
        ///     The time stamp now, in ticks.
        /// </summary>
        private long _ticks = 1_000_000;

        /// <inheritdoc />
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        /// <inheritdoc />
        public override long GetTimestamp() => _ticks;

        /// <summary>
        ///     Moves the clock on.
        /// </summary>
        /// <param name="time">How far to move it.</param>
        public void Advance(TimeSpan time) => _ticks += time.Ticks;
    }

    /// <summary>
    ///     The parts of the ready provider pill, released together.
    /// </summary>
    private sealed class Owned : IDisposable
    {
        /// <summary>
        ///     The things to release, in order.
        /// </summary>
        private readonly IDisposable[] _parts;

        /// <summary>
        ///     Initializes a new instance of the <see cref="Owned" /> class.
        /// </summary>
        /// <param name="parts">The things to release, in order.</param>
        public Owned(params IDisposable[] parts)
        {
            _parts = parts;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            foreach (var part in _parts)
            {
                part.Dispose();
            }
        }
    }
}
