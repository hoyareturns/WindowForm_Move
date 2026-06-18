# WindowForm_Move

WindowForm_Move adds a small floating button strip near the title bar of each normal running Windows program.

It does not inject code into other programs. Instead, it runs lightweight overlays that follow each window and control the target windows through the Windows API.

## Features

- Shows controls on maximized and half-screen windows
- Moves the clicked button's target window to the nearest monitor in that direction
- Places a window on the left, right, top, or bottom half of its current monitor
- If there is no monitor in that direction, nudges the window inside the virtual desktop
- Keeps maximized windows maximized after moving
- Provides a click-through crosshair guide that follows the mouse across the current monitor
- Provides an always-visible search box that runs `Ctrl+F` in the target window
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
5. Type in the `Find` box and press Enter to search the target Excel, document, or browser window.
6. Turn on `ALL` from the button strip or `Move all windows` from the tray menu when needed.
7. Use the tray icon menu to hide the buttons or exit the app.
