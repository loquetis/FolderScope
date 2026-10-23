# FolderScope

FolderScope is a Windows desktop app for seeing which folders are using disk space. Pick a drive or a folder and it scans in the background, then shows a sortable list with size, percent of the parent, file count, and a share bar. Open a row to drill in, or switch to two panes and copy or move files between them.

More apps: [coreydblackwell.com/apps](https://www.coreydblackwell.com/apps/).

## Requirements

- **Run:** Windows 10 or Windows 11, 64-bit. The published `FolderScope.exe` is self-contained. It does not need an installer or a separate .NET runtime.
- **Build:** the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). On Linux or macOS the same SDK cross-compiles the Windows exe. `global.json` asks for the 8.0 SDK and rolls forward to the latest 8.0 feature band.

## Build and run

On Windows, from the repo root:

```powershell
.\build\publish-win-x64.ps1
.\artifacts\win-x64\FolderScope.exe
```

On Linux or macOS:

```bash
./build/publish-win-x64.sh
```

That script runs the tests, then writes `artifacts/win-x64/FolderScope.exe`. A machine that already has the .NET 8 desktop runtime can skip publish and run from source:

```powershell
dotnet run --project src\FolderScope.App -c Debug
```

Tests only, without producing the exe:

```bash
dotnet test tests/FolderScope.Core.Tests -c Release
```

## Run

`FolderScope.exe` is a self-contained app for **Windows 10 and Windows 11, 64-bit**. It does not need an installer or a separate .NET runtime.

```text
FolderScope.exe
FolderScope.exe D:\Photos
FolderScope.exe --scan C:\Users
FolderScope.exe --self-test
```

- No arguments opens the window.
- A folder or drive path starts a scan immediately.
- `--scan <path>` prints the total and the largest entries in the current folder. Useful from a terminal.
- `--self-test` builds a temporary tree, scans it, and returns 0 when the totals are right. Optional second argument: a file to write `ok` or `fail`.

On first launch the exe unpacks native runtime files into a temporary folder. That is normal for a single-file .NET app. After that, startup is just the app.

## What the window does

- **This PC** lists drives. Fixed and removable drives show free space. Network drives are listed without probing them, because a disconnected share can stall.
- **Browse folder** uses the system folder picker.
- The list is virtualized, so a folder with a very large number of files stays scrollable.
- Click a column to sort. Size is the default, largest first. Names sort in natural order (`file2` before `file10`).
- The **Share** column and the bar above the list show how much of the current folder each entry occupies.
- Double-click a folder, or press Enter, to open it. Backspace or **Up** goes back. The path above the list is clickable.
- Double-click a file, or **Show in Explorer**, selects it in File Explorer.
- **Refresh** or Ctrl+R rescans the folder you are viewing. **Stop** or Esc cancels. A cancelled scan keeps what it has already counted and says the totals are incomplete.
- **Dual pane** is how the window opens the first time. Each pane picks its own drive or folder, scans, sorts, and filters on its own, including the date filter. Click a pane to make it active. The active pane has a green edge. **Single pane** on the header switches back, and FolderScope remembers that choice.
- The list includes folders and files in the current folder. Select several rows with Ctrl or Shift.
- The list includes a **Modified** column for folders and files. That is the folder or file's own last-modified time, captured during the scan.
- The filter box (Ctrl+F) hides names in the current folder only. Esc clears it when the box is focused. In the Files tab it also matches the folder path.
- Ctrl+Shift+C copies the selected path, or the folder you are viewing.

## Finding old or new files

The date bar sits above the path. It does not read the disk again. Timestamps are stored while scanning, so changing the filter only recounts what is already in memory. The line under the filters says that filtering by age does not rescan; hover it for the full note. A path or a totals line that does not fit ends in an ellipsis, and the tooltip has the rest.

- **Date** chooses last modified, created, or last accessed. Last accessed is often unreliable on Windows: many systems do not update it.
- **Age** has presets for older than 30 days, 90 days, 6 months, 1 year, 2 years, and 5 years. **Custom date or range** can be older than a calendar day, newer than a calendar day (that day included), or a range of calendar days. Custom dates are local days. Presets are measured from the moment you apply them; months and years step by calendar month and year.
- A file with no timestamp for the chosen field is not treated as old.
- **Match size** and **Match files** appear beside the full totals. They add up only the files in range under each folder. The Size column stays the full total, including files outside the range. Junctions and symbolic links are still not followed.
- **Files** lists the matching files under the folder you are viewing: name, folder, size, and the chosen date. Columns sort. With no age selected, it lists only the files directly in the current folder.
- **Open folder** (and the menu item **Open folder in Explorer**) selects the file in File Explorer. **Copy path** copies it. **Export CSV** writes the files currently listed, as UTF-8, with path, name, size, and the modified, created, and accessed times.
- Inaccessible folders stay in the list and are marked. **Run as admin** restarts the app elevated when some folders could not be read.

While a scan is running you can still sort, filter, and open folders that have already been found. The totals keep moving until that part of the tree is finished.

## Copying, moving, and deleting

The strip between the panes is where Copy and Move live. Each button is an icon: two pages for copy, a page with an arrow for move, and a direction arrow showing which way the files go. A button is enabled only when that side has a selection and the other side is showing a folder. Hover for “Copy selected to right pane (F5)” and the matching move and leftward tips.

Cut, Paste, Delete, Rename, and New folder stay on the top toolbar, and they wrap onto another line when the window is narrow. The right-click menu still has Copy, Cut, and Paste. Drag a selection onto the other pane, or onto a folder row. Ctrl while dragging copies. Shift moves. With neither key, a drop on the same drive moves and a drop on another drive copies.

- **Ctrl+C** and **Ctrl+X** put the selection on the clipboard. **Ctrl+V** writes it into the open folder. Explorer can paste a FolderScope copy, and FolderScope can paste a File Explorer copy (CF_HDROP). A cut from Explorer is pasted as a move.
- **F5** copies the selection to the other pane when dual pane is open and that pane has a folder. Otherwise F5 refreshes.
- **F6** moves the selection to the other pane. Move asks you to confirm. Delete asks too. Copy does not.
- **Delete** sends items to the Recycle Bin. Shift+Delete deletes permanently. If the Recycle Bin cannot take an item, FolderScope offers a permanent delete.
- **F2** renames the selected item. **New folder** creates a folder in the pane you are viewing.
- Work runs off the UI thread. A dialog shows files, bytes, the current item, and Cancel.
- When a name already exists you can Overwrite, Skip, or Rename (`report (2).txt`), and apply that choice to the rest.
- A move on the same drive is a rename. A move to another drive copies, checks that the copy is the same length, and only then deletes the original. Timestamps are kept.
- Shortcuts, junctions, and symbolic links are never followed. You choose whether to copy the link itself or skip it. A junction is recreated as a symbolic link, not as another junction.
- A locked file, a read-only file, a path you cannot access, or a path the system rejects is reported on its own. You can continue with the rest or stop.
- After a successful change, both panes update their sizes from the tree already in memory. A pane that is still scanning refreshes the folder you are viewing instead of patching a live scan.

Cancelled copies delete a partial file. A folder that was only partly copied can be left behind so the items that did finish are not thrown away.

## How the scan treats the disk

- **Hidden and system files are included.** A disk-usage total that skips them is wrong.
- **Sizes are logical sizes.** 1 KB = 1,024 bytes. This is the file length, not compressed size-on-disk, not cluster slack, and not alternate data streams.
- **Directory junctions, mount points, and symbolic links are listed and not followed.** Their size is not added again. A file shortcut contributes 0 bytes. A cloud placeholder (a reparse point with no link target) keeps its logical size.
- **Long paths** use the `\\?\` prefix, and the exe is marked long-path aware, so paths past 260 characters are readable on Windows 10 and 11.
- **Access denied, missing folders, and I/O errors** are recorded on that folder. The rest of the scan continues.
- Cancel is checked between batches of files (every 64) and between folders. One very large directory may finish its current batch before the scan stops.
- Several folders are read at once (up to 8 workers) so a large drive does not stay single-threaded.

## Build

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

### Windows

```powershell
.\build\publish-win-x64.ps1
```

The exe is written to `artifacts\win-x64\FolderScope.exe`.

To build a normal (not self-contained) debug build and run it on a machine that already has the .NET 8 desktop runtime:

```powershell
dotnet run --project src\FolderScope.App -c Debug
```

### Linux or macOS

The same script cross-compiles the Windows exe. The Windows desktop targeting pack and the win-x64 runtime are downloaded on first publish.

```bash
./build/publish-win-x64.sh
```

Output: `artifacts/win-x64/FolderScope.exe`.

### Tests

```bash
dotnet test tests/FolderScope.Core.Tests -c Release
```

The tests cover nested totals, batching, parallel scans, junctions and symlinks (including a link cycle), file links, access denied, cancellation, hidden names, Unicode names, paths longer than 260 characters, and date filters (presets, custom dates, created and accessed fields, match totals, the file list, and CSV export). Transfer tests cover conflict names, volume and path checks, same-drive rename versus cross-drive copy-then-delete, links that are not followed, cancellation, and size updates after a paste. The publish scripts run these tests before they produce the exe.

## CI

- GitHub Actions: `.github/workflows/build.yml` runs the publish script on Ubuntu and uploads `FolderScope.exe`.
- GitLab CI: `.gitlab-ci.yml` does the same with the `mcr.microsoft.com/dotnet/sdk:8.0` image.

Both call `build/publish-win-x64.sh`, which fails if the output is anything other than the single exe.

## Project layout

- `src/FolderScope.Core` — scanner, path handling, sorting, and size formatting. This is plain `net8.0` and is what the tests run.
- `src/FolderScope.App` — WinForms UI (`net8.0-windows`).
- `build/publish-win-x64.sh` and `build/publish-win-x64.ps1` — test and publish.
- `build/make-icon.py` — regenerates `src/FolderScope.App/app.ico`.

## Limitations

- Windows 10/11 x64 only. There is no 32-bit and no ARM64 build.
- Logical size can be larger than "size on disk" for compressed or sparse files, and smaller when cluster slack matters.
- Hard links are counted once for every directory entry that points at them. Junctions and symlinks are not.
- Alternate NTFS data streams are not counted.
- The UI is a drill-down list, not a treemap and not a fully expanded tree of every folder. Expanding millions of tree nodes is what makes this kind of tool stall; the list only materializes the folder you are looking at.
- Numbers use a fixed format (`1,234.5 MB`) so the self-contained exe does not have to carry locale data.
- Running elevated is optional. Without it, protected folders such as System Volume Information are marked inaccessible.
- `--self-test` still checks ordinary totals when Windows refuses to create symbolic links (that needs Developer Mode or an elevated process). Link behavior is covered by the unit tests.
- Last-access time is only as good as the filesystem. FolderScope reports the timestamp it read; it cannot tell whether Windows has been updating it.
- The date filter matches timestamps captured during the current scan. It does not notice a file that changes until you refresh.
- Copy and move use FolderScope's own file engine so the conflict and progress dialogs can be tested. They do not call the Explorer `IFileOperation` dialog. Delete uses the Windows shell so items go to the Recycle Bin.
- A junction or mount point is copied as a symbolic link. Creating that link can fail without Developer Mode or an elevated process. The contents behind the link are not copied.
- Hard links are copied as a new file. Alternate NTFS data streams are not copied.
- Drag-and-drop from Explorer into a pane uses the same clipboard file list. Drop effects from other apps are best-effort.
- If you cancel a folder copy, files already written stay in the destination folder.
- Very long paths go through the `\\?\` prefix for the managed copy. The Recycle Bin API may still reject a path past the classic limit; FolderScope then offers a permanent delete.
