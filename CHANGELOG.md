# Changelog

## 1.2.2

- The folder name above the color bar is fully visible. The age note is one line (“Filtering by age does not rescan.”), and the longer explanation is a tooltip, so it no longer covers that name.
- Date and Age stay attached to their dropdowns. The closed Age box fits the current choice, so Age and All dates stay on the filter row.
- The status line ellipsizes a path or a totals line that does not fit, and the tooltip shows the full text. The progress bar is only there while a scan is running.

## 1.2.1

- Layout no longer clips the Browse folder button, toolbars, date filters, path, status line, or dialogs. Bars grow to fit their contents and wrap when a pane is narrow. Text on buttons is not truncated.
- The window is per-monitor DPI aware and scales at 100%, 125%, 150%, and 175%.
- Copy and Move sit in a strip between the panes as icon buttons. Copy is two overlapping pages; Move is a page with a folded corner. Each icon includes a direction arrow. Hover text is “Copy selected to right pane (F5)” or “Move selected to left pane (F6)” (and the other direction). A button is enabled only when that side has a selection and the other side has a folder. Keyboard shortcuts, the right-click menu, and drag-and-drop are unchanged. Those commands are no longer on the top toolbar.
- Dual pane is the default the first time you open FolderScope. The header toggle still switches to a single pane, and that choice is remembered.
- Hidden `--demo` opens two sample folders so the window can be shown without scanning a real disk.

## 1.2.0

- Dual pane. Each pane has its own folder, scan, sort, and date filter, and lists the folders and files in the current folder.
- Copy, cut, paste, delete, rename, and new folder from the toolbar, the right-click menu, and the keyboard. F5 copies to the other pane (or refreshes when there is nowhere to copy). F6 moves to the other pane. Ctrl+R refreshes.
- Drag between panes. Ctrl copies, Shift moves, and a plain drop moves on the same drive or copies across drives.
- Multi-select. Move and delete ask first.
- File work runs off the UI thread with progress, cancel, and Overwrite / Skip / Rename, including apply-to-all.
- A same-drive move is a rename. A cross-drive move copies, checks the length, then deletes the original. Timestamps are kept. Junctions and symbolic links are not followed.
- Delete uses the Windows Recycle Bin. Shift+Delete is permanent.
- After a change, both panes update sizes without a full rescan. Paste also works with an Explorer file clipboard.

## 1.1.0

- Date filter with presets for older than 30 days, 90 days, 6 months, 1 year, 2 years, and 5 years, plus a custom date or range.
- Choose last modified, created, or last accessed. Last accessed is marked as unreliable.
- Match size and match file count sit beside the full folder totals. Changing the filter does not rescan.
- Files view lists matching files with path, size, and date. Open the folder in Explorer, copy the path, or export CSV.
- Modified column on folders and files in the main list.
- Timestamps are captured during the scan.

## 1.0.0

- First release. Self-contained Windows x64 folder size viewer with a drill-down list, background scan, cancel, and refresh.
