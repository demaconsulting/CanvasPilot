package CanvasPilot_PushToTalkBinding {
    private import SoftwareItemMetadata::*;
    private import CanvasPilot_SettingsService::*;

    part def PushToTalkBinding {
        doc /* The key, or the chord of Ctrl, Alt, or Shift and a key, that is held to talk. It has F9 as its default (F8, the first default, is swallowed by the function keys of many laptops) and refuses, with a sentence that says why and what to do, a key that would take away typing or that the system owns: a modifier alone, the keys of editing and moving, a letter, a digit, or a space without Ctrl or Alt, Ctrl+Space, Alt+Space, Alt with a letter or a digit alone, Ctrl+Alt, any chord with the Windows key, the shortcuts of editing and of the application, Alt+F4, and the function keys F1, F8, F10, F11, and F12. It writes a key as text with the modifiers in the order Ctrl, Alt, Shift and then the key (for example F9 or Ctrl+Shift+Space), reads that text back whatever its case or spacing and nothing from a text that is empty, is not a key, repeats a modifier, has no key after its modifiers, or is not safe, keeps nothing for the default so that it can follow a later release, and reads the old default F8 as the default. It tells whether a key press is the key with exactly the chosen modifiers and whether letting go of a key ends the chord (the key itself or any of its modifiers). Its setting, which the window, the status bar, the welcome screen, and the chat panel share, is the observable value of the key in effect: it follows the talk key of the speech settings (the default for a missing or unsafe value), announces its change, and stops following when it is disposed. The setting is a supporting type of this unit, which has no part definition of its own. */

        metadata info : SoftwareInfo {
            sourceFiles = (
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/PushToTalkBinding.cs",
                "src/DemaConsulting.CanvasPilot/SpeechSubsystem/PushToTalkSetting.cs"
            );
            testFiles = ("test/DemaConsulting.CanvasPilot.Tests/SpeechSubsystem/PushToTalkBindingTests.cs");
            designFiles = ("docs/design/canvas-pilot/speech-subsystem/push-to-talk-binding.md");
            verificationFiles = ("docs/verification/canvas-pilot/speech-subsystem/push-to-talk-binding.md");
            requirementFiles = ("docs/reqstream/canvas-pilot/speech-subsystem/push-to-talk-binding.yaml");
        }

        // Follows the talk key of the speech settings and announces its change.
        dependency PushToTalkBinding to SettingsService;
    }
}
