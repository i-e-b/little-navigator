little-navigator
================

A tiny keyboard-friendly directory navigator for Windows

Start `lnav.exe` in the directory you want to navigate from.
Type to edit the search string

Keybindings
-----------
* `return`
   * if a file is selected, edit the selected file (currently hard coded to gVim).
     If search string ends with `:x:y`, will open file at row `x`, col `y`.
     If search string ends with `:x`, will open file at row `x`
   * if a directory is selected, open the directory in an explorer window
* `shift-return` to edit a new file at the selected path with a name equal to the search string
* `tab` find next file that contains the search string
* `shift-tab` collapse all branches
* `ctrl-V` paste into search string and find next match
* `ctrl-G` find next file whose contents match a regex from the search string.
   * `shift-ctrl-G` open file and go to first match position

Mouse actions
-------------
* double-click to edit selected file
* right-click to copy a relative path to the clipboard. Path is *from* selected *to* right-clicked nodes.

Other
-----
If you pass a command line argument of a file path to `lnav`, it will send this to all other running instances.
Those instances will use it to search for the file.
(I map this to `shift-K` in gvim like this: `noremap K :silent! !lnav %<CR>`)

To do
-----
* cleanup Form1 code.
* option to expand directories that have been truncated due to depth limit.
