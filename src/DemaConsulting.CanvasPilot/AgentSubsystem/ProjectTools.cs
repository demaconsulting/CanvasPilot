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
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     The tools with which the copilot works on the files of the project: <c>project_list</c>, <c>project_read</c>, and
///     <c>project_write</c> (the pack <c>project</c>), and <c>render_image</c> (the pack <c>render</c>). Every tool is
///     confined to the project folder by the path policy that the tool registry gives the packs, and the tools themselves
///     refuse the links that the policy does not see.
/// </summary>
/// <remarks>
///     <para>
///         <b>How it plugs in.</b> The method <see cref="CreatePacks" /> is the supplier of project packs that
///         <see cref="AgentToolRegistry" /> takes in its constructor, so the registry offers these tools after the drawing
///         knowledge and the style tools. The application composition passes a <see cref="ProjectTools" /> (and handles
///         <see cref="FileWritten" />) when it builds the registry.
///     </para>
///     <para>
///         <b>Overwriting is direct.</b> <c>project_write</c> creates or replaces a file at once, with no backup and no
///         question: users keep their drawings in source control, and the application keeps no backup folder. The answer
///         of the tool and the event <see cref="FileWritten" /> make every change visible instead.
///     </para>
///     <para>
///         <b>Privacy.</b> The log gets the name of the tool, the project-relative path, a size, and the outcome of each
///         call, and never a file's content or the words of a message. Nothing is written outside the files that the model
///         is asked to write: <c>render_image</c> keeps the picture in memory.
///     </para>
///     <para>
///         The class holds no state besides its services and the event, and is safe for concurrent use as far as the
///         services are. <see cref="FileWritten" /> is raised on the thread that ran the tool.
///     </para>
/// </remarks>
public sealed class ProjectTools
{
    /// <summary>
    ///     The family prefix of the pack with the tools that list, read, and write files.
    /// </summary>
    public const string ProjectPrefix = "project";

    /// <summary>
    ///     The family prefix of the pack with the tool that draws pictures.
    /// </summary>
    public const string RenderPrefix = "render";

    /// <summary>
    ///     The name of the tool that lists the files of the project.
    /// </summary>
    public const string ListToolName = ProjectFilesToolPack.ListToolName;

    /// <summary>
    ///     The name of the tool that reads a text file of the project.
    /// </summary>
    public const string ReadToolName = ProjectFilesToolPack.ReadToolName;

    /// <summary>
    ///     The name of the tool that creates or replaces a text file of the project.
    /// </summary>
    public const string WriteToolName = ProjectFilesToolPack.WriteToolName;

    /// <summary>
    ///     The name of the tool that draws a picture.
    /// </summary>
    public const string RenderToolName = RenderImageToolPack.RenderToolName;

    /// <summary>
    ///     The most bytes that a written file may have: 256 KiB.
    /// </summary>
    public const int MaxWriteBytes = ProjectFilesToolPack.MaxWriteBytes;

    /// <summary>
    ///     The most pixels on the longer side of a picture that <c>render_image</c> returns.
    /// </summary>
    public const int MaxImageEdge = RenderImageToolPack.MaxEdge;

    /// <summary>
    ///     The two packs of the tools; they hold no project, so one set serves every project.
    /// </summary>
    private readonly IToolPack[] _packs;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProjectTools" /> class.
    /// </summary>
    /// <param name="renderService">Draws image sources and encodes the pictures; nothing is saved.</param>
    /// <param name="activeStyle">
    ///     Gives the active style kit, whose example images <c>render_image</c> may draw; <see langword="null" /> for none.
    /// </param>
    /// <param name="imageSourceFilter">Tells which files CanvasNet draws; <see langword="null" /> for the filter of the application.</param>
    /// <param name="logger">Receives the metadata of every call; <see langword="null" /> to log nothing.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="renderService" /> is <see langword="null" />.</exception>
    public ProjectTools(
        IImageRenderService renderService,
        IActiveStyle? activeStyle = null,
        IImageSourceFilter? imageSourceFilter = null,
        RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(renderService);

        var kinds = new ProjectFileKinds(imageSourceFilter ?? new CanvasNetImageSourceFilter());
        var log = new ToolCallLog(logger);
        _packs =
        [
            new ProjectFilesToolPack(kinds, log, OnFileWritten),
            new RenderImageToolPack(renderService, activeStyle, kinds, log),
        ];
    }

    /// <summary>
    ///     Occurs after the copilot created or replaced a file of the project, so that the shell can select the file in the
    ///     project explorer. It is raised on the thread that ran the tool, not on the thread of the user interface, and a
    ///     handler that throws does not fail the tool.
    /// </summary>
    public event EventHandler<FileWrittenEventArgs>? FileWritten;

    /// <summary>
    ///     Makes the packs of the project tools. The packs are given to <see cref="AgentToolRegistry" /> as its supplier of
    ///     project packs; the registry gives them the path policy that confines them to the project folder.
    /// </summary>
    /// <param name="context">The project that the tools are built for; the registry builds the policy from its folder.</param>
    /// <returns>The pack with the file tools and the pack with the picture tool.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context" /> is <see langword="null" />.</exception>
    public IEnumerable<IToolPack> CreatePacks(AgentToolContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return _packs;
    }

    /// <summary>
    ///     Raises <see cref="FileWritten" />.
    /// </summary>
    /// <param name="args">What was written.</param>
    private void OnFileWritten(FileWrittenEventArgs args)
    {
        FileWritten?.Invoke(this, args);
    }
}
