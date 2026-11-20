## Avalonia Verification

This document provides the verification evidence for the Avalonia OTS software item. Requirements for this OTS item
are defined in the Avalonia OTS Software Requirements document.

### Required Functionality

Avalonia (the `Avalonia`, `Avalonia.Skia`, `Avalonia.Themes.Fluent`, `Avalonia.Fonts.Inter`, `Avalonia.Desktop`, and
`Avalonia.Headless.XUnit` packages) shows modal dialogs over a window, shows menu items that follow their commands,
keeps bound controls and view models in step, holds the bytes of a bitmap unchanged, and carries a bitmap through the
clipboard. The application relies on each of these behaviors, so each has a requirement and a test.

### Verification Approach

Avalonia has no self-validation mode. It is verified by integration tests in `test/OtsSoftwareTests/AvaloniaTests.cs`
that run the framework's headless platform with the application's own `App` and call the framework with the same
kinds of controls the application uses: windows, a menu item with a command, bound text boxes, a writable bitmap in an
image control, and the clipboard of a window. The tests are the evidence that the framework behaves as the application
relies on, and they are run again before a version upgrade is accepted. The user interface tests of the application
(`test/DemaConsulting.CanvasPilot.UiTests`) repeat part of this evidence through the application's own views.

The headless platform does not show a real window, does not use the operating system clipboard, and cannot show the
file pickers. How the application looks on a real display, how a copied picture pastes into another program, and the
file pickers are checked by hand before a release. These checks are not automated and are not evidence for a
requirement.

### Test Scenarios

#### Avalonia_Window_ShowDialog_CompletesWithCloseResult

**Scenario**: An owner window is shown, a second window is shown modally over it and then closed with a text result.

**Expected**: The dialog is visible while it is open, the pending task completes with the text it was closed with, and
the dialog is not visible afterwards.

**Requirement coverage**: `CanvasPilot-OTS-Avalonia-ShowModalDialogs`.

#### Avalonia_MenuItem_FollowsCommandAndRunsIt

**Scenario**: A window shows a menu whose item is bound to a command that cannot run. The command is then changed so
that it can run, and the item is chosen with the mouse.

**Expected**: The item is disabled while the command cannot run and enabled after it can, it keeps its automation
identifier, and the command runs once when the item is chosen.

**Requirement coverage**: `CanvasPilot-OTS-Avalonia-ShowMenusThatFollowCommands`.

#### Avalonia_DataBinding_FollowsViewModelInBothDirections

**Scenario**: A window title and a text box are bound to the same property of a view model. The property is changed,
then the text box text is edited.

**Expected**: The title and text box show the property, follow its change, and the edit reaches the view model.

**Requirement coverage**: `CanvasPilot-OTS-Avalonia-BindControlsToViewModels`.

#### Avalonia_WriteableBitmap_KeepsWrittenPixelsAndIsShownByImageControl

**Scenario**: A writable bitmap of 2 x 1 pixels in blue, green, red, alpha order with premultiplied alpha receives
two pixels through its framebuffer, is shown by an image control, and is read back.

**Expected**: The pixels read back are the pixels written, the size and pixel format are those requested, and the image
control holds the bitmap.

**Requirement coverage**: `CanvasPilot-OTS-Avalonia-DisplayPixelBuffers`.

#### Avalonia_Clipboard_Bitmap_RoundTrips

**Scenario**: A bitmap of 3 x 2 pixels is put on the clipboard of a window and read back.

**Expected**: A bitmap of the same size is returned.

**Requirement coverage**: `CanvasPilot-OTS-Avalonia-TransferPicturesViaClipboard`.
