### ThumbnailProvider

#### Verification Approach

`ThumbnailProvider` is verified by headless UI tests in `ThumbnailViewTests.cs` (the `UiTests` project), because a
thumbnail is an Avalonia bitmap that needs the headless platform. The tests use the real `ImageRenderService` (and so
the real CanvasNet viewer) over a temporary project folder with real SVG files, so that what is checked is the picture
that the user would get. The provider's own counters (`RenderCount`, `CachedCount`) tell whether a call drew the file or
found it in the cache. Nothing is mocked. The rows of the explorer that show the thumbnails are verified with the
`ProjectExplorer` unit, whose view test is in the same file.

#### Test Environment

The standard test runner with the headless Avalonia platform and the Skia back end. A temporary folder per test holds
the pictures and is removed afterwards. No network or display is needed.

The limit of two renders at once, the sized overload with the size in the cache key, the white background, and the
thread on which the render runs are not asserted by a test; they are reviewed in the design and the code.

#### Acceptance Criteria

- All tests of the unit pass with zero failures.
- A thumbnail of a 40 by 20 SVG is a bitmap of exactly 56 by 36 pixels, and two requests for the same file give two
  different bitmaps.
- A file is drawn once while it does not change and again after its content or size changes.
- The cache holds at most the number of thumbnails it was given, the thumbnail used least recently is forgotten first,
  and `Clear` empties the cache.
- A file that does not exist and a file that cannot be drawn give no bitmap and no exception; a missing renderer, a
  capacity below one, and a blank path are refused.

#### Test Scenarios

**ThumbnailProvider_CreateAsync_DrawsOnceUntilTheFileChanges**: A red 40 by 20 SVG is asked for twice. Both give a
bitmap of 56 by 36 pixels, they are different objects, and the file was drawn once. After the file is rewritten with
different content and size the next request gives a bitmap and the file has been drawn twice. This scenario is tested by
`ThumbnailProvider_CreateAsync_DrawsOnceUntilTheFileChanges`.

**ThumbnailProvider_CreateAsync_FullCacheOrBadFile_EvictsLeastRecentlyUsedAndGivesNoBitmap**: A provider that holds two
thumbnails is asked for `a`, `b`, `a`, and `c`, which pushes `b` out: two thumbnails are held and three files were
drawn. Asking for `a` again does not draw, and asking for `b` draws it again (four renders). A broken SVG and a missing
file give `null`, `Clear` empties the cache, a missing renderer and a capacity of zero are refused, and a blank path is
refused. This scenario is tested by
`ThumbnailProvider_CreateAsync_FullCacheOrBadFile_EvictsLeastRecentlyUsedAndGivesNoBitmap`.
