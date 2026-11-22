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
using DemaConsulting.CanvasPilot.LoggingSubsystem;
using DemaConsulting.CanvasPilot.SettingsSubsystem;

namespace DemaConsulting.CanvasPilot.Tests;

/// <summary>
///     Unit tests for <see cref="SettingsService" />.
/// </summary>
public sealed class SettingsServiceTests : IDisposable
{
    /// <summary>
    ///     Temporary root created fresh for each test and removed on disposal.
    /// </summary>
    private readonly string _tempRoot = Directory.CreateTempSubdirectory("canvaspilot-tests-").FullName;

    /// <summary>
    ///     Folder used as the settings folder; it does not exist until the service creates it.
    /// </summary>
    private string SettingsFolder => Path.Combine(_tempRoot, "CanvasPilot");

    /// <summary>
    ///     Folder used for the diagnostic log.
    /// </summary>
    private string LogFolder => Path.Combine(_tempRoot, "logs");

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    /// <summary>
    ///     Test that the default settings folder is the CanvasPilot folder under the application data folder.
    /// </summary>
    [Fact]
    public void SettingsService_DefaultSettingsFolder_Default_IsCanvasPilotUnderApplicationData()
    {
        // Arrange: the platform application data folder
        var applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        // Act: read the default settings folder
        var folder = SettingsService.DefaultSettingsFolder;

        // Assert: it is the CanvasPilot folder beneath the application data folder
        Assert.Equal(Path.Combine(applicationData, "CanvasPilot"), folder);
    }

    /// <summary>
    ///     Test that the settings file is named settings.json inside the settings folder.
    /// </summary>
    [Fact]
    public void SettingsService_SettingsFilePath_Folder_IsSettingsJsonInFolder()
    {
        // Arrange / Act: a service for a folder
        var service = new SettingsService(SettingsFolder);

        // Assert: the file is settings.json in that folder
        Assert.Equal(Path.Combine(SettingsFolder, "settings.json"), service.SettingsFilePath);
    }

    /// <summary>
    ///     Test that saving creates a missing settings folder and the file.
    /// </summary>
    [Fact]
    public void SettingsService_Save_MissingFolder_CreatesFolderAndFile()
    {
        // Arrange: a service whose folder does not exist
        var service = new SettingsService(SettingsFolder);
        Assert.False(Directory.Exists(SettingsFolder));

        // Act: save default settings
        service.Save(new AppSettings());

        // Assert: the folder and file now exist
        Assert.True(File.Exists(service.SettingsFilePath));
    }

    /// <summary>
    ///     Test that every setting survives a save and a load by a fresh service.
    /// </summary>
    [Fact]
    public void SettingsService_SaveLoad_AllSettings_RoundTrips()
    {
        // Arrange: settings with a non-default value in every field
        var original = CreateFullSettings();
        new SettingsService(SettingsFolder).Save(original);

        // Act: load with a fresh service, as after an application restart
        var loaded = new SettingsService(SettingsFolder).Load();

        // Assert: every value is preserved
        Assert.Equal(original.Version, loaded.Version);
        Assert.Equal(original.RecentProjects, loaded.RecentProjects);
        Assert.Equal(original.AiProvider, loaded.AiProvider);
        Assert.Equal("mic", loaded.Speech.CaptureDevice);
        Assert.Equal("speaker", loaded.Speech.PlaybackDevice);
        Assert.Equal("stt-1", loaded.Speech.SttModelId);
        Assert.Equal("tts-1", loaded.Speech.TtsModelId);
        Assert.Equal("0.75", loaded.Speech.ModelParameters["tts-1"]["speed"]);
        Assert.Equal("true", loaded.Speech.ModelParameters["tts-1"]["normalize"]);
        Assert.Equal([TestPlatformPaths.FullPath("Team", "Styles"), TestPlatformPaths.NetworkFolder], loaded.Styles.SharedKitFolders);
        Assert.Equal("slides-dark", loaded.Styles.DefaultKitId);
    }

    /// <summary>
    ///     Test that the written file is a versioned camel-case JSON document with the documented structure.
    /// </summary>
    [Fact]
    public void SettingsService_Save_Settings_WritesVersionedCamelCaseJson()
    {
        // Arrange: a service and full settings
        var service = new SettingsService(SettingsFolder);

        // Act: save and parse the raw file
        service.Save(CreateFullSettings());
        using var document = JsonDocument.Parse(File.ReadAllText(service.SettingsFilePath));
        var root = document.RootElement;

        // Assert: the documented property names and value shapes are present
        Assert.Equal(1, root.GetProperty("version").GetInt32());
        Assert.Equal("C:\\Work\\a.canvaspilot", root.GetProperty("recentProjects")[0].GetString());
        var ai = root.GetProperty("aiProvider");
        Assert.Equal("Ollama", ai.GetProperty("provider").GetString());
        Assert.Equal("llama3", ai.GetProperty("modelName").GetString());
        Assert.Equal(32768, ai.GetProperty("contextWindowTokens").GetInt32());
        Assert.Equal("High", ai.GetProperty("reasoningLevel").GetString());
        Assert.Equal("http://example.test:11434", ai.GetProperty("ollamaEndpoint").GetString());
        Assert.True(ai.GetProperty("unrestrictedAgentTools").GetBoolean());
        Assert.Equal("C:\\Tools\\copilot.exe", ai.GetProperty("copilotCliPath").GetString());
        var speech = root.GetProperty("speech");
        Assert.Equal("mic", speech.GetProperty("captureDevice").GetString());
        Assert.Equal("0.75", speech.GetProperty("modelParameters").GetProperty("tts-1").GetProperty("speed").GetString());
        var styles = root.GetProperty("styles");
        Assert.Equal(TestPlatformPaths.FullPath("Team", "Styles"), styles.GetProperty("sharedKitFolders")[0].GetString());
        Assert.Equal("slides-dark", styles.GetProperty("defaultKitId").GetString());
    }

    /// <summary>
    ///     Test that a missing file yields default settings and a safe agent-tools default.
    /// </summary>
    [Fact]
    public void SettingsService_Load_MissingFile_ReturnsDefaults()
    {
        // Arrange: a service with no settings file
        var service = new SettingsService(SettingsFolder);

        // Act: load
        var loaded = service.Load();

        // Assert: defaults are in effect, including the opt-in agent tools being off
        Assert.Equal(new AiProviderSettings(), loaded.AiProvider);
        Assert.Empty(loaded.RecentProjects);
        Assert.False(loaded.AiProvider.UnrestrictedAgentTools);
        Assert.Equal(AiProviderSettings.DefaultContextWindowTokens, loaded.AiProvider.ContextWindowTokens);
        Assert.Equal(32768, loaded.AiProvider.ContextWindowTokens);
        Assert.Same(loaded, service.Current);
    }

    /// <summary>
    ///     Test that the default context window is 32768 tokens, is above the recommended minimum, and that the
    ///     recommended minimum is itself a value that settings accept.
    /// </summary>
    [Fact]
    public void SettingsService_DefaultContextWindow_Constants_AreThe32768DefaultAndThe16384Minimum()
    {
        // Act and assert: the constants have the documented values and relate as the warning logic needs
        Assert.Equal(32768, AiProviderSettings.DefaultContextWindowTokens);
        Assert.Equal(16384, AiProviderSettings.RecommendedMinimumContextTokens);
        Assert.True(AiProviderSettings.DefaultContextWindowTokens >= AiProviderSettings.RecommendedMinimumContextTokens);
        Assert.True(AiProviderSettings.IsValidContextWindow(AiProviderSettings.RecommendedMinimumContextTokens));
    }

    /// <summary>
    ///     Test that a settings file that does not state a context window gets the new default of 32768 tokens.
    /// </summary>
    [Fact]
    public void SettingsService_Load_NoContextWindowInFile_Uses32768()
    {
        // Arrange: a file with an AI provider section that has no context window
        WriteSettingsFile("""{ "version": 1, "aiProvider": { "modelName": "gpt" } }""");

        // Act: load
        var loaded = new SettingsService(SettingsFolder).Load();

        // Assert: the default of 32768 is in effect
        Assert.Equal(32768, loaded.AiProvider.ContextWindowTokens);
    }

    /// <summary>
    ///     Test that a context window of 8192 that an earlier build stored is a valid value and is kept, not replaced
    ///     by the new default.
    /// </summary>
    [Fact]
    public void SettingsService_Load_StoredContextWindow8192_IsKept()
    {
        // Arrange: a file that an earlier build wrote with its old default
        WriteSettingsFile("""{ "version": 1, "aiProvider": { "contextWindowTokens": 8192 } }""");

        // Act: load, then save and load again
        var service = new SettingsService(SettingsFolder);
        var loaded = service.Load();
        service.Save(loaded);
        var reloaded = new SettingsService(SettingsFolder).Load();

        // Assert: the stored value survives loading and a save
        Assert.Equal(8192, loaded.AiProvider.ContextWindowTokens);
        Assert.Equal(8192, reloaded.AiProvider.ContextWindowTokens);
    }

    /// <summary>
    ///     Test that properties missing from the file take their default values.
    /// </summary>
    [Fact]
    public void SettingsService_Load_MissingProperties_UsesDefaults()
    {
        // Arrange: a file that sets only one value
        WriteSettingsFile("""{ "version": 1, "aiProvider": { "modelName": "gpt" } }""");

        // Act: load
        var loaded = new SettingsService(SettingsFolder).Load();

        // Assert: the value is read and everything else is default
        Assert.Equal("gpt", loaded.AiProvider.ModelName);
        Assert.Equal(AiProviderSettings.DefaultContextWindowTokens, loaded.AiProvider.ContextWindowTokens);
        Assert.Equal(AiProviderSettings.DefaultOllamaEndpoint, loaded.AiProvider.OllamaEndpoint);
        Assert.Empty(loaded.Speech.ModelParameters);
    }

    /// <summary>
    ///     Test that properties this build does not know are ignored.
    /// </summary>
    [Fact]
    public void SettingsService_Load_UnknownProperties_AreIgnored()
    {
        // Arrange: a file with extra properties at several levels
        WriteSettingsFile(
            """{ "version": 1, "future": { "x": 1 }, "recentProjects": ["C:\\a"], "aiProvider": { "extra": [1,2], "modelName": "m" } }""");

        // Act: load
        var loaded = new SettingsService(SettingsFolder).Load();

        // Assert: the known values are read
        Assert.Equal(["C:\\a"], loaded.RecentProjects);
        Assert.Equal("m", loaded.AiProvider.ModelName);
    }

    /// <summary>
    ///     Test that a corrupt file is renamed aside and defaults are used.
    /// </summary>
    [Fact]
    public void SettingsService_Load_CorruptFile_RenamesToBadAndUsesDefaults()
    {
        // Arrange: a file that is not valid JSON
        WriteSettingsFile("{ this is not json");
        var service = new SettingsService(SettingsFolder);

        // Act: load
        var loaded = service.Load();

        // Assert: defaults are in effect, the file was renamed to .bad, and the original name is free
        Assert.Equal(new AiProviderSettings(), loaded.AiProvider);
        Assert.False(File.Exists(service.SettingsFilePath));
        Assert.Equal("{ this is not json", File.ReadAllText(service.SettingsFilePath + ".bad"));
    }

    /// <summary>
    ///     Test that a corrupt file is reported to the log.
    /// </summary>
    [Fact]
    public void SettingsService_Load_CorruptFile_LogsError()
    {
        // Arrange: a corrupt file and a service with a logger
        WriteSettingsFile("[1, 2");
        var logger = new RollingFileLogger(LogFolder);
        var service = new SettingsService(SettingsFolder, logger);

        // Act: load
        service.Load();

        // Assert: the log records the corruption and the new file name
        var log = File.ReadAllText(logger.ActiveFilePath);
        Assert.Contains("[Error]", log);
        Assert.Contains("corrupt", log);
        Assert.Contains("settings.json.bad", log);
    }

    /// <summary>
    ///     Test that an empty file is treated as corrupt.
    /// </summary>
    [Fact]
    public void SettingsService_Load_EmptyFile_TreatedAsCorrupt()
    {
        // Arrange: an empty settings file
        WriteSettingsFile(string.Empty);
        var service = new SettingsService(SettingsFolder);

        // Act: load
        var loaded = service.Load();

        // Assert: defaults are in effect and the file was moved aside
        Assert.Equal(new AppSettings().AiProvider, loaded.AiProvider);
        Assert.True(File.Exists(service.SettingsFilePath + ".bad"));
    }

    /// <summary>
    ///     Test that a file from a newer schema version still loads its known values and logs a warning.
    /// </summary>
    [Fact]
    public void SettingsService_Load_NewerVersion_LoadsKnownValuesAndLogsWarning()
    {
        // Arrange: a version 99 file with a known value and unknown content
        WriteSettingsFile("""{ "version": 99, "aiProvider": { "modelName": "future-model", "newOption": true }, "newSection": {} }""");
        var logger = new RollingFileLogger(LogFolder);
        var service = new SettingsService(SettingsFolder, logger);

        // Act: load
        var loaded = service.Load();

        // Assert: the known value is read, the file is left in place, and a warning is logged
        Assert.Equal("future-model", loaded.AiProvider.ModelName);
        Assert.True(File.Exists(service.SettingsFilePath));
        var log = File.ReadAllText(logger.ActiveFilePath);
        Assert.Contains("[Warning]", log);
        Assert.Contains("99", log);
    }

    /// <summary>
    ///     Test that out-of-range or malformed values are replaced with defaults.
    /// </summary>
    [Fact]
    public void SettingsService_Load_OutOfRangeValues_ReplacedWithDefaults()
    {
        // Arrange: a context window outside the bounds, an endpoint that is not a URL, and null members
        WriteSettingsFile(
            """{ "aiProvider": { "contextWindowTokens": 5, "ollamaEndpoint": "not a url", "modelName": null }, "speech": null, "recentProjects": ["", null, "C:\\a"] }""");

        // Act: load
        var loaded = new SettingsService(SettingsFolder).Load();

        // Assert: defaults replace the bad values and blank recent entries are dropped
        Assert.Equal(AiProviderSettings.DefaultContextWindowTokens, loaded.AiProvider.ContextWindowTokens);
        Assert.Equal(AiProviderSettings.DefaultOllamaEndpoint, loaded.AiProvider.OllamaEndpoint);
        Assert.Equal(string.Empty, loaded.AiProvider.ModelName);
        Assert.Equal(new SpeechSettings().CaptureDevice, loaded.Speech.CaptureDevice);
        Assert.Equal(["C:\\a"], loaded.RecentProjects);
    }

    /// <summary>
    ///     Test that an unrecognized enumeration value falls back to the first member.
    /// </summary>
    [Fact]
    public void SettingsService_Load_UnknownEnumValue_UsesFirstValue()
    {
        // Arrange: a provider and reasoning level this build does not know
        WriteSettingsFile("""{ "aiProvider": { "provider": "SomethingNew", "reasoningLevel": 42 } }""");

        // Act: load
        var loaded = new SettingsService(SettingsFolder).Load();

        // Assert: the defaults for those enumerations are used and the file still loads
        Assert.Equal(AiProviderKind.GitHubCopilot, loaded.AiProvider.Provider);
        Assert.Equal(ReasoningLevel.Auto, loaded.AiProvider.ReasoningLevel);
    }

    /// <summary>
    ///     Test that saving over an existing file replaces its content and leaves no temporary file.
    /// </summary>
    [Fact]
    public void SettingsService_Save_ExistingFile_ReplacesContentAndLeavesNoTemporaryFile()
    {
        // Arrange: an existing settings file
        var service = new SettingsService(SettingsFolder);
        service.Save(new AppSettings { RecentProjects = ["C:\\old"] });

        // Act: save different settings
        service.Save(new AppSettings { RecentProjects = ["C:\\new"] });

        // Assert: the new content is stored and only settings.json remains in the folder
        Assert.Equal(["C:\\new"], new SettingsService(SettingsFolder).Load().RecentProjects);
        Assert.Equal([service.SettingsFilePath], Directory.GetFiles(SettingsFolder));
    }

    /// <summary>
    ///     Test that a failed write reports an IOException, keeps the settings in effect, leaves no temporary
    ///     file, and does not announce a change.
    /// </summary>
    [Fact]
    public void SettingsService_Save_WriteFails_ThrowsIOExceptionAndKeepsCurrentSettings()
    {
        // Arrange: a directory occupies the settings file name, so the final replace cannot succeed
        var service = new SettingsService(SettingsFolder);
        Directory.CreateDirectory(service.SettingsFilePath);
        var changedCount = 0;
        service.Changed += (_, _) => changedCount++;

        // Act / Assert: saving fails with an IOException
        Assert.ThrowsAny<IOException>(() => service.Save(new AppSettings { RecentProjects = ["C:\\x"] }));

        // Assert: nothing changed and no temporary file remains
        Assert.Empty(service.Current.RecentProjects);
        Assert.Equal(0, changedCount);
        Assert.Empty(Directory.GetFiles(SettingsFolder));
    }

    /// <summary>
    ///     Test that saving raises Changed with the settings that were saved.
    /// </summary>
    [Fact]
    public void SettingsService_Save_Settings_RaisesChangedWithSavedSettings()
    {
        // Arrange: a service with a Changed handler
        var service = new SettingsService(SettingsFolder);
        AppSettings? announced = null;
        service.Changed += (_, e) => announced = e.Settings;

        // Act: save settings
        service.Save(new AppSettings { RecentProjects = ["C:\\a"] });

        // Assert: the handler received the saved settings, which are also the current settings
        Assert.NotNull(announced);
        Assert.Equal(["C:\\a"], announced.RecentProjects);
        Assert.Same(announced, service.Current);
    }

    /// <summary>
    ///     Test that Update applies a transformation to the latest settings, saves the result, and announces it.
    /// </summary>
    [Fact]
    public void SettingsService_Update_Transform_SavesAndReturnsTransformedSettings()
    {
        // Arrange: stored settings and a Changed handler
        var service = new SettingsService(SettingsFolder);
        service.Save(new AppSettings { RecentProjects = ["C:\\a"] });
        var changedCount = 0;
        service.Changed += (_, _) => changedCount++;

        // Act: add a recent project through Update
        var updated = service.Update(settings => settings with { RecentProjects = ["C:\\b", .. settings.RecentProjects] });

        // Assert: the transformed value is returned, stored, and announced once
        Assert.Equal(["C:\\b", "C:\\a"], updated.RecentProjects);
        Assert.Equal(["C:\\b", "C:\\a"], new SettingsService(SettingsFolder).Load().RecentProjects);
        Assert.Equal(1, changedCount);
    }

    /// <summary>
    ///     Test that loading an existing file raises Changed with the loaded settings.
    /// </summary>
    [Fact]
    public void SettingsService_Load_ExistingFile_RaisesChanged()
    {
        // Arrange: an existing file and a Changed handler
        WriteSettingsFile("""{ "recentProjects": ["C:\\a"] }""");
        var service = new SettingsService(SettingsFolder);
        AppSettings? announced = null;
        service.Changed += (_, e) => announced = e.Settings;

        // Act: load
        var loaded = service.Load();

        // Assert: the handler received the loaded settings
        Assert.Same(loaded, announced);
    }

    /// <summary>
    ///     Test that a settings file from before style kits has no such member and gives the defaults for it, and that a
    ///     null member does the same.
    /// </summary>
    [Fact]
    public void SettingsService_Load_FileWithoutStyles_GivesNoSharedFoldersAndNoDefaultKit()
    {
        // Arrange: an older file and a file whose styles member is null
        WriteSettingsFile("""{ "version": 1, "aiProvider": { "modelName": "gpt" } }""");
        var older = new SettingsService(SettingsFolder).Load();
        WriteSettingsFile("""{ "version": 1, "styles": null }""");
        var nulled = new SettingsService(SettingsFolder).Load();

        // Assert
        Assert.Empty(older.Styles.SharedKitFolders);
        Assert.Equal(string.Empty, older.Styles.DefaultKitId);
        Assert.Empty(nulled.Styles.SharedKitFolders);
        Assert.Equal(string.Empty, nulled.Styles.DefaultKitId);
    }

    /// <summary>
    ///     Test that the shared kit folders are trimmed, that relative, blank, and invalid entries are dropped with a line
    ///     in the log, that a folder is kept once ignoring case, that the order is kept, and that a folder that does not
    ///     exist is kept.
    /// </summary>
    [Fact]
    public void SettingsService_Load_SharedKitFolders_AreTrimmedFilteredAndUniqueInOrder()
    {
        // Arrange: a file with good, relative, blank, null, duplicate, and not existing folders
        var missing = Path.Combine(_tempRoot, "not-created");
        var folderB = Path.Combine(_tempRoot, "b");
        WriteSettingsFile(
            "{ \"styles\": { \"sharedKitFolders\": ["
            + JsonSerializer.Serialize("  " + folderB + "  ") + ", "
            + JsonSerializer.Serialize("styles\\relative") + ", "
            + JsonSerializer.Serialize("   ") + ", null, "
            + JsonSerializer.Serialize(folderB.ToUpperInvariant()) + ", "
            + JsonSerializer.Serialize(missing) + ", "
            + JsonSerializer.Serialize("C:folder") + "] } }");
        var logger = new RollingFileLogger(LogFolder);

        // Act
        var loaded = new SettingsService(SettingsFolder, logger).Load();

        // Assert: the first spelling of the folder is kept once, the missing folder is kept, and four entries were dropped
        Assert.Equal([folderB, missing], loaded.Styles.SharedKitFolders);
        var log = File.ReadAllText(logger.ActiveFilePath);
        Assert.Equal(4, log.Split("was dropped from the settings", StringSplitOptions.None).Length - 1);
    }

    /// <summary>
    ///     Test that a default kit id that no kit can have is replaced with none, that one with white space around it is
    ///     trimmed, and that a valid one is kept.
    /// </summary>
    /// <param name="stored">The id in the file.</param>
    /// <param name="expected">The id that is read.</param>
    [Theory]
    [InlineData("clean", "clean")]
    [InlineData("  slides-dark ", "slides-dark")]
    [InlineData("Clean", "")]
    [InlineData("-x", "")]
    [InlineData("a b", "")]
    [InlineData("../x", "")]
    [InlineData("", "")]
    public void SettingsService_Load_DefaultKitId_IsValidOrEmpty(string stored, string expected)
    {
        // Arrange
        WriteSettingsFile("{ \"styles\": { \"defaultKitId\": " + JsonSerializer.Serialize(stored) + " } }");

        // Act
        var loaded = new SettingsService(SettingsFolder).Load();

        // Assert
        Assert.Equal(expected, loaded.Styles.DefaultKitId);
    }

    /// <summary>
    ///     Test that saving style settings by an update keeps the other settings and persists the style settings.
    /// </summary>
    [Fact]
    public void SettingsService_Update_StyleSettings_KeepsOtherSettingsAndPersists()
    {
        // Arrange: full settings that are stored
        var service = new SettingsService(SettingsFolder);
        service.Save(CreateFullSettings());
        var folder = Path.Combine(_tempRoot, "team");

        // Act: change only the styles, then load with a fresh service
        service.Update(s => s with { Styles = new StyleSettings { SharedKitFolders = [folder], DefaultKitId = "dema" } });
        var loaded = new SettingsService(SettingsFolder).Load();

        // Assert
        Assert.Equal([folder], loaded.Styles.SharedKitFolders);
        Assert.Equal("dema", loaded.Styles.DefaultKitId);
        Assert.Equal("llama3", loaded.AiProvider.ModelName);
        Assert.Equal("mic", loaded.Speech.CaptureDevice);
    }

    /// <summary>
    ///     Test that the rule of a kit id accepts 1 to 48 lower-case letters, digits, and dashes that do not start with a
    ///     dash, and nothing else.
    /// </summary>
    [Fact]
    public void SettingsService_StyleSettings_IsValidKitId_FollowsTheRule()
    {
        // Act / Assert
        Assert.True(StyleSettings.IsValidKitId("a"));
        Assert.True(StyleSettings.IsValidKitId("9-lives"));
        Assert.True(StyleSettings.IsValidKitId(new string('a', 48)));
        Assert.False(StyleSettings.IsValidKitId(new string('a', 49)));
        Assert.False(StyleSettings.IsValidKitId(null));
        Assert.False(StyleSettings.IsValidKitId(string.Empty));
        Assert.False(StyleSettings.IsValidKitId("-a"));
        Assert.False(StyleSettings.IsValidKitId("A"));
        Assert.False(StyleSettings.IsValidKitId("a_b"));
        Assert.False(StyleSettings.IsValidKitId("a\n"));
    }

    /// <summary>
    ///     Test that a whitespace settings folder is rejected.
    /// </summary>
    [Fact]
    public void SettingsService_Constructor_WhitespaceFolder_ThrowsArgumentException()
    {
        // Arrange / Act / Assert: a whitespace folder is refused
        Assert.Throws<ArgumentException>(() => new SettingsService("  "));
    }

    /// <summary>
    ///     Creates settings with a non-default value in every field.
    /// </summary>
    /// <returns>The settings.</returns>
    private static AppSettings CreateFullSettings()
    {
        return new AppSettings
        {
            RecentProjects = ["C:\\Work\\a.canvaspilot", "C:\\Work\\b.canvaspilot"],
            AiProvider = new AiProviderSettings
            {
                Provider = AiProviderKind.Ollama,
                ModelName = "llama3",
                ContextWindowTokens = 32768,
                ReasoningLevel = ReasoningLevel.High,
                OllamaEndpoint = "http://example.test:11434",
                UnrestrictedAgentTools = true,
                CopilotCliPath = "C:\\Tools\\copilot.exe",
            },
            Speech = new SpeechSettings
            {
                CaptureDevice = "mic",
                PlaybackDevice = "speaker",
                SttModelId = "stt-1",
                TtsModelId = "tts-1",
                ModelParameters = new Dictionary<string, IReadOnlyDictionary<string, string>>
                {
                    ["tts-1"] = new Dictionary<string, string> { ["speed"] = "0.75", ["normalize"] = "true" },
                },
            },
            Styles = new StyleSettings
            {
                SharedKitFolders = [TestPlatformPaths.FullPath("Team", "Styles"), TestPlatformPaths.NetworkFolder],
                DefaultKitId = "slides-dark",
            },
        };
    }

    /// <summary>
    ///     Writes raw text to the settings file, creating the folder.
    /// </summary>
    /// <param name="content">The file content.</param>
    private void WriteSettingsFile(string content)
    {
        Directory.CreateDirectory(SettingsFolder);
        File.WriteAllText(Path.Combine(SettingsFolder, "settings.json"), content);
    }
}
