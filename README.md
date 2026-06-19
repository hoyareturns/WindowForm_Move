# WindowForm_Move

WindowForm_Move adds a small floating button strip near the title bar of each normal running Windows program.

It does not inject code into other programs. Instead, it runs lightweight overlays that follow each window and control the target windows through the Windows API.

## Features

- Shows controls on maximized and half-screen windows
- Moves the clicked button's target window to the nearest monitor in that direction
- Sends diagonal moves directly to the top-row leftmost or rightmost monitor
- Places a window on the left, right, top, or bottom half of its current monitor
- Saves named multi-window layouts and restores or deletes them from the button strip
- Restores only matching windows; unmatched or login-required windows are left untouched
- Provides Korean hover tooltips for every control
- Collapses the layout profile controls when they are not needed
- If there is no monitor in that direction, nudges the window inside the virtual desktop
- Keeps maximized windows maximized after moving
- Provides a click-through crosshair guide that follows the mouse across the current monitor
- Adds numbered screen markers and drag-to-place arrow memos without modifying the underlying program
- Supports editing existing memo boxes, annotation undo, erasing, and clearing all annotations
- Captures a user-dragged screen region with the visible annotations as a PNG file
- Saves captures automatically using the configured folder and `{date}`, `{time}`, or `{datetime}` filename pattern
- Opens the capture folder after saving unless that folder is already open in Explorer
- Stores marker color/size and arrow color/thickness settings for the next run
- Starts marker numbering at 1, automatically advances the toolbar number, and allows manual toolbar input
- Collapses the annotation controls as a separate green `< >` tool set
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
5. Expand the first green `< >` set, choose the marker color/number, then use `①` for numbered markers, `↗` to drag an arrow and enter a memo, or `E` to erase one annotation. Click an existing memo box in arrow mode to edit it.
6. Use `↶` to remove the latest annotation, `AC` to clear all, and the square capture button to drag and save a selected region. Press `Esc` to cancel capture.
7. Open the annotation settings button to choose marker size, arrow appearance, capture folder, and filename pattern.
8. Expand the second green `< >` set, type a layout name, then use `S`, `L`, or `D`.
9. Turn on `ALL` from the button strip or `Move all windows` from the tray menu when needed.
10. Use the tray icon menu to hide the buttons or exit the app.
