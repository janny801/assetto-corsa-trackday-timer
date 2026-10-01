# Assetto Corsa Track Day Session Timer & End Manager

A safe CSP Lua timer for **Assetto Corsa** Track Day. It leaves the original Content Manager,
Assetto Corsa executable, and CSP AI Flood implementation untouched.

---

## Features

- ⏱️ **Configurable Timer**: Set `DURATION_MINUTES` in the installed app's `settings.ini`.
- 💾 **Persistent Settings**: Selected session lengths are saved in Content Manager presets and persist across restarts.
- 🚦 **AI Flood Compatibility**: Uses the original Content Manager and preserves native Track Day
  mode selection, CSP, spawn behavior, and AI cars unchanged.
- 🏁 **Session-Over Message**: Displays **`TRACK DAY OVER`** when the native Track Day countdown reaches zero.
- 🔄 **Native Session Conclusion**: Assetto Corsa remains responsible for ending the session and presenting its normal session controls.

---

## Screenshots

### 1. Content Manager Track Day Duration Slider
The new duration slider placed seamlessly under the **Opponents** count in the Track Day settings grid:

![Content Manager Track Day Slider](assets/cm_trackday_slider.png)

### 2. In-Game Session Timer & Overtime Flag
When the configured time expires in-game, Assetto Corsa displays **TRACK DAY OVER** and switches the timer to Overtime:

![Assetto Corsa Track Day Over](assets/ac_session_over.png)

---

## Installation

### Installation
1. Download or clone this repository.
2. Make sure **Content Manager** and **Assetto Corsa** are closed.
3. Double-click **`Patch.bat`** (or right-click $\rightarrow$ Run as administrator if your game is in Program Files).
4. Copy the repository's `apps` folder into the Assetto Corsa root.
5. Edit `apps\lua\TrackdayTimer\settings.ini` and set `DURATION_MINUTES`.

---

---

## How It Works

### TrackdayTimer Lua App (CSP)
Custom Shaders Patch runs `apps/lua/TrackdayTimer/` in the background during Track Day sessions:
- Counts from session start using `settings.ini`.
- When the configured time expires, repeatedly broadcasts **"TRACK DAY OVER"**.
- Does not rewrite `race.ini`, change session modes, teleport, pause, close the process, alter
  controls, or manipulate AI vehicles.
- Assetto Corsa remains responsible for its native 12-hour session transition; this safe mode does
  not claim to change the engine's native duration.

---

## How to Uninstall / Restore

Both original files are automatically backed up before any modifications:
- **Restore Content Manager**: Delete `Content Manager.exe` and rename `Content Manager.original.exe` back to `Content Manager.exe`.
- **Restore Engine**: No engine restore is required; This implementation does not modify `acs.exe`. CSP's AI Flood implementation is documented as
Track Day-only; the patch does not try to force Flood into Practice or Qualifying because those
modes use different AI behavior and changing CSP would risk the working baseline.
- **Remove Lua App**: Delete the folder `assettocorsa/apps/lua/TrackdayTimer/`.

---

## Project Structure

```text
assetto-corsa-trackday-timer/
├── apps/
│   └── lua/
│       └── TrackdayTimer/
│           ├── manifest.ini           # CSP Lua app manifest
│           └── TrackdayTimer.lua      # Native countdown expiry notification
├── assets/
│   ├── cm_trackday_slider.png         # Screenshot of Content Manager UI
│   └── ac_session_over.png            # Screenshot of in-game session over
├── diffs/
│   ├── actools_SetSessions.cs         # Code diff for actools.dll
│   └── QuickDrive_Trackday.cs         # Code diff for QuickDrive_Trackday
├── scripts/
│   ├── Patch.ps1                      # Automated compilation & patching script
│   └── verify_patched_cm.ps1          # IL bytecode verification script
├── src/
│   ├── FullPatcher.cs                 # Standalone C# Mono.Cecil patcher tool
│   ├── TrackdayDurationHelper.cs      # UI layout & binding helper
│   ├── actools_patched.dll            # Patched actools assembly
│   ├── actools_compressed.bin         # Deflate-compressed actools resource
│   └── lib/                           # Mono.Cecil & TrackdayHelper assemblies
├── Patch.bat                          # 1-Click installer batch script
└── README.md
```

---

## License

MIT License. Open source for the Assetto Corsa sim racing community.
