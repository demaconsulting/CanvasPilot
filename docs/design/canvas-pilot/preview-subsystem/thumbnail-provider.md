### ThumbnailProvider

![Preview Structure](PreviewSubsystemView.svg)

#### Purpose

`ThumbnailProvider` makes the small pictures that the project explorer shows at the right of the rows of the files it
can draw, and the larger pictures that the welcome screen shows for a recent project. It implements `IThumbnailSource`,
which the `Project` subsystem declares and consumes (the file `IThumbnailSource.cs` lives in that subsystem and belongs
to this unit's source files), by having the render service draw the file at the size of the thumbnail on a white
background, off the user interface thread, by keeping the pixels of the latest thumbnails in a small cache, and by
giving every caller a bitmap of its own.

#### Data Model

- `ThumbnailWidth` (56) and `ThumbnailHeight` (36): the size in pixels of a thumbnail of the explorer, twice the 28 by
  18 units that a row shows, so that it is sharp on a dense display.
- `MaximumConcurrentRenders` (2): how many files are drawn at once.
- **Cache**: a dictionary from a key to a node of a linked list of `CacheEntry` records (key and `RenderedImage`), the
  most recently used first, guarded by one lock. Its capacity is the constructor's `capacity` (256 by default, at
  least 1) and counts thumbnails, not bytes. The key is the full path of the file, the last write time in ticks (UTC),
  the length of the file, and the width and height, joined with `|`, so a file that changed (its time or length differ)
  and the same file at another size are different entries. The entry holds only the straight-alpha pixels of the drawn
  picture, copied into an own array, labeled with the format `thumbnail`; the element positions, notes, and fingerprints
  of a full render are not kept.
- **Gate**: a `SemaphoreSlim` with `MaximumConcurrentRenders` places that makes the third and later requests wait their
  turn.
- `CachedCount` (how many thumbnails the cache holds) and `RenderCount` (how many times the cache did not have the
  thumbnail and the file was drawn) are read-only counters for tests.
- **Background**: `#FFFFFF`, the color that a thumbnail is flattened over.

#### Key Methods

**CreateAsync(filePath, projectFolder, cancellationToken)**: the thumbnail of the explorer; it calls the sized overload
with 56 by 36. **CreateAsync(filePath, projectFolder, width, height, cancellationToken)**: checks the arguments, gives
`null` for a file that does not exist, builds the key from the file's information, and looks it up in the cache (a hit
moves the entry to the front). On a miss it waits for the gate, looks again (another caller may have drawn it while this
one waited), and otherwise draws the file with
`IImageRenderService.RenderAsync(filePath, projectFolder, RenderSettings(width, height, BackgroundHex: "#FFFFFF"))`,
which fits the picture inside the box with its proportions kept over the white background, so the picture is exactly the
given size. A render that gives no picture gives `null` and is not remembered. Otherwise the pixels are copied into a
new `RenderedImage`, put in front of the cache (an old entry with the same key is replaced), the oldest entries are
removed until the count is within the capacity, and the gate is released. Whatever the path, the method then creates a
new `WriteableBitmap` for the caller with `RenderedImageBitmap.Create`, so each caller owns its bitmap and evicting an
entry never takes a bitmap from under a row that shows it. The render runs on the render service's own background
thread, so the work is off the user interface thread; the task completes on whatever thread finishes it and the caller
marshals the bitmap to its owner thread.

**Clear**: forgets every cached thumbnail; the bitmaps already given out are the callers'. **Dispose**: clears the cache
and disposes the gate; it is safe to call twice.

#### Error Handling

The constructor throws `ArgumentNullException` for a missing renderer and `ArgumentOutOfRangeException` for a capacity
under one. `CreateAsync` throws `ArgumentException` for a blank path or folder and `ArgumentOutOfRangeException` for a
width or height under one. A missing file, a file that the render service cannot draw (it reports failures as results,
not exceptions), and a render that was cancelled by the caller's token give `null`, or for a cancelled wait the
`OperationCanceledException` of the token; the caller (the explorer or the welcome screen) treats a failure as a row
without a thumbnail. A failed render is not cached, so the next request draws the file again.

#### Dependencies

- **ImageRenderService** (through `IImageRenderService`, with `RenderSettings`) — draws the files.
- **PreviewPane** (`RenderedImageBitmap`) — turns the cached pixels into a display bitmap.
- **ProjectExplorer** — declares `IThumbnailSource` and asks for the thumbnails.
- **Avalonia** — the `Bitmap` that is returned.

#### Callers

- `ProjectExplorerViewModel` (the `Project` subsystem) — asks for the thumbnail of each file that can be drawn and whose
  row is visible, and again when the file's time or size changes; `ApplicationComposition` creates one provider over the
  render service, gives it to the explorer and the shell, and disposes it.
- `RecentThumbnailLoader` (the `AppShell` subsystem) — asks for the larger picture of the first picture of each recent
  project that the welcome screen shows, through the sized overload.
