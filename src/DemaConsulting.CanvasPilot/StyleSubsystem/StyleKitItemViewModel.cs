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

using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     One kit in the list of the Style Kits window and, once chosen, everything that the preview beside the list shows
///     about it: the colors with their roles, the fonts, the numbers, the advice, the export sizes, the warnings of the
///     reader, the text that an assistant would be given and how long it is, and the small pictures of the examples.
/// </summary>
/// <remarks>
///     The values are made once, when the item is made, from the entry as it was found. The small pictures are filled in
///     later by the window's view model, because drawing them takes time.
/// </remarks>
public sealed partial class StyleKitItemViewModel : ObservableObject
{
    /// <summary>
    ///     The number of colors that the strip in the list shows.
    /// </summary>
    public const int StripLength = 6;

    /// <summary>
    ///     The thumbnails of the examples.
    /// </summary>
    private readonly ObservableCollection<StyleThumbnailViewModel> _thumbnails = [];

    /// <summary>
    ///     Whether the item is the one that is chosen in the list.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    ///     Whether the open project uses the kit.
    /// </summary>
    [ObservableProperty]
    private bool _isInUse;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitItemViewModel" /> class.
    /// </summary>
    /// <param name="entry">The kit as it was found.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="entry" /> is <see langword="null" />.</exception>
    public StyleKitItemViewModel(StyleKitEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        Entry = entry;
        var kit = entry.Kit;
        Swatches = kit.Palette.Select(pair => new StyleSwatch(pair.Key, pair.Value, LabelColor(pair.Value))).ToList();
        StripColors = kit.Palette.Values.Take(StripLength).ToList();
        FontLines =
        [
            $"Headings: {kit.Fonts.Heading}",
            $"Body: {kit.Fonts.Body}",
            $"Monospace: {kit.Fonts.Monospace}"
        ];
        NumbersText = string.Create(
            CultureInfo.InvariantCulture,
            $"Line width {kit.StrokeWidth:0.##} px, corner radius {kit.CornerRadius:0.##} px, spacing {kit.Spacing:0.##} px");
        MermaidText = DescribeMermaid(kit.Mermaid);
        VegaLiteText = kit.VegaLiteConfigJson is null
            ? "No Vega-Lite configuration."
            : string.Create(CultureInfo.InvariantCulture, $"Vega-Lite configuration of {kit.VegaLiteConfigJson.Length} characters.");
        PresetLines = kit.SizePresets.Select(DescribePreset).ToList();
        Warnings = entry.Warnings.Select(issue => issue.ToString()).ToList();
        GuidanceText = StyleGuidance.Build(entry);
        GuidanceSizeText = string.Create(
            CultureInfo.InvariantCulture,
            $"{GuidanceText.Length:N0} of {StyleGuidance.BudgetCharacters:N0} characters");
        Thumbnails = new ReadOnlyObservableCollection<StyleThumbnailViewModel>(_thumbnails);
    }

    /// <summary>
    ///     Gets the kit as it was found.
    /// </summary>
    public StyleKitEntry Entry { get; }

    /// <summary>
    ///     Gets the id of the kit.
    /// </summary>
    public string Id => Entry.Kit.Id;

    /// <summary>
    ///     Gets the name of the kit.
    /// </summary>
    public string Name => Entry.Kit.Name;

    /// <summary>
    ///     Gets what the kit is for, or an empty string.
    /// </summary>
    public string Description => Entry.Kit.Description;

    /// <summary>
    ///     Gets the place of the kit in words: <c>project</c>, <c>my kits</c>, <c>shared</c>, or <c>built-in</c>.
    /// </summary>
    public string PlaceText => StylePickerViewModel.PlaceText(Entry.Location);

    /// <summary>
    ///     Gets where the kit is, in a sentence that has the folder.
    /// </summary>
    public string LocationText => $"From {PlaceText}: {Entry.FolderPath}";

    /// <summary>
    ///     Gets a value indicating whether the application does not change the kit: it comes with the program or is shared.
    /// </summary>
    public bool IsReadOnly => Entry.IsReadOnly;

    /// <summary>
    ///     Gets a value indicating whether a kit of a more local place with the same id hides this one.
    /// </summary>
    public bool IsOverridden => Entry.IsOverridden;

    /// <summary>
    ///     Gets the sentence that says what hides the kit, or an empty string when nothing does.
    /// </summary>
    public string OverriddenText => Entry.IsOverridden ? $"Hidden by the kit with the same id in {Entry.OverriddenBy}." : string.Empty;

    /// <summary>
    ///     Gets the sentence that says that the kit cannot be changed, or an empty string when it can.
    /// </summary>
    public string ReadOnlyText => Entry.Location switch
    {
        StyleKitLocation.BuiltIn => "This kit comes with the program and cannot be changed. Duplicate it to change a copy.",
        StyleKitLocation.Shared => "This kit is shared by a team and cannot be changed here. Duplicate it to change a copy.",
        _ => string.Empty
    };

    /// <summary>
    ///     Gets the opacity of the card in the list: dimmed for a kit that is hidden by another.
    /// </summary>
    public double CardOpacity => Entry.IsOverridden ? 0.5 : 1;

    /// <summary>
    ///     Gets the first colors of the palette, for the strip in the list.
    /// </summary>
    public IReadOnlyList<string> StripColors { get; }

    /// <summary>
    ///     Gets every color of the palette with its role.
    /// </summary>
    public IReadOnlyList<StyleSwatch> Swatches { get; }

    /// <summary>
    ///     Gets the three font lists as lines of text.
    /// </summary>
    public IReadOnlyList<string> FontLines { get; }

    /// <summary>
    ///     Gets the line width, corner radius, and spacing as a line of text.
    /// </summary>
    public string NumbersText { get; }

    /// <summary>
    ///     Gets what the kit has for Mermaid.
    /// </summary>
    public string MermaidText { get; }

    /// <summary>
    ///     Gets what the kit has for Vega-Lite.
    /// </summary>
    public string VegaLiteText { get; }

    /// <summary>
    ///     Gets the written advice of the kit.
    /// </summary>
    public string Notes => Entry.Kit.Guidance.Notes;

    /// <summary>
    ///     Gets a value indicating whether the kit has written advice.
    /// </summary>
    public bool HasNotes => Notes.Length > 0;

    /// <summary>
    ///     Gets the things that the pictures should do.
    /// </summary>
    public IReadOnlyList<string> DoItems => Entry.Kit.Guidance.Do;

    /// <summary>
    ///     Gets the things that the pictures should not do.
    /// </summary>
    public IReadOnlyList<string> DontItems => Entry.Kit.Guidance.Dont;

    /// <summary>
    ///     Gets the export sizes as lines of text.
    /// </summary>
    public IReadOnlyList<string> PresetLines { get; }

    /// <summary>
    ///     Gets a value indicating whether the kit has export sizes.
    /// </summary>
    public bool HasPresets => PresetLines.Count > 0;

    /// <summary>
    ///     Gets the warnings that reading the kit gave, as lines of text.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    /// <summary>
    ///     Gets a value indicating whether reading the kit gave warnings.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    ///     Gets the text that an assistant would be given about the kit.
    /// </summary>
    public string GuidanceText { get; }

    /// <summary>
    ///     Gets how long the text for an assistant is, with the most that it may be.
    /// </summary>
    public string GuidanceSizeText { get; }

    /// <summary>
    ///     Gets the small pictures of the examples, which are filled in after the kit is chosen.
    /// </summary>
    public ReadOnlyObservableCollection<StyleThumbnailViewModel> Thumbnails { get; }

    /// <summary>
    ///     Gets a value indicating whether the kit has example images.
    /// </summary>
    public bool HasExamples => Entry.Kit.Examples.Count > 0;

    /// <summary>
    ///     Tells whether another item is the same kit in the same place.
    /// </summary>
    /// <param name="other">The other item, or <see langword="null" />.</param>
    /// <returns><see langword="true" /> when both stand for the kit in the same folder.</returns>
    public bool IsSameKitAs(StyleKitItemViewModel? other)
    {
        return other is not null
               && other.Entry.Location == Entry.Location
               && string.Equals(other.Entry.FolderPath, Entry.FolderPath, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Makes one waiting thumbnail for each example, replacing the ones that there were.
    /// </summary>
    /// <returns>The thumbnails, in the order of the examples of the kit.</returns>
    public IReadOnlyList<StyleThumbnailViewModel> PrepareThumbnails()
    {
        _thumbnails.Clear();
        foreach (var example in Entry.Kit.Examples)
        {
            _thumbnails.Add(new StyleThumbnailViewModel(Path.GetFileName(example.RelativePath), example.Caption ?? string.Empty));
        }

        return _thumbnails.ToList();
    }

    /// <summary>
    ///     Describes the Mermaid part of a kit in a line.
    /// </summary>
    /// <param name="mermaid">The part, or <see langword="null" /> when the kit has none.</param>
    /// <returns>The line.</returns>
    private static string DescribeMermaid(StyleMermaid? mermaid)
    {
        if (mermaid is null)
        {
            return "No Mermaid theme.";
        }

        var count = mermaid.ThemeVariables.Count;
        var noun = count == 1 ? "variable" : "variables";
        return string.Create(CultureInfo.InvariantCulture, $"Mermaid theme {mermaid.Theme} with {count} {noun}.");
    }
    /// <summary>
    ///     Describes an export size in a line.
    /// </summary>
    /// <param name="preset">The size.</param>
    /// <returns>The line, for example <c>Slide: 1920 x 1080 px</c>.</returns>
    private static string DescribePreset(StyleSizePreset preset)
    {
        var size = (preset.Width, preset.Height) switch
        {
            ({ } width, { } height) => $"{width} x {height} px",
            ({ } width, null) => $"{width} px wide",
            (null, { } height) => $"{height} px high",
            _ => "natural size"
        };
        var extras = new List<string>();
        if (preset.Dpi is { } dpi)
        {
            extras.Add(string.Create(CultureInfo.InvariantCulture, $"{dpi:0.##} dpi"));
        }

        if (preset.Transparent)
        {
            extras.Add("transparent");
        }

        return extras.Count == 0 ? $"{preset.Name}: {size}" : $"{preset.Name}: {size}, {string.Join(", ", extras)}";
    }

    /// <summary>
    ///     Chooses black or white for a label that is drawn on a color, whichever has the greater contrast.
    /// </summary>
    /// <param name="hex">The color, <c>#RRGGBB</c>.</param>
    /// <returns>The color of the label.</returns>
    private static string LabelColor(string hex)
    {
        return StyleColors.TryNormalize(hex, out var normalized) && StyleColors.Contrast(normalized, "#FFFFFF") >= StyleColors.Contrast(normalized, "#000000")
            ? "#FFFFFF"
            : "#000000";
    }
}
