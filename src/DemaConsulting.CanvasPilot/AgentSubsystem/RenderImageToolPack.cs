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

using System.ComponentModel;
using System.Globalization;
using System.Text;
using DemaConsulting.AgentKit.Core;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;
using Microsoft.Extensions.AI;

namespace DemaConsulting.CanvasPilot.AgentSubsystem;

/// <summary>
///     The tool pack with the tool that lets the model look at a picture: <c>render_image</c> draws an image source or a
///     picture of the project, or an example image of the active style kit, and returns it as a PNG.
/// </summary>
/// <remarks>
///     <para>
///         <b>No file is written.</b> The render service draws into memory and encodes the PNG into memory (verified in
///         <see cref="IImageRenderService" />), so the picture goes straight into the answer. Nothing is saved in the
///         project folder, in the data folder, or anywhere else.
///     </para>
///     <para>
///         <b>What may be drawn.</b> A file of the project, by a path that passes the checks of
///         <see cref="ProjectPathGuard" /> (and so cannot leave the project or pass through a link), or a file inside the
///         folder of the active style kit given by its full path, which is how <c>style_get</c> shows the example images.
///         The kit grant is read at the time of the call, so a changed style applies at once; it covers the kit folder only
///         and image sources only, and the viewer is told that the kit folder is the only place the picture may read from.
///         Only files that CanvasNet draws are accepted.
///     </para>
///     <para>
///         <b>What comes back.</b> A PNG whose longer side is at most <see cref="MaxEdge" /> pixels (smaller pictures are
///         not enlarged), and a text with the size, the format, and every note of the renderer. A picture that cannot be
///         drawn is an ordinary answer with the code, the message, and the hint of the renderer, so the model can fix the
///         source; the tool does not throw. The pack is safe for concurrent use.
///     </para>
/// </remarks>
internal sealed class RenderImageToolPack : IToolPack
{
    /// <summary>
    ///     The most pixels that the longer side of the returned picture has.
    /// </summary>
    internal const int MaxEdge = 1280;

    /// <summary>
    ///     The name of the tool.
    /// </summary>
    internal const string RenderToolName = "render_image";

    /// <summary>
    ///     The most notes of the renderer that the answer repeats; the rest is counted.
    /// </summary>
    private const int MaxNotes = 50;

    /// <summary>
    ///     The longer sides to try, largest first, when a picture does not fit the limit for the size of an answer.
    /// </summary>
    private static readonly int[] EdgeSteps = [MaxEdge, 960, 640, 320];

    /// <summary>
    ///     Draws the files and encodes the pictures.
    /// </summary>
    private readonly IImageRenderService _renderService;

    /// <summary>
    ///     Gives the active style kit, or <see langword="null" /> when examples of kits are not offered.
    /// </summary>
    private readonly IActiveStyle? _activeStyle;

    /// <summary>
    ///     Tells which files are image sources.
    /// </summary>
    private readonly ProjectFileKinds _kinds;

    /// <summary>
    ///     Receives the call lines of the log.
    /// </summary>
    private readonly ToolCallLog _log;

    /// <summary>
    ///     Initializes a new instance of the <see cref="RenderImageToolPack" /> class.
    /// </summary>
    /// <param name="renderService">Draws the files and encodes the pictures.</param>
    /// <param name="activeStyle">Gives the active style kit; <see langword="null" /> when examples of kits are not offered.</param>
    /// <param name="kinds">Tells which files are image sources.</param>
    /// <param name="log">Receives the metadata of every call.</param>
    internal RenderImageToolPack(IImageRenderService renderService, IActiveStyle? activeStyle, ProjectFileKinds kinds, ToolCallLog log)
    {
        _renderService = renderService;
        _activeStyle = activeStyle;
        _kinds = kinds;
        _log = log;
    }

    /// <inheritdoc />
    public string FamilyPrefix => ProjectTools.RenderPrefix;

    /// <inheritdoc />
    /// <remarks>
    ///     No ability is required: a model that cannot see still gets the size, the format, and the notes of the renderer,
    ///     and the application decides what a model without vision is offered elsewhere.
    /// </remarks>
    public HostCapabilities RequiredCapabilities => HostCapabilities.None;

    /// <inheritdoc />
    public IEnumerable<AIFunction> CreateTools(PathPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        return
        [
            GuardedToolFactory.Create(
                async ([Description("The image source or picture to draw: a path relative to the project folder, such as 'cover.svg', or the full path of an example image of the style kit, as style_get shows it.")] string path,
                    CancellationToken cancellationToken)
                    => await RenderAsync(policy, path, cancellationToken).ConfigureAwait(false),
                RenderToolName,
                "Draws an image source (SVG, Mermaid, Vega-Lite, PlantUML, OpenSCAD, and others) or a picture file of the project and returns the picture, scaled to at most "
                + MaxEdge.ToString(CultureInfo.InvariantCulture)
                + " pixels on its longer side, with the size, the format, and every note of the renderer. Look at the picture after each change. Nothing is saved.")
                .AsGuarded(policy.Limits),
        ];
    }

    /// <summary>
    ///     Draws a file and builds the answer.
    /// </summary>
    /// <param name="policy">The path policy of the project.</param>
    /// <param name="request">The path as the model sent it.</param>
    /// <param name="cancellationToken">Cancels the render when the turn is stopped.</param>
    /// <returns>The picture with its description, or the reason there is none.</returns>
    private async Task<object> RenderAsync(PathPolicy policy, string? request, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Decide which file, in which folder the viewer may read from, or why not
            var target = Resolve(policy, request, out var refusal);
            if (target is null)
            {
                _log.Record(RenderToolName, null, null, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.PathNotPermitted, refusal);
            }

            var (full, relative, root, isKit) = target.Value;
            var shown = isKit ? $"style kit example '{relative}'" : $"'{relative}'";
            if (!File.Exists(full))
            {
                _log.Record(RenderToolName, relative, null, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.TargetNotFound, $"There is no file {shown}. Use project_list to see the files.");
            }

            if (!_kinds.IsImageSource(full))
            {
                _log.Record(RenderToolName, relative, null, ToolCallLog.Refused);
                return ToolResult.Denied(DenialReason.UnsupportedMediaType, $"{shown} is not an image source that can be drawn.");
            }

            // The render is a normal answer whatever its outcome; a stopped turn is the one exception
            var result = await _renderService.RenderAsync(full, root, cancellationToken).ConfigureAwait(false);
            if (result.IsCancelled)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _log.Record(RenderToolName, relative, null, ToolCallLog.Failed);
                return ToolResult.Text("The picture was not drawn because the render was canceled.");
            }

            if (result.Image is null)
            {
                var error = result.Error;
                _log.Record(RenderToolName, relative, null, $"{ToolCallLog.Failed} ({error?.Code ?? "unknown"})");
                return ToolResult.Text(DescribeFailure(shown, error));
            }

            return Answer(policy, shown, relative, result.Image);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The turn was stopped: that is not a failure of the tool
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _log.Record(RenderToolName, null, null, ToolCallLog.Failed);
            return ToolResult.Text($"The picture could not be drawn ({ex.GetType().Name}).");
        }
    }

    /// <summary>
    ///     Scales and encodes a drawn picture and describes it.
    /// </summary>
    /// <param name="policy">The path policy, for the limit of the size of an answer.</param>
    /// <param name="shown">How the file is named in the text.</param>
    /// <param name="relative">The path of the file that is logged.</param>
    /// <param name="image">The drawn picture.</param>
    /// <returns>The picture with its description.</returns>
    private object Answer(PathPolicy policy, string shown, string relative, RenderedImage image)
    {
        // Shrink in steps until the PNG fits the ceiling for an answer; most pictures fit at the first step
        RenderedImage scaled = image;
        byte[] png = [];
        foreach (var edge in EdgeSteps)
        {
            scaled = ImageScaler.FitWithin(image, edge);
            png = _renderService.EncodePng(scaled);
            if (png.Length <= policy.Limits.MaxBinaryBytes)
            {
                break;
            }
        }

        if (png.Length > policy.Limits.MaxBinaryBytes)
        {
            _log.Record(RenderToolName, relative, png.Length, ToolCallLog.Refused);
            return ToolResult.Denied(DenialReason.ResourceTooLarge, $"The picture of {shown} is too large to return even when it is made small.");
        }

        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"Drawn {shown} ({image.Format}), {image.Width} x {image.Height} pixels.");
        if (!ReferenceEquals(scaled, image))
        {
            text.Append(CultureInfo.InvariantCulture, $" The picture is scaled down to {scaled.Width} x {scaled.Height} pixels for you.");
        }

        // Every note of the renderer, because a note is often the reason a part of the picture is missing
        if (image.Diagnostics.Count == 0)
        {
            text.Append(" The renderer reported no notes.");
        }
        else
        {
            text.Append(" Notes of the renderer:");
            foreach (var note in image.Diagnostics.Take(MaxNotes))
            {
                text.Append(CultureInfo.InvariantCulture, $"\n- {note.DisplayText}");
            }

            if (image.Diagnostics.Count > MaxNotes)
            {
                text.Append(CultureInfo.InvariantCulture, $"\n- and {image.Diagnostics.Count - MaxNotes} more.");
            }
        }

        _log.Record(RenderToolName, relative, png.Length, ToolCallLog.Done);
        return ToolResult.Image(png, "image/png", text.ToString());
    }

    /// <summary>
    ///     Finds the file to draw: a file of the project, or a file of the folder of the active style kit.
    /// </summary>
    /// <param name="policy">The path policy of the project.</param>
    /// <param name="request">The path as the model sent it.</param>
    /// <param name="refusal">Receives the reason when no file is found.</param>
    /// <returns>The file, its short name, the folder the viewer may read from, and whether it is a kit file; or <see langword="null" />.</returns>
    private (string Full, string Relative, string Root, bool IsKit)? Resolve(PathPolicy policy, string? request, out string refusal)
    {
        // The project comes first, so a kit that is part of the project is read as the project's own file
        if (ProjectPathGuard.TryResolve(policy, request, false, out var full, out var relative, out var projectRefusal))
        {
            refusal = string.Empty;
            return (full, relative, policy.WorkingDirectory, false);
        }

        // The example images of the active kit are read-only and are named by their full path, outside the project
        var kitFolder = _activeStyle?.Active?.FolderPath;
        if (kitFolder is not null && !string.IsNullOrWhiteSpace(request) && Path.IsPathFullyQualified(request))
        {
            string candidate;
            try
            {
                candidate = Path.GetFullPath(request);
            }
            catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
            {
                refusal = projectRefusal;
                return null;
            }

            if (ProjectPathGuard.IsInside(kitFolder, candidate))
            {
                if (ProjectPathGuard.TryResolveUnder(kitFolder, candidate, out full, out relative, out var kitRefusal))
                {
                    refusal = string.Empty;
                    return (full, relative, Path.GetFullPath(kitFolder), true);
                }

                refusal = kitRefusal;
                return null;
            }
        }

        refusal = projectRefusal;
        return null;
    }

    /// <summary>
    ///     Describes a render that failed, with the code, the message, and the hint of the renderer.
    /// </summary>
    /// <param name="shown">How the file is named in the text.</param>
    /// <param name="error">The error of the renderer.</param>
    /// <returns>The sentence for the model.</returns>
    private static string DescribeFailure(string shown, RenderError? error)
    {
        if (error is null)
        {
            return $"{shown} could not be drawn, and the renderer gave no reason.";
        }

        var text = $"{shown} could not be drawn. Error {error.Code}: {error.Message}";
        return string.IsNullOrWhiteSpace(error.Hint) ? text : $"{text} Hint: {error.Hint}";
    }
}
