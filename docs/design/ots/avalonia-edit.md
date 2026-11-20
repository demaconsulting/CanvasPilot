## AvaloniaEdit

This document describes the integration and usage design for the `AvaloniaEdit` OTS software item.

### Purpose

AvaloniaEdit (the `Avalonia.AvaloniaEdit` package) is the text editing control, a port of a well-known desktop editor
control to Avalonia, on which the message composer is built. It was chosen because the composer needs what no control of
the user interface framework offers together: a caret, selection, wrapping, word navigation, scrolling, accessibility,
and the input methods of the operating system, in a text in which small controls (the chips of attached files, folders,
pictures, and marks) stand inline, between the words. The framework's text box has no inline controls and its read-only
text blocks have no caret. An own control with its own text layout, caret, selection, and input method handling was
rejected as a large amount of code to write, review, and verify. AvaloniaEdit is published under the MIT license,
targets the framework version the application uses, and is a third-party library from an unrelated program, so it is
treated as OTS. It is the only package that was added for the composer.

### Features Used

- The editing surface (`TextEditor`) with its document (`TextDocument`): typing, deletion, selection, caret, word
  wrapping, scrolling, and the input method client, all of which are used as they are.
- Change reports of the document: `Changed` (offset, removed text, inserted text), `UpdateStarted`, and
  `UpdateFinished`, which the composer turns into one action on the message for each update.
- Inline objects: a generator (`VisualLineElementGenerator`) that returns an `InlineObjectElement` with the chip control
  for each placeholder character; the caret and the deletion keys treat the object as one character.
- The undo stack, whose limit is set to zero, and the removal of the surface's undo and redo commands, so that the
  message's history is the only one; key bindings of the surface for the undo and redo gestures.
- Command bindings: the surface's paste, copy, and cut bindings are replaced by the composer's own.
- Text entry events (`TextEntering`) to refuse typed text before it enters; the caret and selection events and
  properties; the options of the surface (the drag of text inside the surface, hyperlinks, overwrite, whole-line copy,
  and Tab input are switched off).
- Background renderers (`IBackgroundRenderer`) and the geometry builder, which draw the background behind dictated text.
- The drop and drag-over events of the text view, with drops allowed by the composer.
- The control's theme, a style that `App.axaml` includes next to the framework's theme.

### Integration Pattern

The control is a NuGet package reference of the application library, with its style included in `App.axaml`. It is used
only by the `ComposerEditor` unit (`ComposerEditorView`, `ChipElementGenerator`, and `DictatedTextBackgroundRenderer`);
no view model refers to it. The composer treats the control as a surface that shows the message and reports what the
user does: it holds no state that the message does not hold, because the document of the control is written from the
message and read back only to learn what the user did. The control raises the same change event for the composer's own
writes as for the user's typing, and refuses a change of the document made from inside the change event, so the composer
writes only when it is not inside an update, under a flag that tells its own writes from the user's, and brings the
surface and the message together when an update finishes. The limit of the undo stack belongs to the document and is set
again if the document were ever replaced; the composer never replaces it. AvaloniaEdit has no self-validation mode; the
OTS tests in `AvaloniaEditTests.cs` run the control headless with the same calls the composer makes and are the evidence
that it behaves as the composer relies on. A new version of the control is accepted only after those tests and the user
interface tests of the composer pass again. How input methods behave, and how the operating system's clipboard and drag
and drop reach the control, are checked by hand before a release.
