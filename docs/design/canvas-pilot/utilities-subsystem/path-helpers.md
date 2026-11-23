### PathHelpers

![Utilities Structure](UtilitiesSubsystemView.svg)

#### Purpose

`PathHelpers` is a static utility class that provides a safe path-combination method and a free-file-name method. Its
responsibilities are to combine two path segments while verifying that the result does not escape the base directory,
protecting callers from string-level path-traversal attacks, and to find a name for a file in a folder that does not
replace anything that is there.

#### Data Model

`PathHelpers` holds no instance state. The class is `internal static` with no fields or
properties.

#### Key Methods

**SafePathCombine**: Safely combines a base path and a relative path.

- *Parameters*: `string basePath` — the base directory path; `string relativePath` — the
  relative path to append.
- *Returns*: `string` — the pre-resolved combined path (preserves the caller's
  relative/absolute style).
- *Preconditions*: Both `basePath` and `relativePath` are non-null.
- *Postconditions*: The returned path, when resolved to absolute form, is contained within
  `basePath`.

Validation steps: (1) reject null inputs via `ArgumentNullException.ThrowIfNull`; (2) call
`Path.Combine(basePath, relativePath)` to produce the candidate path; (3) resolve both
`basePath` and the candidate to absolute form with `Path.GetFullPath`; (4) compute
`Path.GetRelativePath(absoluteBase, absoluteCombined)` and reject if the result equals `".."`,
starts with `".."` followed by a directory separator character, or is itself rooted (absolute);
(5) return the pre-resolved `combinedPath` from step 2.

The containment check uses `Path.GetRelativePath` rather than string inspection to handle root
paths, platform case-sensitivity, and directory-separator normalization natively. The `".."`
check treats a double-dot segment as escaping only when it is the entire relative result or is
followed by a directory separator, avoiding false positives for valid names such as `"..data"`.

**UniqueFileName**: Finds a file name that is free in a folder.

- *Parameters*: `string folder` — the full path of the folder; `string fileName` — the wanted name, with its extension.
- *Returns*: `string` — `fileName` when no file and no folder in `folder` has it; otherwise the name with a number in
  parentheses before the extension, such as `photo (2).png`, using the first number from 2 to 9999 for which the name is
  free.
- *Preconditions*: `folder` is not null and `fileName` is not blank.
- *Postconditions*: No file or folder with the returned name existed in `folder` when the method looked. The method only
  looks; it creates nothing, so a caller that needs the name to stay free must create the file without overwriting.

Both a file and a folder with a name make it taken, because neither may be replaced by a new file. The name is split
with `Path.GetExtension`, so a name with a compound extension such as `chart.vl.json` is numbered before the last
extension (`chart.vl (2).json`), and a name with no extension gets the number at its end.

#### Error Handling

`SafePathCombine` throws `ArgumentNullException` for null inputs. It throws `ArgumentException`
(`"Invalid path component: {relativePath}"`) when the combined path escapes the base directory.
`NotSupportedException` and `PathTooLongException` may propagate from underlying BCL path
operations (`Path.Combine`, `Path.GetFullPath`). No logging or error accumulation is performed;
callers receive exceptions directly.

`UniqueFileName` throws `ArgumentNullException` for a null folder, `ArgumentException` for a blank name, and
`IOException` when thousands of numbered names are taken, which means something is wrong. A folder that does not exist
has no taken names, so the wanted name is returned.

#### Dependencies

- **.NET BCL** — `Path`, `ArgumentNullException`, and related types are the only dependencies.
  No other tool units or subsystems are used.

#### Callers

- **RollingFileLogger** — calls `PathHelpers.SafePathCombine` to resolve the active log file path and
  archive file paths inside its log folder. Other subsystems that build file paths from caller-supplied
  names are added here as they are introduced.
- **ProjectExplorer** — calls `PathHelpers.SafePathCombine` to resolve renamed and created file paths and
  `PathHelpers.UniqueFileName` to name the copies of files that are added or dropped. The `NewProjectDialog` also calls
  `SafePathCombine`.
