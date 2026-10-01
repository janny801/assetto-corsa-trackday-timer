# Assetto Corsa Track Day Session Timer Mod

A bytecode patch and UI extension for **Content Manager** that introduces a session duration slider to **Track Day** mode in Assetto Corsa, allowing sessions to automatically count down, end, and transition to results just like Practice sessions in Weekend mode.

---

## Screenshots

### 1. Content Manager Track Day Duration Slider
The new duration slider placed seamlessly under the **Opponents** count in the Track Day settings grid:

![Content Manager Track Day Slider](assets/cm_trackday_slider.png)

### 2. In-Game Session Timer & Overtime Flag
When the configured time expires in-game, Assetto Corsa triggers the checkered flag and displays **PRACTICE OVER**:

![Assetto Corsa Practice Over](assets/ac_session_over.png)

---

## How It Works

### Game Engine Session Rules & Overtime
Assetto Corsa's physics engine (`acs.exe`) treats both **Weekend Practice** and **Track Day** as `[SESSION_0]` with `TYPE=1`. 

When `DURATION_MINUTES` is specified in `race.ini`:
1. The in-game HUD displays a countdown timer from your configured duration.
2. Once the countdown reaches `0:00`, the engine displays **"PRACTICE OVER"** and switches the timer to **"Time: Overtime"**.
3. In accordance with authentic motorsport practice rules, cars out on track are not abruptly frozen mid-corner; instead, drivers are allowed to **complete their in-progress lap** and return to the pit lane (or hit `Esc` $\rightarrow$ **Back to Pits**).
4. Once you cross the finish line or return to pits, the session concludes and transitions to the session results screen.

---

## The Problem in Vanilla Content Manager

In the stock version of Content Manager:
- `DURATION_MINUTES = 720` (12 hours) was hardcoded inside `actools.dll` (`Game+TrackdayProperties.SetSessions`).
- The `QuickDrive_Trackday` view model had no duration property or persistence logic.
- The UI had no slider control for setting a track day time limit.

---

## The Solution

This mod applies non-destructive IL bytecode patching via **Mono.Cecil**:

1. **`actools.dll` (`Game+TrackdayProperties.SetSessions`)**:
   - Replaced the hardcoded `720` with:
     ```csharp
     section["DURATION_MINUTES"] = (this.Duration > 0) ? (int)this.Duration : 720;
     ```
   - Setting the slider to `0` maps to `720` minutes ("Unlimited"), ensuring 100% backwards compatibility with existing presets.
   - Re-compressed via Deflate and updated the embedded `costura.actools.dll.compressed` resource in `Content Manager.exe`.

2. **`QuickDrive_Trackday.ViewModel`**:
   - Added `TrackdayDuration` property (clamped 0 to 180 min).
   - Added `TrackdayDuration` to `SaveableData` so user presets and session lengths persist across app restarts.
   - Updated `GetModeProperties()` to set `Duration = (double)TrackdayDuration`.

3. **WPF UI Injection (`QuickDrive_Trackday.OnLoaded`)**:
   - Programmatically injects a `StackPanel` containing:
     - A `ValueLabel` bound to `TrackdayDuration` using Content Manager's native `ZeroToOffConverter` ("Track Day: Unlimited" at 0, "Track Day: X minutes" when active). Supports inline numerical editing on click.
     - A smooth WPF `Slider` (0 to 180 minutes).
   - Positioned at **Column 1, Row 2** of the settings grid, directly matching Weekend mode's Practice duration slider layout.
   - Compiled directly as a native method on `QuickDrive_Trackday` with **zero new external assembly references**, avoiding Costura JIT pre-load exceptions.

---

## Project Structure

```text
assetto-corsa-trackday-timer/
├── assets/
│   ├── cm_trackday_slider.png     # Screenshot of Content Manager UI
│   └── ac_session_over.png        # Screenshot of in-game Practice Over screen
├── diffs/
│   ├── actools_SetSessions.cs     # Code diff for actools.dll
│   └── QuickDrive_Trackday.cs     # Code diff for QuickDrive_Trackday
├── scripts/
│   ├── run_fullpatcher.ps1        # Script to build and apply patch
│   └── verify_patched_cm.ps1      # IL bytecode verification script
├── src/
│   ├── FullPatcher.cs             # Standalone C# Mono.Cecil patcher tool
│   ├── TrackdayDurationHelper.cs  # UI layout & binding helper
│   ├── actools_patched.dll        # Patched actools assembly
│   └── lib/                       # Mono.Cecil libraries
└── README.md
```

---

## How to Build and Apply

### Prerequisites
- Windows 10 / 11
- .NET Framework 4.8 / Microsoft .NET `csc.exe`
- Content Manager for Assetto Corsa

### Building and Applying
Run the included PowerShell patch script:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\run_fullpatcher.ps1
```

The script will:
1. Load your original `Content Manager.exe`.
2. Update the embedded `costura.actools.dll.compressed` resource with the patched session writer.
3. Inject the `TrackdayDuration` view model properties, serialization, and WPF UI elements.
4. Verify all bytecode instructions and assembly references.
5. Save the patched executable ready for use.
