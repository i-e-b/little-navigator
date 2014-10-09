little-navigator
================

A tiny keyboard-friendly directory navigator for Windows

Start `lnav.exe` in the directory you want to navigate from.
Type to edit the find pattern

Keybindings
-----------
* `return` edit the selected file (currently hard coded to gVim)
* `shift-return` to edit a new file at the selected path with a name equal to the find pattern
* `tab` find next file that contains the find pattern
* `shift-tab` collapse all branches

To do
-----
* Keep expanded tree structure on update
* Don't update if a hidden file is created or changed
* copy relative path (between two nodes -- using right-click?)
* Paste goes into search path
* Double-click triggers open
* grep-here command? `ctrl-G`
* call from cmd line to 'show here' in open instance
