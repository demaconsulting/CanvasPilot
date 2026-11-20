## Utilities

![Utilities Structure](UtilitiesSubsystemView.svg)

### Overview

The `Utilities` subsystem provides shared utility functions for the CanvasPilot. It
supplies reusable, independently testable helpers consumed by other subsystems. Its primary
responsibility is safe file-path manipulation, protecting callers from path-traversal
vulnerabilities when constructing paths from caller-supplied inputs. The `Utilities` subsystem
contains one unit: `PathHelpers`.

### Interfaces

**PathHelpers.SafePathCombine**: Combines a base path and a relative path, rejecting any result
that escapes the base directory.

- *Type*: In-process .NET static method.
- *Role*: Provider.
- *Contract*: Accepts `string basePath` and `string relativePath`. Returns the combined path
  produced by `Path.Combine(basePath, relativePath)` after verifying that the resolved result
  remains within `basePath`. Preserves the caller's relative/absolute style in the return value.
- *Constraints*: Throws `ArgumentNullException` for null inputs; throws `ArgumentException`
  when the combined path escapes the base directory; may propagate `NotSupportedException` or
  `PathTooLongException` from underlying BCL path operations.

### Design

The `Utilities` subsystem contains only the `PathHelpers` unit. It has no dependencies on other
tool units or subsystems; it uses only .NET BCL types (`Path`, `ArgumentNullException`).

`PathHelpers.SafePathCombine` is a pure utility method: it performs no file-system I/O, holds
no state, and throws immediately on invalid input. Consumers use it to construct file paths from
caller-supplied names: currently the `Logging` subsystem (`RollingFileLogger`), which resolves its
active log file and archive file paths inside its log folder.
