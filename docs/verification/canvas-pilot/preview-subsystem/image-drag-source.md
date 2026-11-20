### ImageDragSource

#### Verification Approach

`ImageDragSource` is verified by unit tests in `ImageDragSourceTests.cs`. They use a hand-written encoder that returns a
PNG signature and counts its calls (so reuse can be shown), real files in a temporary folder, and a real
`RollingFileLogger` in one test; the folder that cannot be created is a path that is already a file. The unit is also
exercised with the real render service by the subsystem test and through the application's composition by the system
test, and the view's part (the handling of the pointer and the data offered to the drag) is verified with the
`PreviewPane` unit; those scenarios are described in the verification documents of `PreviewPane`, the `Preview`
subsystem, and the system.

What no automated test can show is whether a particular program accepts the dropped file or bitmap. That is checked by
hand before a release (see *Manual Acceptance Check*) and is not evidence for a requirement.

#### Test Environment

Unit tests use the standard test environment with temporary local folders and real file I/O; no network, external
services, or display are required. The manual check needs Windows 10 or 11 with a desktop session and the programs
named in it.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A picture is written once as a file that holds the encoder's PNG bytes, named after its source file (without the image
  source extension, and for Vega-Lite files without `.vl.json`) and the first eight characters of its pixel
  fingerprint; a different picture of the same file has another file.
- Characters that a file name cannot hold are replaced, a long name is limited to 60 characters, and the file is
  always directly in the drag folder.
- A folder that cannot be created, and an encoder that fails, give `null`, are logged, and leave no file behind.
- Purging deletes only own files directly in the folder: not other names, not almost-matching names, not folders or
  their content; a missing folder gives zero and is not created.
- The manual acceptance check has been performed for the release and its outcome recorded for each target.

#### Test Scenarios

**ImageDragSource_Prepare_Picture_WritesValidPngWithSuggestedName**: A picture of `flow.mmd` is prepared for a folder
that does not exist yet. The file is `flow-` and the first eight characters of the pixel fingerprint, `.png`, in the
folder; it starts with the PNG signature; it is the only entry in the folder. This scenario is tested by
`ImageDragSource_Prepare_Picture_WritesValidPngWithSuggestedName`.

**ImageDragSource_Prepare_VegaLiteFile_NamesFileAfterStemWithoutCompoundExtension**: A picture of `sales.vl.json` gets
the file name `sales-` and the fingerprint start, `.png`. This scenario is tested by
`ImageDragSource_Prepare_VegaLiteFile_NamesFileAfterStemWithoutCompoundExtension`.

**ImageDragSource_Prepare_SamePicture_ReusesFile**: Two equal pictures give the same path and one encoding; a picture
of the same file with another size gives another path; the folder holds two files. This scenario is tested by
`ImageDragSource_Prepare_SamePicture_ReusesFile`.

**ImageDragSource_Prepare_UnusualSourceName_ProducesSafeFileNameInsideFolder**: A source name with a character that no
file name may hold gives a name with an underscore in its place, and a source name of 200 characters gives a file name
of 60 characters, a hyphen, eight characters, and `.png`; both files are directly in the folder. This scenario is
tested by `ImageDragSource_Prepare_UnusualSourceName_ProducesSafeFileNameInsideFolder`.

**ImageDragSource_Prepare_UnwritableFolder_ReturnsNullAndLogs**: When the folder is the path of an existing file, the
result is `null`, that file is unchanged, and the log says that the picture could not be prepared for dragging and
names its source file. This scenario is tested by `ImageDragSource_Prepare_UnwritableFolder_ReturnsNullAndLogs`.

**ImageDragSource_Prepare_EncoderFails_ReturnsNullAndLeavesNoFile**: An encoder that throws an `IOException` gives
`null` and leaves no file, in particular no half-written file. This scenario is tested by
`ImageDragSource_Prepare_EncoderFails_ReturnsNullAndLeavesNoFile`.

**ImageDragSource_Purge_FolderWithOtherFiles_RemovesOnlyOwnFiles**: The folder holds two prepared pictures, the
leftover of an interrupted write, files named `notes.txt`, `photo.png`, a name with seven digits and a name with capital
letters, and a folder with a matching name that contains a matching file. Purging returns three, removes the two
pictures and the leftover, and leaves everything else. This scenario is tested by
`ImageDragSource_Purge_FolderWithOtherFiles_RemovesOnlyOwnFiles`.

**ImageDragSource_Purge_MissingFolder_ReturnsZero**: Purging a folder that does not exist returns zero and does not
create it. This scenario is tested by `ImageDragSource_Purge_MissingFolder_ReturnsZero`.

**ImageDragSource_New_InvalidArguments_Throw**: A missing encoder, a blank folder, and a missing picture are refused,
and the default folder ends with `CanvasPilot` and `drag`. This scenario is tested by
`ImageDragSource_New_InvalidArguments_Throw`.

#### Manual Acceptance Check

Whether other programs accept what the preview offers cannot be shown without them, so it is checked by hand on
Windows before a release, on the build that is to be released. The check is not automated and is not evidence for a
requirement; whoever performs it records the outcome for each target.

1. Start CanvasPilot, open or create a project, and select a source file, for example a Mermaid file `flow.mmd`, so
   that a picture is shown in the preview.
2. Press the left button on the picture and move the pointer about ten pixels out of the preview. The pointer shows
   the copy symbol. Drop the picture on each target below and note the result:
   - **A Windows Explorer folder or the desktop**: a file `flow-` followed by eight characters and `.png` appears and
     opens as the shown picture.
   - **A PowerPoint slide**: the picture is inserted at the drop point.
   - **An Outlook new message with an HTML body**: the picture is inserted in the body.
   - **Paint**: the picture opens or is pasted into the canvas.
3. Check the negative cases: a click on the picture, without moving, starts no drag; no drag starts while no picture is
   shown; pressing Escape during a drag cancels it and drops nothing.
4. Close CanvasPilot and check that `%LOCALAPPDATA%\CanvasPilot\drag` holds no `.png` file. Then start it again.
5. If a target refuses the picture or inserts something wrong, record which target and which format (the file or the
   bitmap) is the cause; the format that breaks a target is then removed from the offered item and the limitation is
   described in the user guide.
