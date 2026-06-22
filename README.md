# Smart_Window

Smart_Window adds a small floating button strip near the title bar of each normal running Windows program.

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
- Opens a frozen full-desktop presentation mode for plain dots, numbered markers, and drag-to-place arrow memos
- Keeps annotation clicks completely separate from the underlying Excel, browser, and document windows
- Supports editing existing memo boxes, annotation undo, erasing, and clearing all annotations
- Captures a user-dragged screen region with the visible annotations as a PNG file
- Saves captures automatically using the configured folder and `{date}`, `{time}`, or `{datetime}` filename pattern
- Opens the capture folder after saving unless that folder is already open in Explorer
- Stores marker color/size and arrow color/thickness settings for the next run
- Starts marker numbering at 1, automatically advances the toolbar number, and allows manual toolbar input
- Keeps the initiating Smart_Window annotation button set over the frozen screen instead of opening a second toolbar
- The compact `ALL` button or tray menu applies monitor moves to all movable windows
- Runs from the system tray
- Tray menu supports show/hide, all-window mode, crosshair guide, and exit

## Run

```powershell
dotnet run
```

Published executable:

```powershell
.\bin\Release\net6.0-windows\win-x64\publish\Smart_Window.exe
```

## Usage

1. Start `Smart_Window`.
2. Small buttons appear near the top-right of normal running windows.
3. Use a window's own overlay buttons to move that window.
4. Use the crosshair button to toggle the mouse guide.
5. Expand the first green `< >` set, choose the marker color/number, then select `●`, `①`, or `↗` to enter the frozen presentation mode. `●` adds a point without changing the marker number.
6. Continue using the same Smart_Window button set for markers, arrow memos, erasing, undo, clear-all, capture, and settings. A notice appears to its left; press `Esc` to exit.
7. Use the square capture button to save a selected region; press `Esc` to cancel capture.
8. Open settings to choose marker size, arrow appearance, capture folder, and filename pattern.
9. Expand the second green `< >` set, type a layout name, then use `S`, `L`, or `D`.
10. Turn on `ALL` from the button strip or `Move all windows` from the tray menu when needed.
11. Use the tray icon menu to hide the buttons or exit the app.
