# WindowForm_Move

WindowForm_Move adds a small floating button strip near the title bar of the currently active Windows program.

It does not inject code into other programs. Instead, it runs as a lightweight overlay that follows the active window and controls that window through the Windows API.

## Features

- Shows small buttons on the active window: left, right, up, down
- Moves the active window to the nearest monitor in that direction
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
2. Click any normal program window.
3. Use the small overlay buttons near the top-right of that window.
4. Turn on `ALL` to move all movable windows together.
5. Use the tray icon menu to hide the buttons or exit the app.
