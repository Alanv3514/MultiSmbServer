[English](README.md) · [Español](README.es.md)

# Multi SMB Server

Self-contained **SMBv1 (NT LM 0.12)** file server with a **.NET 8 / WPF** GUI, built to serve games and resources over the network to retro consoles: **PlayStation 2** (Open PS2 Loader), **Wii** and **GameCube** (USB Loader GX, WiiFlow, Nintendont).

## Requirements

- Windows (disk access uses the native NT APIs).
- .NET 8 Runtime (or use the self-contained build, see [Building the .exe](#building-the-exe)).
- Consoles (PS2, Wii, GameCube) on the same network as the PC.

## Quick start

1. Run the application.
2. In **Shares**, add one or more shared folders:
   - **PS2**: the root with the OPL folders (`DVD/`, `CD/`, `ART/`, `CFG/`, `VMC/`).
   - **Wii/GameCube** (optional): the root with `wbfs/` (Wii) and/or `games/` (GameCube).
3. Set the **Name** of each share (e.g. `PS2SMB`, `WII`), the **Port**, **User** and **Password**.
4. Press **Start server** and configure OPL as described below.

> The **share name** and **port** must be exactly the same as the ones you configure in OPL (or in the Wii loader).

The configuration is **saved automatically** (when starting the server, on close, or with the **Save configuration** button), so it loads on the next launch.

### Multiple shares

You can expose several folders at once (PS2, Wii, GameCube...) with the **+ Add share** button. Each share has its own name and folder. All consoles connect to the same server and port; the share name is what distinguishes them.

## System tray and silent start

- **Minimizing** the window hides it in the system tray (it keeps serving in the background).
- **Closing (X)** shows a dialog: exit the app, minimize to tray, or cancel.
- Double-click the tray icon (or the **Open** menu) restores it; **Exit** really closes it.
- Command line arguments:
  - `/START` — starts the server automatically on launch (uses the saved configuration).
  - `/SILENT` (or `/HIDE`, `/MINIMIZED`) — starts hidden in the tray.

Examples:

```powershell
# Open and start the server automatically
MultiSmbServer.exe /START

# Start in the background (tray) with the server active
MultiSmbServer.exe /START /SILENT
```

## Expected folder structure

The PS2 share folder must contain the standard OPL subfolders:

```
PS2 folder/
├── DVD/   -> games in .iso format (DVD games)
├── CD/    -> games in .iso format (CD games)
├── ART/   -> cover art (downloaded by OPL)
├── CFG/   -> per-game configurations (written by OPL)
└── VMC/   -> virtual Memory Cards (written by OPL)
```

For Wii/GameCube (USB Loader GX, WiiFlow, Nintendont), the share folder typically contains:

```
Wii folder/
├── wbfs/    -> Wii games (.wbfs)
└── games/   -> GameCube games (one subfolder per game)
```

## Configuration in Open PS2 Loader (OPL)

Go to OPL → **Settings** → **Network Settings** (first set up networking with a static IP or DHCP) and fill in:

| OPL field | Value |
|---|---|
| **SMB Server** | The IP of the PC running the server (e.g. `192.168.1.10`). |
| **SMB Share Name** | The same share name (e.g. `PS2SMB`). |
| **Share Port** | The same port you set in the app (e.g. `445` or `1445`). |
| **SMB Username** | The user configured in the app (e.g. `ps2`). |
| **SMB Password** | The password configured in the app (e.g. `opl`), or leave it empty. |

Final steps in OPL:

1. Go back to the menu and select the **Network** (SMB) mode.
2. Press **Refresh / Scan** so OPL lists the games in `DVD/` and `CD/`.
3. Start a game: OPL reads the ISO over SMB in blocks of up to ~60 KB.

### Authentication notes

- If you leave the OPL password empty, the session logs in as **Guest**. In the app, **Allow Guest/Anonymous access** must be checked (it is by default).
- If you set a user and password, they must match exactly between the app and OPL.
- OPL speaks classic SMB1 (no extended security) and uses NTLMv1 over the NEGOTIATE challenge; the server handles it automatically.

## Building the .exe

From the project folder (the one containing `MultiSmbServer.csproj`):

```powershell
# Framework-dependent exe (requires .NET 8 Runtime on the target PC)
dotnet publish -c Release -r win-x64 --self-contained false -o publish

# Self-contained single-file exe (no .NET required, ~147 MB)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish-standalone
```

The executable ends up in `publish\MultiSmbServer.exe` (framework-dependent) or `publish-standalone\MultiSmbServer.exe` (self-contained).

## Security

SMBv1 is an old, unencrypted protocol (it is what OPL/PS2 and Wii homebrew require, unavoidable). To avoid exposing yourself like Windows' native service, the app applies these measures:

- **LAN only (enabled by default)**: rejects connections from public/Internet IPs (only accepts `192.168.x.x`, `10.x.x.x`, `172.16–31.x.x`, loopback and IPv6 link-local/ULA). Uncheck it only if you know what you are doing.
- **Non-standard port**: use a high port (e.g. `1445`) instead of `445` to reduce automated scans.
- **Do not port-forward** the port on your router: the server is only for your local network.
- **Credentials**: set a user/password (or use Guest). Remember that authentication is NTLMv1, weak by design of SMB1.

Unlike Windows' native `LanmanServer`, this app does not run with system privileges nor listen on `0.0.0.0` with the full Windows stack exposed.

## Technical notes

- The server uses **SMBLibrary** (1.5.0) with SMB1 only (`enableSMB1=true`), which is what OPL and Wii homebrew require.
- Supports **multiple simultaneous shares** (PS2, Wii/GameCube, etc.) on the same server and port.
- Supported ports: `445` (Direct TCP), `139` (NetBIOS over TCP) or **any custom port** (in that case the server listens on Direct TCP on that port).
- Filesystem access uses `NTDirectoryFileSystem` (SMBLibrary.Win32).
- Logs (connections, authentication, tree connects and reads) are shown in the embedded console with timestamps.
- File logs are throttled to avoid saturating the UI during game loading; unhandled exceptions are written to `%APPDATA%\MultiSmbServer\crash.log`.

## Support the project

If you find it useful, you can buy me a coffee:

- [Ko-fi](https://ko-fi.com/elanvzone)
- [Cafecito](https://cafecito.app/elanvzone)
