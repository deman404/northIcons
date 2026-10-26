northIcons
A local WPF workspace for your SVG icons.

Start northIcons.exe, or use the northIcons shortcut on your Desktop.
Requires the .NET 10 Desktop Runtime, already installed on this PC.

Your library
The app starts with the bundled Icons.zip archive beside the executable. Use Change folder for another library.
Search matches words in icon filenames. Category chips are filename-based
shortcuts, not official Flaticon categories. Refresh icons picks up new files.
Results are paged, with 72 icons per page, to keep large libraries responsive.

Using icons
Select an icon to see a larger preview.
Copy SVG copies the original SVG markup as text.
Copy file lets you paste the SVG file into Windows Explorer or a project folder.
Export SVG saves a copy wherever you choose.
Choose project folder once, then use Add to project for quick file copying.
Existing project files are replaced only after confirmation.
Original library files are never modified. With an icon color selected, copies and exports use that color.
Favorites and recently used icons are saved locally.

Keyboard
Ctrl+F focuses search. Escape clears the focused search.
Ctrl+C copies the selected SVG when the search box is not focused.

Source and license
View source & license opens the icon's Flaticon page using sources.tsv when
available. Original authors' license and attribution requirements still apply.
All searching, previews and file operations work offline.

Preferences: %LOCALAPPDATA%\northIcons\settings.json
Source code is included beside the executable. Rebuild: dotnet build -c Release

Preview support
The built-in renderer handles SVG paths, basic shapes, groups, transforms and
simple class styles used by this icon library. Complex SVG features such as
gradients, masks and embedded images are not supported. When a preview cannot
be generated, the original SVG can still be copied or exported.

UI and PNG update
Navigation and action controls use embedded SVGs from your icons lib folder.
Their original filenames/source pages are recorded in Assets\sources.tsv.
PNG export: select an icon, choose 64/128/256/512/1024/2048 px, then click PNG.
Images are exported with a transparent background and preserved proportions.
PNG export requires an SVG that the preview renderer supports.
Button hover preserves text contrast. Pressed, disabled and keyboard-focus
states are styled separately; the PNG size menu matches the app styling.


Icon colors
Select an icon, then choose a color swatch or enter #RGB / #RRGGBB in ICON COLOR.
The chosen color updates the large preview, Copy SVG, Copy file, SVG export,
PNG export and Add to project. Grid thumbnails retain the library originals.
Reset restores original colors and byte-for-byte original SVG copies.
Invalid hex input keeps the last valid color; the field turns red.
Transparent fills (none) and opacity are preserved. White/light colors use a
 darker preview background. Color selection lasts for the current app session.
Colored clipboard files are stored in %LOCALAPPDATA%\northIcons\Clipboard.


Bundled library
Icons.zip is included in the source project, build output and publish output.
Keep Icons.zip beside northIcons.exe when distributing the application.
A missing saved library folder falls back to the included library.
See README.md and THIRD_PARTY_NOTICES.md for third-party redistribution terms.

The library is unpacked automatically once into %LOCALAPPDATA%\northIcons\Library. Subsequent launches reuse the extracted files.

