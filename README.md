# WindowForm_Move

WindowForm_Move adds a small floating button strip near the title bar of each normal running Windows program.

It does not inject code into other programs. Instead, it runs lightweight overlays that follow each window and control the target windows through the Windows API.

## Features

- Shows small buttons on each movable window: left, right, up, down
- Moves the clicked button's target window to the nearest monitor in that direction
- If there is no monitor in that direction, nudges the window inside the virtual desktop
- Keeps maximized windows maximized after moving
- `ALL` mode applies the direction move to all movable windows
- Runs from the system tray
- Tray menu supports show/hide, all-window mode, and exit

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
4. Turn on `ALL` to move all movable windows together.
5. Use the tray icon menu to hide the buttons or exit the app.
