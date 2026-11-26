## Avalonia

This document describes the integration and usage design for the `Avalonia` OTS software item.

### Purpose

Avalonia is the cross-platform .NET user interface framework that shows CanvasPilot's windows, menus, dialogs, and
pictures. It was chosen because one code base then produces a native-looking desktop application, the same
framework is used by the other DEMA Consulting desktop applications, and its headless platform lets the user
interface be tested without a display. The packages are pinned to one version: `Avalonia` (the framework),
`Avalonia.Skia` (the drawing back end), `Avalonia.Themes.Fluent` (the control theme), and `Avalonia.Fonts.Inter`
(the application font) in the application library, `Avalonia.Desktop` (the desktop platform support) in the
executable project, and `Avalonia.Headless.XUnit` (the headless test platform) in the test projects. The framework is
a third-party library from an unrelated program, so it is treated as OTS.

### Features Used

- Windows and modal dialogs: `Window.Show` and `Window.ShowDialog<T>` with the close result, and the storage
  provider and clipboard that a window offers.
- Controls: `Menu` and `MenuItem` with commands and automation identifiers, text boxes, buttons, list and tree
  controls, scroll viewers, and the `Image` control.
- Data binding: compiled bindings (every view declares its view model type), including two-way bindings of text
  boxes, and the data templates that the view locator uses to show a view for each view model.
- Automation peers: `ControlAutomationPeer.CreatePeerForElement` and the peers of `ComboBox` and `ListBox`, which a
  screen reader reads. The application relies on two facts of them: a closed `ComboBox` reports as its value the
  `ToString` of its chosen item when that item is not a control, and a `ListBoxItem` whose template is a panel is named
  by the `ToString` of its item unless it carries `AutomationProperties.Name`. Every item type that the application
  shows in such a list therefore returns its shown words from `ToString`, or its row is named with
  `AutomationProperties.Name`, as the rows of the page rail of the settings dialog are.
- Imaging: `WriteableBitmap` in blue, green, red, alpha order with premultiplied alpha, filled through its
  framebuffer.
- Drag and drop out of the application: `DataTransfer` and `DataTransferItem` with a file (`SetFile`, from
  `IStorageProvider.TryGetFileFromPathAsync`) and a bitmap (`SetBitmap`) in one item, and
  `DragDrop.DoDragDropAsync`, which the preview starts when the user drags the shown picture.
- Application startup: `AppBuilder` with the desktop platform, the Skia back end, and the Inter font.
- The headless platform with `AvaloniaFact` tests, used by the user interface tests and by the OTS tests.

### Integration Pattern

The framework is a NuGet package reference of the application library; the executable project adds the desktop
platform and starts the application with `AppBuilder`. Views are written in AXAML next to a code-behind file, bind
only to their view model, and are found for a view model by the view locator, so that view models never reference
controls. Only the main window knows the window services (dialogs, pickers, and the clipboard), and it offers them to
the rest of the application through the `IDialogService` seam, so that view models and their unit tests need no
framework controls. Avalonia has no self-validation mode; the OTS tests in `AvaloniaTests.cs` run the headless
platform with the same kinds of controls and calls the application uses, and are the evidence that the framework
behaves as the application relies on. The user interface tests of the application repeat part of this evidence
through the application's own views. A framework upgrade re-runs both before it is accepted. How the application looks
on a real display, and how the operating system clipboard and drag and drop behave, are checked by hand before a
release; `DragDrop.DoDragDropAsync` never completes under the headless platform, so the application calls it through a
replaceable operation in the preview view and the tests check what is offered to it.
