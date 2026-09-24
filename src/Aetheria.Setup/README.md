# Aetheria.Setup — installer builder

Turns the **published** binaries into ready-to-ship installers, one per platform. It does not build
the game: publish first (the profiles in `src/Aetheria/Properties/PublishProfiles` write straight into
`C:\Deploy\Aetheria`), then run this.

```bash
dotnet run --project src/Aetheria.Setup
```

```
C:\Deploy\Aetheria\                <- source (--source)
    WinX64\      Aetheria.exe + raylib.dll
    LinuxX64\    Aetheria     + libraylib.so
    Installers\                    <- output (--out)
        Aetheria-1.0-Setup-win-x64.exe
        Aetheria-1.0-Setup-linux-x64.sh
```

Options: `--source DIR`, `--out DIR`, `--targets win-x64,linux-x64`, `--version 1.0`
(by default the version is read from the published Windows exe). A target whose folder is missing —
or whose Raylib native (`raylib.dll` / `libraylib.so`) is absent — is skipped with a message, so you
can build just the platforms you have.

> **Platforms.** Raylib-cs 8.0.0 ships native binaries for **win-x64** and **linux-x64** only (plus
> macOS). There is no 32/64-bit ARM Linux native, so — unlike the MonoGame-based Terraforming Mars,
> which shipped a Linux-ARM build — Aetheria targets Windows x64 and Linux x64. A macOS `.app`/`.dmg`
> installer is a possible future addition (the native exists; only the packaging is missing).

## What the installers do

| | Windows | Linux / Linux ARM |
|---|---|---|
| Format | self-extracting `.exe` (IExpress — part of Windows, no WiX/Inno needed) | self-extracting `.sh` (tar.gz appended after a marker line, `makeself` style) |
| Installs to | `%LOCALAPPDATA%\Programs\Aetheria` (per user, **no admin**) | `/opt/aetheria` as root, otherwise `~/.local/share/aetheria` |
| Menu entry | Start menu shortcut | `aetheria.desktop` in the applications menu |
| Desktop icon | shortcut on the desktop | `.desktop` on the desktop (honours `XDG_DESKTOP_DIR`, marked trusted for GNOME) |
| Icon | the exe's own icon | the 256px PNG pulled out of `Icon.ico`, installed into `hicolor` |
| Extras | entry in *Apps & features* with an uninstaller | `aetheria` symlink in `~/.local/bin`, `uninstall.sh` in the install folder |

Saved games live in `%APPDATA%\Aetheria` (Windows) / `~/.config/Aetheria` (Linux) — see
`SaveStore` — and are **never** touched by install or uninstall.

## Running them

* **Windows** — double-click the `.exe`. A console window walks through the install and offers to
  launch the game. To script it, extract first and call the script with parameters:

  ```
  Aetheria-1.0-Setup-win-x64.exe /C /T:%TEMP%\ae /Q
  powershell -ExecutionPolicy Bypass -File %TEMP%\ae\install.ps1 -Silent -Dir "C:\Games\Aetheria" -NoDesktopIcon
  ```

* **Linux** — `sh Aetheria-1.0-Setup-linux-x64.sh` (or `chmod +x` it first). Options:
  `--prefix DIR`, `--no-desktop-icon`, `-y`. Run it with `sudo` for a system-wide install.
  Aetheria renders with OpenGL through X11; a normal desktop install already has what it needs
  (mesa/libGL, libX11). No audio libraries are required — Raylib mixes sound internally.

## Notes for maintainers

* The install/uninstall scripts live in `Templates/` and are embedded in the tool; `@PLACEHOLDER@`
  tokens (app name, version, exe name…) are filled in when an installer is built.
* The app icon is `src/Aetheria/Icon.ico` (generated procedurally, in keeping with the game). The
  game exe embeds it via `<ApplicationIcon>`; this tool embeds the same file to pull out the 256px
  PNG for the Linux `.desktop` icon.
* The Windows installer can only be built on Windows (IExpress); the Linux ones build anywhere.

### IExpress traps (all of them fail silently — cost a full debugging round each)

| Trap | Symptom | Rule |
|---|---|---|
| Quoted `.sed` path on the command line | `iexpress` exits with `1`, builds nothing | run it *inside* the staging folder and pass a bare `setup.sed` |
| `AppLaunched=cmd /c install.cmd` | the package extracts, runs, and does **nothing at all** | `AppLaunched` must name a file **inside** the package: `AppLaunched=install.cmd` (IExpress already sets the extraction folder as the working directory) |
| `ShowInstallProgramWindow=1` | install runs invisibly; any prompt hangs forever | `0` = **visible** window, `1` = hidden |
| `.cmd` written with LF endings | `': was unexpected at this time'`, or `'install.cmd' is not recognized` | batch files need **CRLF** and no BOM; `.ps1` wants CRLF **with** BOM so non-ASCII comments survive Windows PowerShell 5.1 |
