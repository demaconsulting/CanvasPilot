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

using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.KnowledgeSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.StyleSubsystem;

namespace DemaConsulting.CanvasPilot.UiTests;

/// <summary>
///     Headless UI tests of the accessibility of the application, the sweep over its windows: every button that has no text has
///     a name and a tooltip, every field has a name, every button can be reached with the keyboard and is big enough to hit,
///     the shared button looks show a focus ring on the keyboard focus, the menu has a key for every item, and the colors of the
///     notices and of the welcome screen are readable. The windows are drawn in both theme variants.
/// </summary>
public sealed class AccessibilityViewTests : IDisposable
{
    /// <summary>
    ///     The smallest width and height of something to hit, in device independent pixels.
    /// </summary>
    private const double MinimumTarget = 28;

    /// <summary>
    ///     How many buttons and fields the sweeps have looked at, so that a sweep that finds nothing to look at is noticed.
    /// </summary>
    private static int _swept;

    /// <summary>
    ///     Temporary root removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-ui-a11y-").FullName;

    /// <summary>
    ///     The compositions created by a test, disposed with the fixture so no watcher outlives it.
    /// </summary>
    private readonly List<ApplicationComposition> _compositions = [];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var composition in _compositions)
        {
            composition.Dispose();
        }

        for (var attempt = 0; attempt < 40 && Directory.Exists(_root); attempt++)
        {
            try
            {
                Directory.Delete(_root, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }

    /// <summary>
    ///     Test that in the main window, with and without a project, and in every dialog and window of the application, in
    ///     both theme variants, a button or toggle that shows no text has an accessible name and a tooltip, a text field,
    ///     list, drop-down, and slider has an accessible name, and every button can take the focus and is at least 28 by 28.
    /// </summary>
    [AvaloniaFact]
    public async Task Accessibility_Windows_ControlsHaveNamesTooltipsFocusAndSize()
    {
        // Arrange: a remembered project, and the main window
        var problems = new List<string>();
        Interlocked.Exchange(ref _swept, 0);
        await CreateProjectAsync("Release Overview", "overview.svg");
        var composition = AppShellUiHelpers.Compose(_root);
        _compositions.Add(composition);
        var window = new MainWindowView(composition.Shell);
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Act: sweep the welcome screen, the workspace, and every dialog and window in each theme variant
        foreach (var variant in ThemeUiHelpers.Variants)
        {
            // The welcome screen, then the workspace of the open project
            window.RequestedThemeVariant = variant;
            Settle();
            Sweep(window, $"welcome {variant}", problems);
            if (!composition.Shell.HasProject)
            {
                await composition.Shell.RecentProjects[0].OpenCommand.ExecuteAsync(null);
            }

            Settle();
            Sweep(window, $"workspace {variant}", problems);
            composition.Shell.CloseProjectCommand.Execute(null);
            Settle();

            // Every dialog and window
            await SweepDialogAsync<AboutDialogView>(window, composition.Shell.ShowAboutCommand, variant, "about", problems);
            await SweepDialogAsync<KnowledgeBrowserView>(window, composition.Shell.ShowDrawingKnowledgeCommand, variant, "knowledge", problems);
            await SweepDialogAsync<NewProjectDialogView>(window, composition.Shell.NewProjectCommand, variant, "new project", problems);
            await SweepDialogAsync<StyleKitManagerView>(window, composition.Shell.ShowStyleKitsCommand, variant, "style kits", problems);
            await SweepSettingsAsync(window, composition.Shell, variant, problems);
        }

        window.Close();

        // Assert: no control has a problem, and the sweep looked at enough controls
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct().Take(60)));
        Assert.True(Volatile.Read(ref _swept) > 150, $"Only {_swept} controls were looked at.");
    }

    /// <summary>
    ///     Test that the shared button looks show the focus ring on the keyboard focus and on no other control, in both
    ///     theme variants, for the main button, the quiet button, and the card button.
    /// </summary>
    [AvaloniaFact]
    public void Accessibility_ButtonThemes_TabKeyShowsTheFocusRingOnTheFocusedButtonOnly()
    {
        foreach (var variant in ThemeUiHelpers.Variants)
        {
            // Arrange: three buttons with the shared looks, one after the other, in a window
            var controls = new Control[]
            {
                new Button { Theme = (ControlTheme)ThemeUiHelpers.Resolve("PrimaryButton", variant)!, Content = "A" },
                new Button { Theme = (ControlTheme)ThemeUiHelpers.Resolve("SecondaryButton", variant)!, Content = "B" },
                new Button { Theme = (ControlTheme)ThemeUiHelpers.Resolve("CardButton", variant)!, Content = "C", Width = 120, Height = 40 },
            };
            var panel = new StackPanel { Margin = new Thickness(20), Spacing = 8 };
            panel.Children.AddRange(controls);
            var window = new Window { Content = panel, RequestedThemeVariant = variant };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            var expected = ThemeUiHelpers.ColorOf("CpFocusRing", variant);

            foreach (var focused in controls)
            {
                // Act: press the Tab key, which moves the focus to the next control
                window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();

                // Assert: only that button shows the ring, in the focus color
                foreach (var control in controls)
                {
                    var ring = control.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "PART_FocusRing");
                    var color = Assert.IsAssignableFrom<ISolidColorBrush>(ring.BorderBrush).Color;
                    Assert.Equal(ReferenceEquals(control, focused) ? expected : Colors.Transparent, color);
                }
            }

            window.Close();
        }
    }

    /// <summary>
    ///     Test that every menu and every item of the menu has an access key, and that no two entries of a menu share one,
    ///     so the whole menu can be used with the keyboard alone.
    /// </summary>
    [AvaloniaFact]
    public void Accessibility_MainMenu_EveryEntryHasAUniqueAccessKey()
    {
        // Arrange: the main window
        var composition = AppShellUiHelpers.Compose(_root);
        _compositions.Add(composition);
        var window = new MainWindowView(composition.Shell);
        window.Show();
        var menu = window.FindControl<Menu>("MainMenu")!;

        // Act: read the access keys of the menus and of their items
        var menus = menu.Items.OfType<MenuItem>().ToList();
        var topKeys = menus.Select(item => AccessKeyOf(item.Header)).ToList();

        // Assert: every entry has one key, unique among its neighbors
        Assert.Equal(5, menus.Count);
        Assert.DoesNotContain(null, topKeys);
        Assert.Equal(topKeys.Count, topKeys.Distinct().Count());
        foreach (var top in menus)
        {
            var items = top.Items.OfType<MenuItem>().ToList();
            var keys = items.Select(item => AccessKeyOf(item.Header)).ToList();
            Assert.True(keys.All(key => key is not null), $"An item of the {top.Header} menu has no access key.");
            Assert.True(keys.Count == keys.Distinct().Count(), $"Two items of the {top.Header} menu share an access key.");
        }

        window.Close();
    }

    /// <summary>
    ///     Test that the text on the notices and on the welcome screen is readable in both theme variants: the text on the
    ///     tinted banners, the warning and danger colors that carry words, the dark text on the amber button, and the text on
    ///     the backdrop of the welcome screen, including the glows.
    /// </summary>
    [AvaloniaFact]
    public void Accessibility_Colors_NoticesAndWelcomeTextAreReadable()
    {
        // Arrange: the running application, which merges the token dictionary
        Assert.NotNull(Application.Current);

        // Act / Assert: compute the contrast of the text on the banners and the amber button in each variant, and check it
        foreach (var variant in ThemeUiHelpers.Variants)
        {
            var pane = ThemeUiHelpers.ColorOf("CpSurfacePane", variant);
            var window = ThemeUiHelpers.ColorOf("CpSurfaceWindow", variant);

            // Text on the tinted banners (provider notice, error banner)
            var warningBanner = ThemeUiHelpers.Over(ThemeUiHelpers.ColorOf("CpWarningTint", variant), pane);
            var dangerBanner = ThemeUiHelpers.Over(ThemeUiHelpers.ColorOf("CpDangerTint", variant), window);
            foreach (var (text, background) in new (string, Color)[]
            {
                ("CpTextPrimary", warningBanner), ("CpTextSecondary", warningBanner), ("CpWarning", warningBanner),
                ("CpTextPrimary", dangerBanner), ("CpDanger", dangerBanner)
            })
            {
                var ratio = ThemeUiHelpers.Contrast(ThemeUiHelpers.ColorOf(text, variant), background);
                Assert.True(ratio >= 4.5, $"{text} on a banner in {variant} is {ratio:F2}:1, below 4.5:1.");
            }

            // The dark text of the amber button
            var amber = ThemeUiHelpers.Contrast(ThemeUiHelpers.ColorOf("CpSurfaceWindow", variant), ThemeUiHelpers.ColorOf("CpWarning", variant));
            Assert.True(amber >= 4.5, $"The text of the amber button in {variant} is {amber:F2}:1, below 4.5:1.");
        }

        // Act: read the backdrop of the dark welcome screen: its three stops, and the worst place under a violet glow
        var stops = ThemeUiHelpers.StopColorsOf("CpWelcomeBackdrop", ThemeVariant.Dark);
        Assert.Equal(3, stops.Count);
        var glow = ThemeUiHelpers.Over(ThemeUiHelpers.StopColorsOf("CpWelcomeGlowViolet", ThemeVariant.Dark)[0], stops[1]);
        // Assert: the text on every part of the backdrop has at least 4.5:1 contrast
        foreach (var background in stops.Append(glow))
        {
            foreach (var text in new[] { "CpTextPrimary", "CpTextSecondary", "CpAccentText" })
            {
                var ratio = ThemeUiHelpers.Contrast(ThemeUiHelpers.ColorOf(text, ThemeVariant.Dark), background);
                Assert.True(ratio >= 4.5, $"{text} on the dark welcome backdrop {background} is {ratio:F2}:1, below 4.5:1.");
            }
        }

        foreach (var background in stops)
        {
            var ratio = ThemeUiHelpers.Contrast(ThemeUiHelpers.ColorOf("CpTextTertiary", ThemeVariant.Dark), background);
            Assert.True(ratio >= 4.5, $"The tertiary text on the dark welcome backdrop {background} is {ratio:F2}:1, below 4.5:1.");
        }
    }

    /// <summary>
    ///     Lets the layout and the pending jobs finish.
    /// </summary>
    private static void Settle()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    ///     Reads the access key of a menu header, which is the letter after the underscore.
    /// </summary>
    /// <param name="header">The header.</param>
    /// <returns>The key in lower case, or <see langword="null" /> when there is none.</returns>
    private static char? AccessKeyOf(object? header)
    {
        if (header is not string text)
        {
            return null;
        }

        var index = text.IndexOf('_', StringComparison.Ordinal);
        return index >= 0 && index + 1 < text.Length ? char.ToLowerInvariant(text[index + 1]) : null;
    }

    /// <summary>
    ///     Creates a project with files and remembers it, as an earlier run would.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <param name="files">The files to create.</param>
    /// <returns>A task that completes when the project is stored.</returns>
    private async Task CreateProjectAsync(string name, params string[] files)
    {
        var token = TestContext.Current.CancellationToken;
        var projects = Directory.CreateDirectory(Path.Combine(_root, "projects")).FullName;
        var created = await new ProjectService().CreateAsync(projects, name, token);
        foreach (var file in files)
        {
            await File.WriteAllTextAsync(
                Path.Combine(created.Project!.FolderPath, file),
                "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='60'><rect width='100' height='60' fill='#ccc'/></svg>",
                token);
        }

        var settings = new SettingsService(Path.Combine(_root, "settings"));
        settings.Load();
        new RecentProjects(settings).Add(created.Project!.FilePath);
    }

    /// <summary>
    ///     Opens a dialog or window with a command, sweeps it, and closes it.
    /// </summary>
    /// <typeparam name="T">The type of the window that the command opens.</typeparam>
    /// <param name="owner">The main window.</param>
    /// <param name="command">The command that opens the window.</param>
    /// <param name="variant">The theme variant.</param>
    /// <param name="name">What the window is called in a message.</param>
    /// <param name="problems">Collects the problems.</param>
    /// <returns>A task that completes when the window is closed.</returns>
    private static async Task SweepDialogAsync<T>(
        Window owner,
        CommunityToolkit.Mvvm.Input.IAsyncRelayCommand command,
        ThemeVariant variant,
        string name,
        List<string> problems)
        where T : Window
    {
        var shown = command.ExecuteAsync(null);
        Settle();
        var dialog = owner.OwnedWindows.OfType<T>().Single();
        dialog.RequestedThemeVariant = variant;
        Settle();
        Sweep(dialog, $"{name} {variant}", problems);
        dialog.Close();
        await shown;
    }

    /// <summary>
    ///     Opens the settings, sweeps each page, and closes the dialog.
    /// </summary>
    /// <param name="owner">The main window.</param>
    /// <param name="shell">The shell.</param>
    /// <param name="variant">The theme variant.</param>
    /// <param name="problems">Collects the problems.</param>
    /// <returns>A task that completes when the dialog is closed.</returns>
    private static async Task SweepSettingsAsync(Window owner, MainWindowShell shell, ThemeVariant variant, List<string> problems)
    {
        var shown = shell.OpenSettingsCommand.ExecuteAsync(null);
        Settle();
        var dialog = owner.OwnedWindows.OfType<SettingsDialogView>().Single();
        dialog.RequestedThemeVariant = variant;
        var model = (SettingsDialogViewModel)dialog.DataContext!;
        foreach (var page in model.Pages)
        {
            model.SelectedPage = page;
            Settle();
            Sweep(dialog, $"settings {page.Title} {variant}", problems);
        }

        dialog.Close();
        await shown;
    }

    /// <summary>
    ///     Looks at every control of a window that is shown and notes what a user of the keyboard or of a screen reader would miss.
    /// </summary>
    /// <param name="root">The window.</param>
    /// <param name="label">What the window is called in a message.</param>
    /// <param name="problems">Collects the problems.</param>
    private static void Sweep(Window root, string label, List<string> problems)
    {
        foreach (var control in root.GetVisualDescendants().OfType<Control>().Where(control => control.IsEffectivelyVisible))
        {
            var id = AutomationProperties.GetAutomationId(control) ?? control.Name ?? control.GetType().Name;

            // The chevron of a tree item is for the pointer: the keyboard opens and closes the item itself with Right and Left
            if (control.Name == "PART_ExpandCollapseChevron")
            {
                continue;
            }

            if (control is Button or ToggleButton or TextBox or ComboBox or Slider or NumericUpDown or ListBox or TreeView)
            {
                Interlocked.Increment(ref _swept);
            }

            switch (control)
            {
                case Button or ToggleButton when control.GetType() == typeof(Button) || control.GetType() == typeof(ToggleButton):
                    var button = (ContentControl)control;
                    if (!HasText(button) && string.IsNullOrWhiteSpace(AutomationProperties.GetName(button)))
                    {
                        problems.Add($"{label}: the icon button {id} has no name.");
                    }

                    if (!HasText(button) && ToolTip.GetTip(button) is null)
                    {
                        problems.Add($"{label}: the icon button {id} has no tooltip.");
                    }

                    if (control.IsEffectivelyEnabled && (!control.Focusable || !control.IsTabStop))
                    {
                        problems.Add($"{label}: the button {id} cannot take the focus with the keyboard.");
                    }

                    if (!HasText(button) && control.Bounds.Width > 0 && (control.Bounds.Width < MinimumTarget || control.Bounds.Height < MinimumTarget))
                    {
                        problems.Add($"{label}: the icon button {id} is {control.Bounds.Width:F0} by {control.Bounds.Height:F0}, below {MinimumTarget:F0}.");
                    }

                    break;

                case TextBox or ComboBox or Slider or NumericUpDown or ListBox or TreeView:
                    if (string.IsNullOrWhiteSpace(AutomationProperties.GetName(control)) && !HasLabelAbove(control))
                    {
                        problems.Add($"{label}: the field {id} ({control.GetType().Name}) has no name.");
                    }

                    break;

                default:
                    break;
            }
        }
    }

    /// <summary>
    ///     Tells whether a button shows words.
    /// </summary>
    /// <param name="button">The button.</param>
    /// <returns><see langword="true" /> when its content is text or holds a text block with text.</returns>
    private static bool HasText(ContentControl button)
    {
        return button.Content is string { Length: > 0 }
               || button.GetVisualDescendants().OfType<TextBlock>().Any(text => text.IsEffectivelyVisible && !string.IsNullOrWhiteSpace(text.Text))
               || button.GetVisualDescendants().OfType<AccessText>().Any(text => !string.IsNullOrWhiteSpace(text.Text));
    }

    /// <summary>
    ///     Tells whether a field has words of its own that name it: a placeholder, or a list or tree whose items are text.
    /// </summary>
    /// <param name="control">The field.</param>
    /// <returns><see langword="true" /> when the field has a placeholder.</returns>
    private static bool HasLabelAbove(Control control)
    {
        return control is TextBox { PlaceholderText.Length: > 0 };
    }
}
