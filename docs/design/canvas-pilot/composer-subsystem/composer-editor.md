### ComposerEditor

![Composer Structure](ComposerSubsystemView.svg)

#### Purpose

`ComposerEditor` is the view of the message that the user is composing: one editing surface in which typed text,
dictated text, and chips for attached files, folders, pictures, and marks stand in order, with a caret, a selection,
wrapping, and the input methods of the operating system. It shows what `ComposerViewModel` holds and reports everything
the user does as that view model's actions, so that every rule of editing, undo, and redo stays in the view model and
the document, where it is tested without a window. The unit exists so that the surface can be the library's editing
control (see the *AvaloniaEdit* design) while no rule of the message lives in it.

#### Data Model

The unit is made of these parts, all in the user interface layer:

- `ComposerEditorView` (a `UserControl`, bound to a `ComposerViewModel`): hosts the editing surface (`TextEditor`), the
  strip of live dictated words above it (an accent tint with italic text, shown while `IsDictating`), the line for the
  status text below it, the hint that a project must be open, the placeholder (`ComposerPlaceholder`, the text
  `ComposerViewModel.PlaceholderText` in the tertiary text color, shown while `ShowsPlaceholder` is true, and not
  hit-testable, so that the surface under it still takes the pointer), and the highlight shown while a drag is over
  the text. The surface has no background and no border of its own, a 48 pixel minimum height, the body type size and
  the application font, because the card that the host puts around the view is the field. `ThumbnailProvider` (get and
  set) gives the chips their
  pictures; `UndoRedoTarget` is the view model, which the Edit menu finds from the focused control's data context;
  `SurfaceText` and `IsConsistent` expose the rule that the surface equals the message; `FocusEditor` puts the focus in
  the surface; `PasteAsync(clipboard)` and `HandleDrop(data, point)` are the entry points that the paste command and the
  drop handler call.
- `ComposerTextEditor`: the editing surface (a `TextEditor`) with an automation peer, because the library's control has
  none and the platform reports it as an unnamed scroll area. The peer is an edit field (`AutomationControlType.Edit`)
  with a value pattern: `Value` is the message with each chip named in brackets and the chip of a mark that has a note
  named with its note, such as `[Mark 1: make it red]` (`ValueProvider`, set by the view to `DescribeForScreenReader` of
  the message with `ComposerViewModel.GetMarkNote`), `SetValue` replaces the whole message as one step through
  `ValueSetter` (chips are not kept) and is read-only while the message cannot be edited, and a change of the text is
  reported as a change of the value. The surface and its text area are named "Message", and the peer takes the focus
  into the text area.
- `ChipElementGenerator`: the library's inline element generator, which turns each placeholder character of the surface
  into an inline control.
- `ChipView` (a `UserControl`): one chip, a pill at least 24 pixels high (the shared `ChipBorder` theme: a raised
  surface with a subtle border and a fully rounded corner) with an icon for its kind (file, folder, picture, mark) or
  the picture from the provider, its name (trimmed with an ellipsis beyond 180 pixels), a delete button (which
  `IsDeleteButtonVisible` can hide; the transcript of sent messages hides it), and a tooltip with the full path or
  `Mark n on the picture`, followed by `: <note>` when the mark has a note (`GetMarkToolTip`); the same text is the
  chip's `AutomationProperties.HelpText`, so that a screen reader gives it too. `IsNoteEditable` (off by default, on for
  the chips of the message being written) gives the chip of a mark a context menu with the item *Edit note…*
  (`ComposerChipEditNote`, named `Edit the note of Mark n`, showing the key F2), and makes a double-click on the chip
  ask for the change too; both raise `EditNoteRequested`, and the double-click is handled so that the surface does not
  select a word. `IsNoteEditEnabled` (on by default) says whether that offer can be used now: while it is off the item
  is disabled, and neither the item nor a double-click raises `EditNoteRequested`. A mark chip has the class `mark`,
  which tints the pill with `CpMarkTint`, gives it the `CpMark` outline, and fills its icon with `CpMark`, so it reads
  like the mark on the picture. The delete button shows a small cross icon; its accessible name and tooltip are
  `Remove {label}`, set when the chip is shown, so that the screen reader says which chip.
- `IChipThumbnailProvider` and `ImageChipThumbnailProvider`: the contract that gives a chip its small picture
  asynchronously, and the default implementation for attached picture files.
- `DictatedTextBackgroundRenderer` and `DictatedRange`: draws the background behind dictated text; `GetRanges` finds the
  dictated stretches of a message as offsets.
- `DataTransferReader`: reads a drop or the clipboard into the view-neutral `DroppedContent` of the composer.

**Positional mapping.** The text of the surface is exactly `ComposerDocument.FlatText`: every text segment's text, and
one placeholder character (U+FFFC) for each chip. The n-th placeholder in the surface is the n-th chip of the message. A
chip therefore has no identity in the surface: offsets are equal on both sides, the generator asks the view model for
chip n, and nothing in the surface can get a chip out of step with the message. The view model never receives a
placeholder character from the user, because typing, pasting, and dropping refuse or clean it before it enters.

**State of the view.** The view holds the view model it follows, the chips that the surface was last drawn for (to draw
again when one chip takes the place of another and leaves the text unchanged), the caret of the surface when its current
update began (what an undo restores), the changes of the current update, and three flags: the view is writing the
message into the surface, the view is handing an edit of the surface to the view model, and an update of the surface's
document is in progress.

#### Key Methods

**Setup.** The surface is configured once: its own undo stack is limited to zero and its undo and redo commands are
removed, so that the message's history is the only one; key bindings for Ctrl+Z, Ctrl+Y, and Ctrl+Shift+Z (with the
Command key in place of Ctrl on macOS, the rule that the surface applies to its own clipboard keys) call the view
model's `Undo` and `Redo`; the surface's drag of text inside itself, hyperlinks, the overwrite toggle, whole-line cut
and copy, and Tab input are switched off (a drag of text inside the surface would be a removal and an insertion that
breaks the chips); the chip generator and the dictated-text renderer are added to the text view; the paste, copy, and
cut commands are replaced by the view's own handlers; and drops are allowed on the text view and on the surface, because
the library allows them only for its own drag of text, which is switched off.

**Mirror from the surface into the message.** The view notes the start of each update of the surface's document and the
caret then, collects each change (offset, removed length, inserted text), and at the end of the update hands the update
to the view model as one action. One change is passed exactly, with the caret from the start of the update as the caret
before; several changes, such as a line break together with the indentation of the line, are passed as the one change
that is the difference between the message and the surface (the common start and end are skipped). The view model makes
one undo step of it: a typed character, a Backspace or Delete, a commit of an input method, typing over a selection, and
a cut are each one change. The text typed is cleaned by the message. After the call the view compares the surface with
the message and makes the surface equal to the message when they differ, which restores the surface when the message
cleaned the text or refused the change, and tells the view model where the caret ended up.

**Mirror from the message into the surface.** Every `Changed` of the view model that the view did not cause (an undo or
a redo, a dictated block, a drop, a paste, a mark, a deleted chip, a changed project) is written into the surface as the
smallest change that makes the surface equal to the message, under the flag that keeps the surface's events from being
taken for the user's edit. The caret is put where the view model has it and the selection is cleared. The lines are
drawn again when the chips of the message are not the chips that were drawn, and the dictated background in any case.
The surface is read-only while the view model cannot edit.

**Caret and selection.** The caret and the selection of the surface are told to the view model after each change of
them, except while the surface is being changed, so that new text, dictated words, chips, and pasted items go where the
user is.

**Chips.** The generator counts the placeholders before an offset to know which chip to ask for and makes a new
`ChipView` for it each time a line is drawn; the control is cheap and the picture is cached by the provider. `ChipView`
shows the icon at once and asks the provider for the picture when it is attached to the tree, shows it when it arrives,
and cancels the request when it is detached; a provider that fails leaves the icon. Pressing the delete button raises
`DeleteRequested`; the view finds the chip's offset by its identity (two chips of the same file are equal but are not
the same chip) and calls `ComposerViewModel.DeleteChip`, one step, which also works while the focus stays in the surface
because the button does not take it. Backspace and Delete over a chip are one change of one placeholder character and
therefore one step too.

**Notes of marks.** When the generator makes the control of a mark chip, the view asks `ComposerViewModel.GetMarkNote`
for the mark's note, shows it in the tooltip and the help text, turns `IsNoteEditable` on, and sets `IsNoteEditEnabled`
to `ComposerViewModel.CanEdit`. The view follows `ComposerViewModel.MarkNoteChanged` and draws the lines again, so every
chip is made again with its current note; it also draws them again when `CanEdit` changes (a send starts or ends, or the
project opens or closes), which it notices with the read-only state of the surface, so that the Edit note item is
enabled exactly while the message can be edited. `DescribeForScreenReader(segments, getMarkNote)` gives the text that
the automation peer reads: each text segment as it is, the chip of a mark with a note that is not empty as
`[{label}: {note}]`, and every other chip as `[{label}]`; it throws `ArgumentNullException` for a missing argument. A
double-click on the chip or its Edit note item calls `ComposerViewModel.RequestMarkNoteEdit` with the chip's mark. Chips
cannot take the focus inside the surface, so the keyboard way is a key binding of F2, which calls
`RequestMarkNoteEditAtCaret` for the mark chip just before the caret, or else just after it; the view model refuses
both while the message cannot be edited. None of them changes the message. F2 is never a talk key (see
*PushToTalkBinding*), so the window does not take it before the surface.

**Previews.** `ImageChipThumbnailProvider` makes a preview only for a picture chip whose file exists and is not larger
than 20 MB, decodes it straight to a width of 64 pixels on a background thread, keeps up to 64 previews keyed by path,
time of the last write, and length, and remembers files that have none. SVG files and files that the decoder does not
know have no preview and keep their icon. It never opens a chip that is not a picture and never throws.

**Typed text.** The surface refuses text that holds the placeholder character or a control character other than a tab or
a line break, before it enters, by marking the entering event as handled.

**Paste, copy, cut, and drop.** The paste command reads the clipboard through `DataTransferReader.ReadAsync` and gives
the `DroppedContent` to `ComposerViewModel.HandlePaste`. Copy and cut put `DescribeForClipboard` of the selected
segments on the clipboard, in which each chip is its name in brackets, so the placeholder character never reaches
another program; cut then deletes the selection through the view model as one step. A drag is accepted, with a copy
effect and a highlight, only when the message can be edited and the drag brings files, a picture, or text; a drop is
read by `DataTransferReader.Read` and given to `ComposerViewModel.HandleDrop` with the offset under the pointer.
`DataTransferReader` takes files and folders first (by their local paths), then a picture (encoded as PNG), then text,
so that the path text that a file manager sends with its files is not inserted; it never opens or reads the files; and
it reports content it cannot read as empty.

#### Error Handling

Programming errors throw: null arguments (`ArgumentNullException`). Everything that depends on the machine, the user, or
another program is handled without an exception reaching the interface thread: a clipboard that cannot be read or a
picture that cannot be put on it sets the status text of the view model; a drag that brings nothing acceptable is
refused with no effect; a chip with no picture keeps its icon; a change that does not fit the message makes the surface
equal to the message; and a command that arrives while the message cannot be edited does nothing. The input method of
the operating system is the library's; the commit of an input method is verified, but the composition that comes before
it cannot be verified without a real input method (see the verification document).

#### Dependencies

- **ComposerViewModel**: every action on the message, the document to show, the caret, and the state to bind; and
  through it **ComposerDocument**, **ComposerHistory**, **AttachmentIntake**, and the note changes of the
  **MarkRegistry**.
- **DroppedContent** (of `AttachmentIntake`): what the reader hands over.
- **AvaloniaEdit** (OTS): the editing surface, its inline objects, change reports, and replaceable commands; see the
  *AvaloniaEdit* design.
- **Avalonia** and **CommunityToolkit.Mvvm** (OTS): controls, drag and drop data, the clipboard, bitmaps, and the key
  binding commands.
- **ThemeResources** (of the AppShell subsystem, by resource keys only): the colors, tints, mark colors, type sizes, the
  icons, and the `ChipBorder` and `IconButton` themes.

#### Callers

- The chat panel hosts the view in the card of the message, gives it the view model of the message and a provider of
  pictures for mark chips, and shows the sent messages with the same chip control, without its delete button
  (`IsDeleteButtonVisible`) and without the change of notes (`IsNoteEditable` stays off). It listens for the Enter key
  before the view does and sends the message when Enter is pressed on its own and the message can be sent; the view
  itself writes a line break for Enter, and that is what the panel leaves to it in every other case.
- The Edit menu and the keys reach undo and redo through `UndoRedoTarget`, which is the view model.
