### StyleKitArchive

![Style Structure](StyleSubsystemView.svg)

#### Purpose

`StyleKitArchive` writes a style kit as a zip file and reads a kit from a zip file. A kit travels as one file, by mail, a
download, or a shared folder, so the import reads a file whose maker is not known and whose names the application did not
choose. The unit treats every archive as hostile and refuses a bad one as a whole. It has no user interface and uses only
`System.IO.Compression`.

#### Data Model

- `StyleImportConflict`: what to do when the place already has a kit with the id of the imported kit: `Refuse`, `Replace`,
  or `KeepBoth`.
- `StyleImportResult`: the `Kit` that was imported (read from its new folder), the `FolderPath` of that folder, the `Error`
  sentence when nothing was imported, and the `Issues` (the warnings of an imported kit, or the errors of a style file that
  was refused). `Succeeded` is true when there is a kit.
- The limits (constants of the unit): `MaxEntries` is 100, `MaxEntryBytes` is 16 MiB per file, `MaxTotalBytes` is 64 MiB
  for all files, at most 24 example files, a name of at most 200 characters and a part of at most 100.
- The layout of an archive: `style.json` and the files directly in `examples/`, at the top or all inside one folder that
  holds the style file. Nothing else is part of a kit.

#### Key Methods

**Export**: writes the canonical text of the kit (`StyleKitSerializer.Write`) as `style.json` and then each example that the
kit lists as `examples/{name}`, taken from the folder of the kit, to a temporary file next to the target, and moves the
temporary file over the target only when it is complete, so a failure leaves an existing file as it was and no partial
archive. A file of the kit that cannot be read, or a path that cannot be written, is an `IOException` and the temporary file
is deleted. The archive made by the export is the one that the import reads, so a kit that is exported and imported is the
same kit with the same example files byte for byte.

**Import**: never throws for a bad archive or a failure to write; the outcome is a `StyleImportResult`. It has these steps,
and a refusal at any of them ends the import with the folder of the target as it was:

1. *Plan* (nothing is written): the number of entries is at most 100; the name of every entry, folders included, is checked
   by the rules below; an entry that is a symbolic link (the Unix mode `0xA000` in its attributes) is refused; the size that
   the header claims of each file is at most 16 MiB, and the sizes together at most 64 MiB; then the layout is found.
2. *Layout*: when there is no `style.json` at the top, a single wrapper folder is accepted if all files are in it and it
   holds the style file (a folder named `examples` is therefore not a wrapper); every file must then be `style.json` or a
   file directly in `examples/`; a name that is repeated, ignoring case, is refused; there must be a style file; and there
   are at most 24 examples.
3. *Extract*: the files are written into a hidden folder `.import-{guid}.tmp` of the target (the catalog does not read
   folders whose names start with a period). Each destination is joined to that folder with `PathHelpers.SafePathCombine`,
   a second protection after the names were checked. The bytes are counted as they are read from the archive and not as the
   headers say, because a header can lie: reading stops with a refusal at 16 MiB for a file or 64 MiB for all, and the
   count of the bytes and the CRC-32 of each file that was read must be the size and the checksum of its header, or the
   archive is refused as damaged. The framework gives only as many bytes as the header says, so a header that claims too
   little gives a short file that does not match its checksum, and a header that claims too much ends in an error of the
   framework that is reported as a damaged file.
4. *Read*: the staged folder is read with `StyleKitSerializer.ReadFolder`, with the rules of the format. A style file with
   errors is refused with the sentence `The style file in the zip file has errors:` and the issues in `Issues`. The warning
   that the folder is not named as the id is dropped, because the staging folder has an arbitrary name.
5. *Place*: the hidden folder is moved to the folder named as the id. When that folder exists: `Refuse` ends the import
   with a sentence; `KeepBoth` takes the first number from 2 for which `{id}-{n}` is free, writes the style file again with
   that id and the name `{Name} ({n})` (the name is cut so that it stays within 60 characters), and moves the folder
   there; `Replace` moves the existing folder aside to a hidden name, moves the new folder in, and deletes the old one
   only afterwards, and when the move fails the old folder is moved back. The imported kit is read again from its final
   folder, so the paths of its examples are those of the place.
6. *Cleanup*: the hidden staging folder is removed on every outcome that does not move it into place.

**Names that are refused**: an empty name; more than 200 characters; a backslash; a leading slash; a control character or
one of `: * ? " < > |` (which also refuses a drive such as `C:`); a part that is empty, `.`, or `..`; a part with more than
100 characters, a part that ends with a period or a space, and a part whose name before the first period is a device name
(`CON`, `PRN`, `AUX`, `NUL`, `COM1` to `COM9`, `LPT1` to `LPT9`, ignoring case).

#### Interactions

- `StyleKitSerializer` (StyleKitDocument unit): writes the style file of an export, and reads and checks the style file of an
  import.
- `PathHelpers`: joins every name that comes from an archive to a folder.

#### Error Handling

An archive that is not a zip file is `The file is not a usable zip file`; a missing or unreadable file, a refused folder, and
a full disk are `The kit could not be imported` with the message of the system; both return a result and never throw. Only
a call with a blank path is an `ArgumentException`. The paths in messages have control characters replaced and are cut at 80
characters, so an archive cannot put text of its own choosing into the message of the application.
