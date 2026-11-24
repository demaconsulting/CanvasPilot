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

using DemaConsulting.CanvasPilot.AnnotationSubsystem;
using DemaConsulting.CanvasPilot.AppShellSubsystem;
using DemaConsulting.CanvasPilot.ChatSubsystem;
using DemaConsulting.CanvasPilot.ComposerSubsystem;
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.PayloadSubsystem;
using DemaConsulting.CanvasPilot.PreviewSubsystem;
using DemaConsulting.CanvasPilot.ProjectSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;
using DemaConsulting.CanvasPilot.SpeechSubsystem;
using NSubstitute;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Tests that every place that names the talk key (the status bar, the welcome screen, the chat panel with its key cap,
///     tooltip, and tip) takes the key from the one setting and follows it when the user chooses another key, so that no
///     hint is out of date.
/// </summary>
public sealed class PushToTalkHintsTests : IDisposable
{
    /// <summary>
    ///     Temporary root removed on disposal.
    /// </summary>
    private readonly string _root = Directory.CreateTempSubdirectory("canvaspilot-talk-key-tests-").FullName;

    /// <summary>
    ///     What is disposed with the fixture.
    /// </summary>
    private readonly List<IDisposable> _owned = [];

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (var item in _owned)
        {
            item.Dispose();
        }

        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the status bar, the welcome screen, and the shell show F9 by default and the key of the settings once
    ///     the user has chosen it, announcing the change, and never show the old F8.
    /// </summary>
    [Fact]
    public void PushToTalkHints_StatusBarAndWelcome_FollowTheSettings()
    {
        // Arrange: a shell over the settings of the temporary folder
        var logger = new RollingFileLogger(Path.Combine(_root, "logs"));
        var settings = new SettingsService(Path.Combine(_root, "settings"), logger);
        settings.Load();
        var explorer = new ProjectExplorerViewModel(new FakeImageSourceFilter(".svg"), new FakeRefreshScheduler(), logger: logger);
        var shell = new MainWindowShell(new ProjectService(logger), new RecentProjects(settings), settings, new ProjectWorkspaceViewModel(explorer), []);
        _owned.Add(shell);
        var statusChanges = new List<string?>();
        var welcomeChanges = new List<string?>();
        shell.StatusBar.PropertyChanged += (_, e) => statusChanges.Add(e.PropertyName);
        shell.Welcome.PropertyChanged += (_, e) => welcomeChanges.Add(e.PropertyName);
        var before = (shell.StatusBar.TalkKeyText, shell.Welcome.DescribeStepText, shell.TalkKey.Text);

        // Act: the user chooses another key
        settings.Update(current => current with { Speech = current.Speech with { PushToTalkKey = "Ctrl+Shift+Space" } });

        // Assert
        Assert.Equal(("F9", "Type a request, or hold F9 and speak it. The copilot draws the picture.", "F9"), before);
        Assert.Equal("Ctrl+Shift+Space", shell.StatusBar.TalkKeyText);
        Assert.Equal("Type a request, or hold Ctrl+Shift+Space and speak it. The copilot draws the picture.", shell.Welcome.DescribeStepText);
        Assert.Contains(nameof(StatusBarViewModel.TalkKeyText), statusChanges);
        Assert.Contains(nameof(WelcomeViewModel.DescribeStepText), welcomeChanges);
        Assert.DoesNotContain("F8", shell.Welcome.DescribeStepText, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Test that the chat panel shows F9 on its key cap, in its tooltip, and in the tip of the empty conversation by
    ///     default, and the key of the setting after a change, announcing each.
    /// </summary>
    [Fact]
    public void PushToTalkHints_ChatPanel_KeyCapTooltipAndTipFollowTheSetting()
    {
        // Arrange: a panel over a setting that is fixed and one that follows settings
        var settings = new SettingsService(Path.Combine(_root, "chat-settings"));
        settings.Load();
        var talkKey = new PushToTalkSetting(settings);
        _owned.Add(talkKey);
        var marks = new MarkRegistry();
        var composer = new ComposerViewModel(new FakeMessageBuilder(), marks, Substitute.For<IDictationService>());
        var panel = new ChatPanelViewModel(
            composer,
            marks,
            new AnnotationOverlayViewModel(marks, new FakeRenderedImageSource(), _ => { }),
            Substitute.For<IMarkImageComposer>(),
            talkKey: talkKey);
        _owned.Add(panel);
        var changes = new List<string?>();
        panel.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        var before = (panel.TalkKeyText, panel.EmptyTipText, panel.TalkToolTip);

        // Act
        settings.Update(current => current with { Speech = current.Speech with { PushToTalkKey = "F6" } });

        // Assert
        Assert.Equal(("F9", "Drop a picture, or hold F9 and speak", "Hold this button, or hold F9, speak, and let go to add what you said to the message"), before);
        Assert.Equal("F6", panel.TalkKeyText);
        Assert.Equal("Drop a picture, or hold F6 and speak", panel.EmptyTipText);
        Assert.Contains("hold F6,", panel.TalkToolTip, StringComparison.Ordinal);
        Assert.Contains(nameof(ChatPanelViewModel.TalkKeyText), changes);
        Assert.Contains(nameof(ChatPanelViewModel.EmptyTipText), changes);
        Assert.Contains(nameof(ChatPanelViewModel.TalkToolTip), changes);
    }
}
