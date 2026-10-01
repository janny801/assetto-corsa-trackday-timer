# Assetto Corsa Track Day Session Timer & End Manager

A small Content Manager patch and CSP Lua notification for **Assetto Corsa** Track Day. It changes the duration written by Content Manager while leaving the original Assetto Corsa executable and CSP AI Flood implementation untouched.

---

## Features

- ⏱️ **Content Manager Duration Slider**: Adds a customizable session duration slider (0 to 180 minutes) to the Track Day settings grid. Setting it to 0 preserves the native 12-hour default.
- 💾 **Persistent Settings**: Selected session lengths are saved in Content Manager presets and persist across restarts.
- 🚦 **AI Flood Compatibility**: Explicitly writes native Track Day (`TYPE=2`) instead of Practice
  (`TYPE=1`), while leaving `acs.exe`, CSP, spawn behavior, and AI cars unchanged.
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

### Option 1: 1-Click Patcher (Recommended)
1. Download or clone this repository.
2. Make sure **Content Manager** and **Assetto Corsa** are closed.
3. Double-click **`Patch.bat`** (or right-click $\rightarrow$ Run as administrator if your game is in Program Files).
4. The patcher will automatically:
   - Detect `Content Manager.exe` (creates `Content Manager.original.exe` backup).
   - Patch `Content Manager.exe` with the duration slider and session writer.
   - Leave `acs.exe` unchanged.
5. Copy the repository's `apps` folder into the Assetto Corsa root to install the Lua notification.

---

### Option 2: Drag & Drop (Standard AC Mod Style)
If you prefer manual installation:

1. **In-Game Notification**:
   - Copy the **`apps`** folder from this repository directly into your Assetto Corsa root directory (e.g., `C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\`).
   - This installs `apps/lua/TrackdayTimer/`, which displays the session-over message while
     leaving native session conclusion untouched.

2. **Content Manager Patcher**:
   - Run `Patch.bat` to patch only `Content Manager.exe`.

---

## How It Works

### 1. Content Manager & actools.dll
In the original Content Manager, Track Day writes `DURATION_MINUTES=720` (12 hours) inside
`actools.dll` (`TrackdayProperties.SetSessions`). This is the source of the countdown shown in
the session information panel.

This mod extracts the original embedded `actools.dll`, changes only
`TrackdayProperties.SetSessions`, and embeds that otherwise-original assembly back
into Content Manager via **Mono.Cecil**:
```csharp
section["DURATION_MINUTES"] = (this.Duration > 0) ? (int)this.Duration : 720;
```
It also dynamically injects a WPF `Slider` and `ValueLabel` into `QuickDrive_Trackday.xaml`.
The patch preserves `NAME`, `SPAWN_SET`, and all AI car entries. It explicitly sets
`UsePracticeSessionType=false`, which makes the generated session `TYPE=2` (native Track Day).
This is required because CSP AI Flood is a Track Day feature and Content Manager can otherwise
generate the mode as Practice (`TYPE=1`). Only the duration and this session-mode selection are changed.

### 2. TrackdayTimer Lua App (CSP)
Custom Shaders Patch runs `apps/lua/TrackdayTimer/` in the background during Track Day sessions:
- Listens to `sim.sessionTimeLeft`.
- When time expires (`<= 0`), repeatedly broadcasts **"TRACK DAY OVER"**.
- Does not teleport, pause, close the process, alter controls, or manipulate AI vehicles.

---

## How to Uninstall / Restore

Both original files are automatically backed up before any modifications:
- **Restore Content Manager**: Delete `Content Manager.exe` and rename `Content Manager.original.exe` back to `Content Manager.exe`.
- **Restore Engine**: No engine restore is required; This implementation does not modify `acs.exe`. CSP's AI Flood implementation is documented as
Track Day-only; the patch does not try to force Flood into Practice because that would require
changing CSP behavior rather than the timer and would risk the working baseline.
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
