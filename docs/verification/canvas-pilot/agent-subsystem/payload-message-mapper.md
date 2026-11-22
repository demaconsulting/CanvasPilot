### PayloadMessageMapper

#### Verification Approach

`PayloadMessageMapper` is verified with unit tests in `PayloadMessageMapperTests.cs` and with one subsystem test in
`AgentSubsystemTests.cs`. The unit tests build payloads by hand from the real payload types (the mapper reads only the
properties of the payload and does not decode a PNG, so the bytes of an image are one or two bytes that identify it),
which lets a test control exactly the marks, the parts of the source, the notes, and the paths. The central test
compares the text of a message with an attachment and a picture with two marks to a golden text, written in the test as
a literal, line by line as the model reads it; the other tests each pin one rule (the order and cap of the images, the
parts of the source, the paths, the one-line rule, the rounding and the culture). The project folder is a path of the
test platform that need not exist, because the mapper works on the spelling of paths.

The subsystem test uses the real parts that make a payload: a real picture drawn by the render service, real marks in
the mark registry, a real composed message, and the real payload builder with the real composer of mark pictures. It
maps the payload and checks the text for the picture, the marks, and the reason that an SVG cannot name the parts under
a mark, and that the images are three real PNG files in the stated order.

#### Test Environment

The unit tests use no file and no display. The subsystem test makes one temporary project folder with an SVG file and
deletes it. The culture test changes the culture of the thread and restores it. No network or external service is used.

#### Acceptance Criteria

- All unit tests pass with zero failures.
- A message with one attachment and a picture with two marks gives exactly the golden text, the role user, and the
  contents text, the annotated picture, and the close-ups of mark 1 and mark 2, all of the media type `image/png`, and
  the text has no part of the path where the user had the attachment.
- A message of only text gives one text content equal to the text with its line breaks made line feeds and its trailing
  white space removed, and no block of context.
- With two pictures, the annotated pictures come first in the order of the pictures, then the close-ups in the order of
  the numbers of the marks whichever picture each mark is on, each with its own bytes, and the list of images in the
  text agrees.
- With twelve marks, ten close-ups are sent, the first ten numbers, and the text says `2 more marks (11, 12)`; with
  eleven marks it says `1 more mark (11)`; with ten it says nothing about close-ups left out.
- Of seven parts under a mark five are written, in order, and `more elements are left out` is added; a mark that the
  payload says is truncated gets the same words; a mark with no part says `none under the mark`; a mark of a picture
  that cannot say gives `not available` with the reason, or `the picture cannot say` when there is no reason.
- A folder attachment says how many files, in the singular and the plural; a picture inside the project has its relative
  path with forward slashes; a picture outside it has its file name followed by `(outside the project)`; no text
  contains a folder of the project folder or of the user.
- A note with line breaks, quotes, and a line like the heading of the block stays on one line with single quotes; values
  over 200 characters end with an ellipsis; the heading of the block is on exactly one line.
- The text is the same in the German and Arabic cultures and in the invariant culture; 10.5 is written 11, 2.5 is
  written 3, 1234.4 is written 1234, and -0.4 is written 0.
- A missing payload, a missing folder, and a blank folder are refused.

#### Test Scenarios

**PayloadMessageMapper_Map_AttachmentAndTwoMarks_IsTheGoldenTextWithThreeImages**: An attachment inside the project, a
Mermaid picture of 800 by 600 pixels, mark 1 (a rectangle with a note and two parts of the source) and mark 2 (an arrow
with no note on a picture that cannot name parts) are mapped. The text is the golden text, the contents are the text and
three PNG images named `picture-1.png`, `mark-1.png`, and `mark-2.png`, and the original place of the attachment does
not appear. This scenario is tested by `PayloadMessageMapper_Map_AttachmentAndTwoMarks_IsTheGoldenTextWithThreeImages`.

**PayloadMessageMapper_Map_TextOnly_IsTheTextAlone**: A payload of only text with Windows line breaks and trailing white
space gives one content, the text with line feeds and no trailing white space. This scenario is tested by
`PayloadMessageMapper_Map_TextOnly_IsTheTextAlone`.

**PayloadMessageMapper_Map_TwoPictures_AnnotatedPicturesFirstThenCropsInMarkOrder**: Picture 1 has the marks 2 and 4 and
picture 2 has the marks 1 and 3. The images are `picture-1`, `picture-2`, then the close-ups of the marks 1, 2, 3, and
4, with the bytes of each, and the text lists the images with the right picture for each close-up. This scenario is
tested by `PayloadMessageMapper_Map_TwoPictures_AnnotatedPicturesFirstThenCropsInMarkOrder`.

**PayloadMessageMapper_Map_MoreThanTenMarks_SendsTenCropsAndSaysWhichAreLeftOut**: Twelve marks, given in reverse order,
send ten close-ups of the marks 1 to 10 and the sentence for marks 11 and 12; eleven marks give the singular sentence;
ten marks give no sentence. This scenario is tested by
`PayloadMessageMapper_Map_MoreThanTenMarks_SendsTenCropsAndSaysWhichAreLeftOut`.

**PayloadMessageMapper_Map_SourceElements_AreLimitedToFiveAndTruncationIsSaid**: Seven parts under a mark give five and
the sentence that more are left out and not the sixth; two parts with the truncation flag give the sentence; no part
gives `none under the mark`. This scenario is tested by
`PayloadMessageMapper_Map_SourceElements_AreLimitedToFiveAndTruncationIsSaid`.

**PayloadMessageMapper_Map_UnavailableWithoutReason_StillSaysThePictureCannotSay**: A mark with a blank note and parts
that are not available and have no reason gives `no note` and `not available (the picture cannot say)`. This scenario is
tested by `PayloadMessageMapper_Map_UnavailableWithoutReason_StillSaysThePictureCannotSay`.

**PayloadMessageMapper_Map_Paths_AreRelativeToTheProjectAndNeverRevealFolders**: Attachments of the kinds folder (with
three files and with one), and image; a picture below two folders of the project and a picture outside it. The text has
the relative paths with forward slashes, the file name and `(outside the project)` for the outside picture, and none of
the folders of the user or of the project. This scenario is tested by
`PayloadMessageMapper_Map_Paths_AreRelativeToTheProjectAndNeverRevealFolders`.

**PayloadMessageMapper_Map_HostileValues_AreOneLineAndCut**: A note with a line break, the heading of the block, and
double quotes, an identifier with a line break, a text of 300 characters, and a name of 300 characters give one-line
values with single quotes and an ellipsis at 200 characters, and the heading appears on one line only. This scenario is
tested by `PayloadMessageMapper_Map_HostileValues_AreOneLineAndCut`.

**PayloadMessageMapper_Map_SameMessage_SameResultInEveryCultureWithRoundedPixels**: A mark with fractional boxes is
mapped in the German, Arabic, and invariant cultures and a fourth time; the texts are equal, the numbers are whole and
rounded as the criteria say, and the images have the same names. This scenario is tested by
`PayloadMessageMapper_Map_SameMessage_SameResultInEveryCultureWithRoundedPixels`.

**PayloadMessageMapper_Map_BadArguments_AreRefused**: A missing payload, a missing folder, and a blank folder are
refused. This scenario is tested by `PayloadMessageMapper_Map_BadArguments_AreRefused`.

**AgentSubsystem_SendMessage_RealPayloadWithMarks_MapsToTextAndPngImagesInOrder**: The subsystem scenario that is
described in the verification of the Agent subsystem. This scenario is tested by
`AgentSubsystem_SendMessage_RealPayloadWithMarks_MapsToTextAndPngImagesInOrder`.
