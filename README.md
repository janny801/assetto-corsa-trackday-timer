# Assetto Corsa Track Day Session Timer & End Manager

A complete, two-part mod for **Assetto Corsa** and **Content Manager** that introduces a session duration slider to **Track Day** mode, patches the native in-game banner to display **"TRACK DAY OVER"**, and automatically brings the car back to pits to conclude the session just like competitive modes.

---

## Features

- ⏱️ **Content Manager Duration Slider**: Adds a customizable session duration slider (0 to 180 minutes) to the Track Day settings grid. Setting it to 0 sets it to Unlimited (12 hours).
- 💾 **Persistent Settings**: Selected session lengths are saved in Content Manager presets and persist across restarts.
- 🚦 **AI Flood Compatibility**: Preserves Content Manager's native Track Day session-type setting so CSP's AI Flood behavior is not disabled.
- 🏁 **Authentic In-Game Banner**: Replaces the hardcoded engine message `"PRACTICE OVER"` with **`"TRACK DAY OVER"`** in Assetto Corsa's native engine (`acs.exe`).
- 🚗 **Automatic Session Conclusion**: When the timer expires, the session enters overtime allowing you to finish your current flying lap (or enter the pit lane). Once completed (or upon stopping), the car is automatically brought to pits, controls are secured, and the session results/race menu opens.

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
   - Detect Assetto Corsa and `acs.exe` (creates `acs.original.exe` backup).
   - Patch `Content Manager.exe` with the duration slider and session writer.
   - Patch `acs.exe` to display **"TRACK DAY OVER"**.
   - Install the `TrackdayTimer` Lua app into `assettocorsa/apps/lua/TrackdayTimer/`.

---

### Option 2: Drag & Drop (Standard AC Mod Style)
If you prefer manual installation:

1. **In-Game Session Finisher**:
   - Copy the **`apps`** folder from this repository directly into your Assetto Corsa root directory (e.g., `C:\Program Files (x86)\Steam\steamapps\common\assettocorsa\`).
   - This installs `apps/lua/TrackdayTimer/` which handles automatic pit return and session conclusion.

2. **Content Manager & Engine Patcher**:
   - Run `Patch.bat` to patch your `Content Manager.exe` and `acs.exe`.

---

## How It Works

### 1. Content Manager & actools.dll
In vanilla Content Manager, Track Day duration was hardcoded to 720 minutes (12 hours) inside `actools.dll` (`TrackdayProperties.SetSessions`). 

This mod modifies `actools.dll` and `QuickDrive_Trackday.ViewModel` via **Mono.Cecil**:
```csharp
section["DURATION_MINUTES"] = (this.Duration > 0) ? (int)this.Duration : 720;
```
It dynamically injects a WPF `Slider` and `ValueLabel` into `QuickDrive_Trackday.xaml` on load with zero external assembly dependencies. The patch does not override `UsePracticeSessionType`; this preserves Content Manager's native Track Day behavior and lets CSP determine AI Flood behavior from the active CSP configuration.

For AI Flood, the active CSP configuration must contain the following values. The standalone
`Documents\Assetto Corsa\cfg\extension\new_behaviour.ini` uses `[AI_FLOOD]`; a Content Manager
custom preset uses the namespaced section `[NEW_BEHAVIOUR:AI_FLOOD]`:
```ini
[NEW_BEHAVIOUR:AI_FLOOD]
ENABLED=1
MIN_TRACK_LENGTH=0
PUSH_FORCE=100000
PUSH_SPEED=80
SHUFFLE_BEHAVIOUR=AUTO
SPEED_LIMIT=60,80
```
The timer patch does not spawn, remove, or reposition AI cars. If cars remain in pit stalls, verify that
the track has a valid AI fast lane, the correct section name is used for the configuration location,
and the edited Content Manager preset is actually active. Content Manager can report custom presets as
inactive even when a preset file exists on disk.

### 2. Assetto Corsa Engine (acs.exe)
In `acs.exe`, session type 1 (Practice) hardcodes the wide-string `L"PRACTICE OVER"` (length 13). The patcher updates:
- String at `0x4CA5F0` $\rightarrow$ `L"TRACK DAY OVER"` (length 14).
- Instruction string lengths at `0x13826F` and `0x1383EA` $\rightarrow$ `14` (`0x0E`).

### 3. TrackdayTimer Lua App (CSP)
Custom Shaders Patch runs `apps/lua/TrackdayTimer/` in the background during Track Day sessions:
- Listens to `sim.sessionTimeLeft`.
- When time expires (`<= 0`), broadcasts **"TRACK DAY OVER"** notification.
- Watches player telemetry:
  - If on a flying lap, allows lap completion across the finish line.
  - If driver enters pitlane or brings car to a stop, triggers conclusion.
- Calls `ac.tryToTeleportToPits()`, immobilizes vehicle controls, and cleanly closes the session back to Content Manager (saving session results and displaying the summary screen).

---

## How to Uninstall / Restore

Both original files are automatically backed up before any modifications:
- **Restore Content Manager**: Delete `Content Manager.exe` and rename `Content Manager.original.exe` back to `Content Manager.exe`.
- **Restore Engine**: Delete `acs.exe` and rename `acs.original.exe` back to `acs.exe` in your Assetto Corsa directory.
- **Remove Lua App**: Delete the folder `assettocorsa/apps/lua/TrackdayTimer/`.

---

## Project Structure

```text
assetto-corsa-trackday-timer/
├── apps/
│   └── lua/
│       └── TrackdayTimer/
│           ├── manifest.ini           # CSP Lua app manifest
│           └── TrackdayTimer.lua      # Session conclusion & pit return logic
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
