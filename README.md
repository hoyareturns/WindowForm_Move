# WindowForm_Move

WindowForm_Move adds a small floating button strip near the title bar of each normal running Windows program.

It does not inject code into other programs. Instead, it runs lightweight overlays that follow each window and control the target windows through the Windows API.

## Features

- Shows controls on maximized and half-screen windows
- Moves the clicked button's target window to the nearest monitor in that direction
- Sends diagonal moves directly to the top-row leftmost or rightmost monitor
- Places a window on the left, right, top, or bottom half of its current monitor
- Saves named multi-window layouts and restores or deletes them from the button strip
- Optionally launches missing programs before restoring a saved layout
- Restores only matching windows; unmatched or login-required windows are left untouched
- Provides Korean hover tooltips for every control
- Collapses the layout profile controls when they are not needed
- If there is no monitor in that direction, nudges the window inside the virtual desktop
- Keeps maximized windows maximized after moving
- Provides a click-through crosshair guide that follows the mouse across the current monitor
- The compact `ALL` button or tray menu applies monitor moves to all movable windows
- Runs from the system tray
- Tray menu supports show/hide, all-window mode, crosshair guide, and exit

## Run

```powershell
dotnet run
```

Published executable:

```powershell
.\bin\Release\net6.0-windows\win-x64\publish\WindowForm_Move.exe
```

## Usage

1. Start `WindowForm_Move`.
2. Small buttons appear near the top-right of normal running windows.
3. Use a window's own overlay buttons to move that window.
4. Use the crosshair button to toggle the mouse guide.
5. Expand the green `< >` set, type a layout name, then use `S`, `L`, or `D`.
6. Turn on `RUN` before loading when missing programs should be launched automatically.
7. Turn on `ALL` from the button strip or `Move all windows` from the tray menu when needed.
8. Use the tray icon menu to hide the buttons or exit the app.
