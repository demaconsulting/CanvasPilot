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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.UtilitiesSubsystem;

namespace DemaConsulting.CanvasPilot.StyleSubsystem;

/// <summary>
///     The editor of one style kit of the user or of the project: a draft of every part of the kit that the user changes,
///     the problems of the draft as the same messages that the reader of the files gives, and a save that refuses a draft
///     with an error.
/// </summary>
/// <remarks>
///     <para>
///         The draft is a copy: nothing on disk changes until <see cref="SaveCommand" /> runs, and canceling leaves the
///         kit exactly as it was. The files of the examples are changed only by a save: the files that the user chose are
///         copied into the <c>examples</c> folder, the kit file is written, and then the files that the user removed are
///         deleted, in that order, so a failure on the way never leaves a kit file that names a file that is not there.
///     </para>
///     <para>
///         The identifier of a kit is the name of its folder, so it is shown and cannot be changed; to give a kit another
///         id, duplicate it. The view model is used on the thread that owns the user interface.
///     </para>
/// </remarks>
public sealed partial class StyleKitEditorViewModel : ObservableObject
{
    /// <summary>
    ///     The names of the roles that the palette usually has, which a new color takes in turn.
    /// </summary>
    private static readonly string[] StandardRoles = ["background", "text", "primary", "secondary", "accent", "surface", "muted"];

    /// <summary>
    ///     The properties that the editor derives from the draft; a change of one of them is not a change of the draft.
    /// </summary>
    private static readonly HashSet<string> DerivedProperties = new(StringComparer.Ordinal)
    {
        nameof(Errors), nameof(HasErrors), nameof(Warnings), nameof(HasWarnings), nameof(CanSave), nameof(IsDirty),
        nameof(SaveError), nameof(HasSaveError), nameof(ExampleMessage), nameof(HasExampleMessage), nameof(ContrastText)
    };

    /// <summary>
    ///     The Mermaid themes that a kit may name.
    /// </summary>
    private static readonly IReadOnlyList<string> Themes = StyleKitSerializer.MermaidThemes;

    /// <summary>
    ///     The catalog that the kit is written through.
    /// </summary>
    private readonly IStyleKitCatalog _catalog;

    /// <summary>
    ///     The kit as it was found when the editor was opened.
    /// </summary>
    private readonly StyleKitEntry _entry;

    /// <summary>
    ///     Optional logger.
    /// </summary>
    private readonly RollingFileLogger? _logger;

    /// <summary>
    ///     The names of the example files that the kit had when the editor was opened.
    /// </summary>
    private readonly HashSet<string> _originalExampleNames;

    /// <summary>
    ///     What the draft looked like when the editor was opened, to tell whether it changed.
    /// </summary>
    private string _originalSignature = string.Empty;

    /// <summary>
    ///     Whether the draft is being checked, so that the properties that the check sets are not taken for edits.
    /// </summary>
    private bool _suspended = true;

    /// <summary>
    ///     The name of the kit.
    /// </summary>
    [ObservableProperty]
    private string _name;

    /// <summary>
    ///     What the kit is for.
    /// </summary>
    [ObservableProperty]
    private string _description;

    /// <summary>
    ///     The font list of the headings.
    /// </summary>
    [ObservableProperty]
    private string _headingFont;

    /// <summary>
    ///     The font list of the body text.
    /// </summary>
    [ObservableProperty]
    private string _bodyFont;

    /// <summary>
    ///     The font list of the monospaced text.
    /// </summary>
    [ObservableProperty]
    private string _monospaceFont;

    /// <summary>
    ///     The usual line width in pixels.
    /// </summary>
    [ObservableProperty]
    private decimal _strokeWidth;

    /// <summary>
    ///     The usual corner radius in pixels.
    /// </summary>
    [ObservableProperty]
    private decimal _cornerRadius;

    /// <summary>
    ///     The usual gap between elements in pixels.
    /// </summary>
    [ObservableProperty]
    private decimal _spacing;

    /// <summary>
    ///     Whether the kit has a Mermaid theme.
    /// </summary>
    [ObservableProperty]
    private bool _useMermaid;

    /// <summary>
    ///     The Mermaid theme.
    /// </summary>
    [ObservableProperty]
    private string _mermaidTheme;

    /// <summary>
    ///     The Vega-Lite configuration as JSON text; empty for none.
    /// </summary>
    [ObservableProperty]
    private string _vegaLiteConfigText;

    /// <summary>
    ///     The written advice.
    /// </summary>
    [ObservableProperty]
    private string _notes;

    /// <summary>
    ///     The errors of the draft, which stop it from being saved.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasErrors), nameof(CanSave))]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private IReadOnlyList<string> _errors = [];

    /// <summary>
    ///     The warnings of the draft, which do not stop it from being saved.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarnings))]
    private IReadOnlyList<string> _warnings = [];

    /// <summary>
    ///     Whether the draft differs from the kit that was opened.
    /// </summary>
    [ObservableProperty]
    private bool _isDirty;

    /// <summary>
    ///     Why the last save failed, or an empty string.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaveError))]
    private string _saveError = string.Empty;

    /// <summary>
    ///     What is wrong with the example files that the user chose last, or an empty string.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExampleMessage))]
    private string _exampleMessage = string.Empty;

    /// <summary>
    ///     How readable the text is on the background, or an empty string while a color is not valid.
    /// </summary>
    [ObservableProperty]
    private string _contrastText = string.Empty;

    /// <summary>
    ///     Initializes a new instance of the <see cref="StyleKitEditorViewModel" /> class with a draft of a kit.
    /// </summary>
    /// <param name="catalog">The catalog that the kit is written through.</param>
    /// <param name="entry">The kit to edit, which must be one of the user or of the project.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="catalog" /> or <paramref name="entry" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the kit is read only: it comes with the program or is shared.</exception>
    public StyleKitEditorViewModel(IStyleKitCatalog catalog, StyleKitEntry entry, RollingFileLogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.IsReadOnly || entry.Location is not (StyleKitLocation.User or StyleKitLocation.Project))
        {
            throw new InvalidOperationException($"The kit '{entry.Kit.Name}' is read only; duplicate it to change a copy.");
        }

        _catalog = catalog;
        _entry = entry;
        _logger = logger;

        var kit = entry.Kit;
        _name = kit.Name;
        _description = kit.Description;
        _headingFont = kit.Fonts.Heading;
        _bodyFont = kit.Fonts.Body;
        _monospaceFont = kit.Fonts.Monospace;
        _strokeWidth = (decimal)kit.StrokeWidth;
        _cornerRadius = (decimal)kit.CornerRadius;
        _spacing = (decimal)kit.Spacing;
        _useMermaid = kit.Mermaid is not null;
        _mermaidTheme = kit.Mermaid?.Theme ?? "base";
        _vegaLiteConfigText = Indent(kit.VegaLiteConfigJson);
        _notes = kit.Guidance.Notes;

        foreach (var (role, hex) in kit.Palette)
        {
            PaletteRows.Add(new StyleColorRowViewModel(role, hex, StyleKitSerializer.RequiredRoles.Contains(role)));
        }

        foreach (var (variable, value) in kit.Mermaid?.ThemeVariables ?? new Dictionary<string, string>())
        {
            MermaidVariables.Add(new StyleNameValueRowViewModel(variable, value));
        }

        foreach (var item in kit.Guidance.Do)
        {
            DoItems.Add(new StyleTextRowViewModel(item));
        }

        foreach (var item in kit.Guidance.Dont)
        {
            DontItems.Add(new StyleTextRowViewModel(item));
        }

        foreach (var preset in kit.SizePresets)
        {
            Presets.Add(new StylePresetRowViewModel(preset.Name, preset.Width, preset.Height, preset.Dpi, preset.Transparent));
        }

        foreach (var example in kit.Examples)
        {
            Examples.Add(new StyleExampleRowViewModel(Path.GetFileName(example.RelativePath), example.Caption ?? string.Empty, example.FullPath, isNew: false));
        }

        _originalExampleNames = new HashSet<string>(Examples.Select(row => row.FileName), StringComparer.OrdinalIgnoreCase);
        Track(PaletteRows);
        Track(MermaidVariables);
        Track(DoItems);
        Track(DontItems);
        Track(Presets);
        Track(Examples);

        _suspended = false;
        _originalSignature = Signature();
        Revalidate();
    }

    /// <summary>
    ///     Raised when the editor asks its window to close, after a successful save or a cancel.
    /// </summary>
    public event EventHandler? CloseRequested;

    /// <summary>
    ///     Gets or sets what lets the user choose example files, or <see langword="null" /> when nothing does, which leaves
    ///     the Add Files command with nothing to do. The argument is the folder to start in, or <see langword="null" />.
    /// </summary>
    public Func<string?, Task<IReadOnlyList<string>>>? PickExampleFiles { get; set; }

    /// <summary>
    ///     Gets the identifier of the kit, which is the name of its folder and cannot be changed.
    /// </summary>
    public string Id => _entry.Kit.Id;

    /// <summary>
    ///     Gets the words that say where the kit is kept.
    /// </summary>
    public string LocationText => $"{StylePickerViewModel.PlaceText(_entry.Location)}: {_entry.FolderPath}";

    /// <summary>
    ///     Gets the colors of the palette.
    /// </summary>
    public ObservableCollection<StyleColorRowViewModel> PaletteRows { get; } = [];

    /// <summary>
    ///     Gets the Mermaid themes that can be chosen.
    /// </summary>
    public static IReadOnlyList<string> MermaidThemes => Themes;

    /// <summary>
    ///     Gets the variables of the Mermaid theme.
    /// </summary>
    public ObservableCollection<StyleNameValueRowViewModel> MermaidVariables { get; } = [];

    /// <summary>
    ///     Gets the things that the pictures should do.
    /// </summary>
    public ObservableCollection<StyleTextRowViewModel> DoItems { get; } = [];

    /// <summary>
    ///     Gets the things that the pictures should not do.
    /// </summary>
    public ObservableCollection<StyleTextRowViewModel> DontItems { get; } = [];

    /// <summary>
    ///     Gets the export sizes.
    /// </summary>
    public ObservableCollection<StylePresetRowViewModel> Presets { get; } = [];

    /// <summary>
    ///     Gets the example images.
    /// </summary>
    public ObservableCollection<StyleExampleRowViewModel> Examples { get; } = [];

    /// <summary>
    ///     Gets a value indicating whether the draft has an error.
    /// </summary>
    public bool HasErrors => Errors.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether the draft has a warning.
    /// </summary>
    public bool HasWarnings => Warnings.Count > 0;

    /// <summary>
    ///     Gets a value indicating whether the draft can be saved, which it can while it has no error.
    /// </summary>
    public bool CanSave => !HasErrors;

    /// <summary>
    ///     Gets a value indicating whether the last save failed.
    /// </summary>
    public bool HasSaveError => SaveError.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether there is something to say about the example files that were chosen last.
    /// </summary>
    public bool HasExampleMessage => ExampleMessage.Length > 0;

    /// <summary>
    ///     Gets a value indicating whether the kit was saved.
    /// </summary>
    public bool Saved { get; private set; }

    /// <summary>
    ///     Gets the kit as it was found after the save, or <see langword="null" /> when it was not saved.
    /// </summary>
    public StyleKitEntry? SavedEntry { get; private set; }

    /// <summary>
    ///     Builds the kit that the draft describes, as it would be written.
    /// </summary>
    /// <remarks>
    ///     The result is not checked: a draft with an error gives a kit that has the error. Lines that are blank in the lists
    ///     of advice and of theme variables are left out, and a number of an export size that is not a number is left out of
    ///     its size.
    /// </remarks>
    /// <returns>The kit.</returns>
    public StyleKit BuildKit()
    {
        return Build(null);
    }

    /// <summary>
    ///     Adds files as examples, which are copied into the kit when it is saved.
    /// </summary>
    /// <remarks>
    ///     A file that is not there, or that is larger than the limit of an example, is not added; what is wrong with it is
    ///     in <see cref="ExampleMessage" />. A file that has the name of an example already gets a number in its name.
    /// </remarks>
    /// <param name="paths">The full paths of the files.</param>
    /// <returns>The number of files that were added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="paths" /> is <see langword="null" />.</exception>
    public int AddExampleFiles(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        var problems = new List<string>();
        var added = 0;
        foreach (var path in paths)
        {
            var full = Path.GetFullPath(path);
            var fileName = Path.GetFileName(full);
            var info = new FileInfo(full);
            if (!info.Exists)
            {
                problems.Add($"'{fileName}' was not found.");
            }
            else if (info.Length > StyleKitSerializer.MaxExampleBytes)
            {
                problems.Add($"'{fileName}' is larger than {StyleKitSerializer.MaxExampleBytes / (1024 * 1024)} MiB, which is the limit of an example.");
            }
            else
            {
                var unique = UniqueExampleName(fileName, Examples.Select(row => row.FileName));
                Examples.Add(new StyleExampleRowViewModel(unique, string.Empty, full, isNew: true));
                added++;
            }
        }

        ExampleMessage = string.Join(" ", problems);
        return added;
    }

    /// <summary>
    ///     Adds a color to the palette: the next of the usual roles that the palette does not have, or a numbered name.
    /// </summary>
    [RelayCommand]
    private void AddColor()
    {
        var taken = new HashSet<string>(PaletteRows.Select(row => row.Role.Trim()), StringComparer.Ordinal);
        var role = StandardRoles.FirstOrDefault(candidate => !taken.Contains(candidate));
        var number = 1;
        while (role is null)
        {
            var candidate = $"color-{number.ToString(CultureInfo.InvariantCulture)}";
            role = taken.Contains(candidate) ? null : candidate;
            number++;
        }

        PaletteRows.Add(new StyleColorRowViewModel(role, "#808080", isRequired: false));
    }

    /// <summary>
    ///     Removes a color from the palette. A color of a role that every kit needs stays.
    /// </summary>
    /// <param name="row">The color to remove.</param>
    [RelayCommand]
    private void RemoveColor(StyleColorRowViewModel? row)
    {
        if (row is { IsRequired: false })
        {
            PaletteRows.Remove(row);
        }
    }

    /// <summary>
    ///     Adds a theme variable to the Mermaid part.
    /// </summary>
    [RelayCommand]
    private void AddMermaidVariable()
    {
        MermaidVariables.Add(new StyleNameValueRowViewModel(string.Empty, string.Empty));
    }

    /// <summary>
    ///     Removes a theme variable from the Mermaid part.
    /// </summary>
    /// <param name="row">The variable to remove.</param>
    [RelayCommand]
    private void RemoveMermaidVariable(StyleNameValueRowViewModel? row)
    {
        if (row is not null)
        {
            MermaidVariables.Remove(row);
        }
    }

    /// <summary>
    ///     Adds a line to the list of things that the pictures should do.
    /// </summary>
    [RelayCommand]
    private void AddDo()
    {
        DoItems.Add(new StyleTextRowViewModel(string.Empty));
    }

    /// <summary>
    ///     Adds a line to the list of things that the pictures should not do.
    /// </summary>
    [RelayCommand]
    private void AddDont()
    {
        DontItems.Add(new StyleTextRowViewModel(string.Empty));
    }

    /// <summary>
    ///     Removes a line from the list that holds it.
    /// </summary>
    /// <param name="row">The line to remove.</param>
    [RelayCommand]
    private void RemoveGuidanceItem(StyleTextRowViewModel? row)
    {
        if (row is not null && !DoItems.Remove(row))
        {
            DontItems.Remove(row);
        }
    }

    /// <summary>
    ///     Adds an export size that the user fills in.
    /// </summary>
    [RelayCommand]
    private void AddPreset()
    {
        var taken = new HashSet<string>(Presets.Select(row => row.Name.Trim()), StringComparer.OrdinalIgnoreCase);
        var name = "New size";
        var number = 2;
        while (taken.Contains(name))
        {
            name = $"New size {number.ToString(CultureInfo.InvariantCulture)}";
            number++;
        }

        Presets.Add(new StylePresetRowViewModel(name, 1280, 720, null, false));
    }

    /// <summary>
    ///     Removes an export size.
    /// </summary>
    /// <param name="row">The size to remove.</param>
    [RelayCommand]
    private void RemovePreset(StylePresetRowViewModel? row)
    {
        if (row is not null)
        {
            Presets.Remove(row);
        }
    }

    /// <summary>
    ///     Asks the user for example files and adds them.
    /// </summary>
    /// <returns>A task that completes when the files were added, or at once when nothing asks.</returns>
    [RelayCommand]
    private async Task AddExamplesAsync()
    {
        if (PickExampleFiles is null)
        {
            return;
        }

        var chosen = await PickExampleFiles(null);
        if (chosen.Count > 0)
        {
            AddExampleFiles(chosen);
        }
    }

    /// <summary>
    ///     Removes an example; its file is deleted only when the kit is saved.
    /// </summary>
    /// <param name="row">The example to remove.</param>
    [RelayCommand]
    private void RemoveExample(StyleExampleRowViewModel? row)
    {
        if (row is not null)
        {
            Examples.Remove(row);
            ExampleMessage = string.Empty;
        }
    }

    /// <summary>
    ///     Writes the kit: copies the chosen example files in, saves the kit file, and deletes the example files that were
    ///     removed. Does nothing while the draft has an error. A failure is in <see cref="SaveError" /> and leaves the
    ///     editor open.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        Revalidate();
        if (!CanSave)
        {
            return;
        }

        SaveError = string.Empty;
        var kit = BuildKit();
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(_entry.FolderPath)));
        var copied = new List<string>();
        try
        {
            var kitFolder = parent is null ? _entry.FolderPath : PathHelpers.SafePathCombine(parent, kit.Id);
            var examplesFolder = Path.Combine(kitFolder, StyleKitSerializer.ExamplesFolderName);
            var examples = CopyExamples(examplesFolder, copied);

            var saved = _catalog.Save(kit with { Examples = examples }, _entry.Location);
            DeleteRemovedExamples(examplesFolder);

            Saved = true;
            SavedEntry = saved;
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or NotSupportedException)
        {
            // The files that this save copied are taken away again, so a failed save leaves the folder as it was
            foreach (var file in copied)
            {
                TryDelete(file);
            }

            Log(LogLevel.Warning, $"The style kit '{kit.Id}' could not be saved: {ex.Message}");
            SaveError = $"The kit could not be saved: {ex.Message}";
        }
    }

    /// <summary>
    ///     Leaves the editor without saving; nothing on disk changes.
    /// </summary>
    [RelayCommand]
    private void Cancel()
    {
        Saved = false;
        SavedEntry = null;
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    ///     Checks the draft again after anything but a derived value changed.
    /// </summary>
    /// <param name="e">The event data.</param>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (!_suspended && e.PropertyName is { } name && !DerivedProperties.Contains(name))
        {
            Revalidate();
        }
    }

    /// <summary>
    ///     Builds the kit of the draft and collects what is wrong with the draft that the kit itself cannot show.
    /// </summary>
    /// <param name="issues">Receives the problems of the draft, or <see langword="null" /> to leave them out.</param>
    /// <returns>The kit.</returns>
    private StyleKit Build(List<StyleIssue>? issues)
    {
        var palette = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in PaletteRows)
        {
            var role = row.Role.Trim();
            if (role.Length == 0)
            {
                issues?.Add(new StyleIssue("palette", "a color has no name; give every color a name.", true));
            }
            else if (!palette.TryAdd(role, row.Hex.Trim()))
            {
                issues?.Add(new StyleIssue($"palette.{role}", "is used for more than one color; every color needs its own name.", true));
            }
        }

        StyleMermaid? mermaid = null;
        if (UseMermaid)
        {
            var variables = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in MermaidVariables.Where(row => row.Name.Trim().Length > 0 || row.Value.Trim().Length > 0))
            {
                var variable = row.Name.Trim();
                if (variable.Length == 0)
                {
                    issues?.Add(new StyleIssue("mermaid.themeVariables", "a variable has a value but no name.", true));
                }
                else if (!variables.TryAdd(variable, row.Value.Trim()))
                {
                    issues?.Add(new StyleIssue($"mermaid.themeVariables.{variable}", "is set more than once.", true));
                }
            }

            mermaid = new StyleMermaid(MermaidTheme, variables);
        }

        var presets = new List<StyleSizePreset>();
        for (var i = 0; i < Presets.Count; i++)
        {
            var problems = new List<(string Property, string Message)>();
            presets.Add(Presets[i].ToPreset(problems));
            foreach (var (property, message) in problems)
            {
                issues?.Add(new StyleIssue($"sizePresets[{i.ToString(CultureInfo.InvariantCulture)}].{property}", message, true));
            }
        }

        var examples = Examples
            .Select(row => new StyleExample(
                $"{StyleKitSerializer.ExamplesFolderName}/{row.FileName.Trim()}",
                row.SourcePath,
                row.Caption.Trim().Length == 0 ? null : row.Caption.Trim()))
            .ToList();

        return new StyleKit(
            Id,
            Name,
            Description,
            palette,
            new StyleFonts(HeadingFont, BodyFont, MonospaceFont),
            (double)StrokeWidth,
            (double)CornerRadius,
            (double)Spacing,
            mermaid,
            CompactVegaLite(VegaLiteConfigText),
            new StyleGuidanceText(
                Notes,
                DoItems.Select(row => row.Text.Trim()).Where(text => text.Length > 0).ToList(),
                DontItems.Select(row => row.Text.Trim()).Where(text => text.Length > 0).ToList()),
            presets,
            examples);
    }

    /// <summary>
    ///     Checks the draft and sets the errors, the warnings, the hint about contrast, and whether the draft changed.
    /// </summary>
    private void Revalidate()
    {
        var issues = new List<StyleIssue>();
        var kit = Build(issues);
        var numbered = new HashSet<string>(
            issues.Select(issue => issue.Path).Where(path => path.StartsWith("sizePresets[", StringComparison.Ordinal)).Select(PresetOf),
            StringComparer.Ordinal);

        // A number that could not be read is said once, where it is; the check of the kit would only say that a size is missing
        issues.AddRange(StyleKitSerializer.Validate(kit).Where(issue => !numbered.Contains(issue.Path)));
        Errors = issues.Where(issue => issue.IsError).Select(issue => issue.ToString()).Distinct(StringComparer.Ordinal).ToList();
        Warnings = issues.Where(issue => !issue.IsError).Select(issue => issue.ToString()).Distinct(StringComparer.Ordinal).ToList();
        ContrastText = DescribeContrast(kit);
        IsDirty = !string.Equals(Signature(), _originalSignature, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Gets the part of a path of a property that names a size, for example <c>sizePresets[1]</c> of
    ///     <c>sizePresets[1].width</c>.
    /// </summary>
    /// <param name="path">The path of the property.</param>
    /// <returns>The path of the size.</returns>
    private static string PresetOf(string path)
    {
        var dot = path.IndexOf('.', StringComparison.Ordinal);
        return dot < 0 ? path : path[..dot];
    }

    /// <summary>
    ///     Describes how readable the text is on the background.
    /// </summary>
    /// <param name="kit">The kit of the draft.</param>
    /// <returns>A sentence, or an empty string when a color is not valid.</returns>
    private static string DescribeContrast(StyleKit kit)
    {
        if (!kit.Palette.TryGetValue("background", out var background) || !kit.Palette.TryGetValue("text", out var text)
            || !StyleColors.TryNormalize(background, out var back) || !StyleColors.TryNormalize(text, out var front))
        {
            return string.Empty;
        }

        var ratio = StyleColors.Contrast(back, front);
        var shown = ratio.ToString("0.0", CultureInfo.InvariantCulture);
        return ratio >= 4.5
            ? $"Text on the background: {shown} : 1, which reads well."
            : $"Text on the background: {shown} : 1, which is low; 4.5 : 1 or more reads well.";
    }

    /// <summary>
    ///     Makes a text that stands for the draft, which differs when anything that is saved differs.
    /// </summary>
    /// <returns>The text.</returns>
    private string Signature()
    {
        try
        {
            var examples = string.Join('|', Examples.Select(row => $"{row.FileName}>{row.SourcePath}>{row.IsNew}"));
            return StyleKitSerializer.Write(BuildKit()) + examples;
        }
        catch (JsonException)
        {
            // A configuration that is not JSON cannot be written; it is a change by definition
            return "unwritable:" + VegaLiteConfigText;
        }
    }

    /// <summary>
    ///     Copies the example files that are not in the kit folder yet and makes the list of examples that the kit file
    ///     names.
    /// </summary>
    /// <param name="examplesFolder">The <c>examples</c> folder of the kit.</param>
    /// <param name="copied">Receives the full paths of the files that were made.</param>
    /// <returns>The examples of the kit, with the paths of the files in the kit folder.</returns>
    private List<StyleExample> CopyExamples(string examplesFolder, List<string> copied)
    {
        var examples = new List<StyleExample>();
        if (Examples.Count > 0)
        {
            Directory.CreateDirectory(examplesFolder);
        }

        // The names that a new file may not take: those of the examples that stay, and those of files that are in the folder
        // and are not examples that the user removed (a removed example is deleted after the save, so its name is free)
        var blocked = new HashSet<string>(Examples.Where(row => !row.IsNew).Select(row => row.FileName.Trim()), StringComparer.OrdinalIgnoreCase);
        if (Directory.Exists(examplesFolder))
        {
            foreach (var file in Directory.EnumerateFiles(examplesFolder).Select(path => Path.GetFileName(path)).Where(file => !_originalExampleNames.Contains(file)))
            {
                blocked.Add(file);
            }
        }

        foreach (var row in Examples)
        {
            var name = row.FileName.Trim();
            if (row.IsNew)
            {
                if (blocked.Contains(name))
                {
                    name = UniqueExampleName(name, blocked);
                }

                blocked.Add(name);
                var destination = PathHelpers.SafePathCombine(examplesFolder, name);
                File.Copy(row.SourcePath, destination, overwrite: true);
                copied.Add(destination);
                row.FileName = name;
            }
            else if (!SamePath(row.SourcePath, Path.Combine(examplesFolder, name)) && File.Exists(row.SourcePath))
            {
                // The kit lives in a folder that is not named as its id, so its examples are brought along
                var destination = PathHelpers.SafePathCombine(examplesFolder, name);
                File.Copy(row.SourcePath, destination, overwrite: true);
                copied.Add(destination);
            }

            examples.Add(new StyleExample(
                $"{StyleKitSerializer.ExamplesFolderName}/{name}",
                Path.Combine(examplesFolder, name),
                row.Caption.Trim().Length == 0 ? null : row.Caption.Trim()));
        }

        return examples;
    }

    /// <summary>
    ///     Deletes the example files that the kit had and that the user removed. A file that cannot be deleted is logged and
    ///     left, which the next read of the folder shows as an example again.
    /// </summary>
    /// <param name="examplesFolder">The <c>examples</c> folder of the kit.</param>
    private void DeleteRemovedExamples(string examplesFolder)
    {
        var kept = new HashSet<string>(Examples.Select(row => row.FileName.Trim()), StringComparer.OrdinalIgnoreCase);
        foreach (var name in _originalExampleNames.Where(name => !kept.Contains(name)))
        {
            try
            {
                var file = PathHelpers.SafePathCombine(examplesFolder, name);
                if (File.Exists(file))
                {
                    File.Delete(file);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
            {
                Log(LogLevel.Warning, $"The example '{name}' of the style kit '{Id}' could not be deleted: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     Deletes a file that a failed save made, without failing again.
    /// </summary>
    /// <param name="path">The full path of the file.</param>
    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nothing more can be done for a file that cannot be removed
        }
    }

    /// <summary>
    ///     Tells whether two paths name the same file, ignoring case as Windows does.
    /// </summary>
    /// <param name="first">The first path.</param>
    /// <param name="second">The second path.</param>
    /// <returns><see langword="true" /> when both are the same full path.</returns>
    private static bool SamePath(string first, string second)
    {
        return string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    ///     Makes the name of a new example from a file name: a name that is not in use, with characters that a name may
    ///     have and at most 100 characters, with a number before the extension when needed.
    /// </summary>
    /// <param name="fileName">The name of the file that was chosen.</param>
    /// <param name="taken">The names in use, compared ignoring case.</param>
    /// <returns>The name.</returns>
    private static string UniqueExampleName(string fileName, IEnumerable<string> taken)
    {
        var used = new HashSet<string>(taken, StringComparer.OrdinalIgnoreCase);
        var extension = Path.GetExtension(fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            stem = stem.Replace(invalid, '-');
            extension = extension.Replace(invalid, '-');
        }

        if (extension.Length > 20)
        {
            extension = extension[..20];
        }

        if (stem.Length > 100 - extension.Length - 6)
        {
            stem = stem[..(100 - extension.Length - 6)];
        }

        var candidate = stem + extension;
        var number = 2;
        while (used.Contains(candidate))
        {
            candidate = $"{stem}-{number.ToString(CultureInfo.InvariantCulture)}{extension}";
            number++;
        }

        return candidate;
    }

    /// <summary>
    ///     Writes JSON text that is compact as indented text, so that the user can read and edit it.
    /// </summary>
    /// <param name="json">The compact text, or <see langword="null" /> for none.</param>
    /// <returns>The indented text, or an empty string for none.</returns>
    private static string Indent(string? json)
    {
        if (json is null)
        {
            return string.Empty;
        }

        using var document = JsonDocument.Parse(json);
        return JsonSerializer.Serialize(document.RootElement, new JsonSerializerOptions { WriteIndented = true, NewLine = "\n" });
    }

    /// <summary>
    ///     Makes the compact text of the Vega-Lite configuration that the kit holds.
    /// </summary>
    /// <param name="text">The text of the editor.</param>
    /// <returns>The compact text; the text as it is when it is not JSON, so that the check of the kit says what is wrong;
    /// <see langword="null" /> for a blank text.</returns>
    private static string? CompactVegaLite(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            return StyleKitSerializer.Compact(document.RootElement);
        }
        catch (JsonException)
        {
            return trimmed;
        }
    }

    /// <summary>
    ///     Checks the draft again when a row is added or removed or when a value of a row changes.
    /// </summary>
    /// <typeparam name="T">The type of the rows.</typeparam>
    /// <param name="rows">The rows to watch.</param>
    private void Track<T>(ObservableCollection<T> rows)
        where T : INotifyPropertyChanged
    {
        rows.CollectionChanged += (_, e) =>
        {
            if (e.OldItems is not null)
            {
                foreach (T row in e.OldItems)
                {
                    row.PropertyChanged -= OnRowChanged;
                }
            }

            if (e.NewItems is not null)
            {
                foreach (T row in e.NewItems)
                {
                    row.PropertyChanged += OnRowChanged;
                }
            }

            if (!_suspended)
            {
                Revalidate();
            }
        };

        foreach (var row in rows)
        {
            row.PropertyChanged += OnRowChanged;
        }
    }

    /// <summary>
    ///     Checks the draft again after a value of a row changed.
    /// </summary>
    /// <param name="sender">The row.</param>
    /// <param name="e">The event data.</param>
    private void OnRowChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_suspended)
        {
            Revalidate();
        }
    }

    /// <summary>
    ///     Writes a diagnostic entry when a logger is available. Failing to log never blocks the editor.
    /// </summary>
    /// <param name="level">Severity of the event.</param>
    /// <param name="message">Event text.</param>
    private void Log(LogLevel level, string message)
    {
        try
        {
            _logger?.Log(level, message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // Logging must never break the editor.
        }
    }
}
