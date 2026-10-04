# Assetto Corsa Track Day Session Timer & End Manager

A CSP Lua app for **Assetto Corsa** Track Day. It leaves the original Content Manager, Assetto Corsa
executable, session mode, and CSP AI Flood implementation untouched.

## Features

- ⏱️ **In-Game Duration Prompt**: An automatically opened centered setup panel accepts a typed
  duration from 1–180 minutes and focuses the input automatically.
- 💾 **Persistent Settings**: Selected session lengths are saved in Content Manager presets and persist across restarts.
- 🚦 **AI Flood Compatibility**: Uses the original Content Manager and preserves native Track Day
  mode selection, CSP, spawn behavior, and AI cars unchanged.
- 🏁 **Session-Over Message**: Displays **`TRACK DAY OVER`** when the app timer expires.
- 🔄 **Lap-Safe End Flow**: After expiry, the app waits for the current lap to finish or for the
  car to enter the pits, then teleports to the pits and ends Assetto Corsa.
- 📌 **Automatic App Opening**: After you finish setting up the timer in the pits and click
  **Drive**, the temporary setup panel opens automatically. It closes after you select a duration,
  and the session begins.
- 🕒 **Draggable Timer HUD**: After you select a duration, a text-only timer appears automatically
  with no surrounding window background. Drag it anywhere on screen.

## Screenshots

### 1. Track Day Timer Setup
The timer setup screen opens automatically when the Track Day session starts. The minutes field is
focused automatically, so type the desired duration and press **START TRACK DAY** (or Enter):

![Track Day Timer setup](assets/trackday-timer-setup.png)

### 2. In-Game Timer HUD
After starting the session, the setup screen closes and the transparent, draggable timer remains
visible as text only:

![Track Day Timer HUD](assets/trackday-timer-hud.png)

## Installation

1. Download or clone this private repository.
2. Close Content Manager and Assetto Corsa.
3. Copy the repository's `apps` folder into the Assetto Corsa installation folder, merging it
   with the existing `apps` folder. The default installation path is:
   `C:\Program Files (x86)\Steam\steamapps\common\assettocorsa`
4. Alternatively, run `Patch.bat` as administrator. The current installer installs the CSP Lua
   app and does not patch `Content Manager.exe` or `acs.exe`.
5. Launch the same native **Track Day** configuration that previously worked with AI Flood.
6. Restart Assetto Corsa once after installation so CSP rescans the Lua app manifest.
7. After finishing the setup in the pits, click **Drive**. The Track Day Timer setup screen
   opens automatically and focuses the minutes field. Type a value from 1 to 180, then press
   **START TRACK DAY** or Enter.
8. After the timer is set, the setup screen closes and the transparent timer HUD appears. Drag it
   to the preferred position; CSP saves that HUD position for later sessions.

### Requirements

- Assetto Corsa with Custom Shaders Patch Lua apps enabled.
- A CSP version meeting the manifest's `REQUIRED_VERSION` value.
- A native Content Manager Track Day session. Do not use the experimental native Content Manager
  patch, because it changes the session behavior and disables AI Flood.

### Tested CSP Versions

The app has been tested and works with both:

- CSP `0.3.0-preview542`
- CSP `0.2.12-preview1`

## How It Works

When a Track Day session starts, CSP opens the Track Day Timer app. Enter a duration from 1 to
180 minutes and select **START TRACK DAY**. The temporary setup panel closes automatically, and a
draggable countdown HUD appears.

The timer runs alongside the native Track Day session and does not replace Content Manager's
session controls or CSP AI Flood. When time expires, the app shows **TRACK DAY OVER**, waits for
the current lap to finish or for the car to enter the pits, then ends the session.

The timer state is stored with CSP so hiding and restoring Lua apps does not restart the timer.
The app also checks the native session countdown to avoid carrying a timer into a new Track Day
session with the same name.

### For Modders

The app is in `apps/lua/TrackdayTimer/`. `manifest.ini` defines the CSP Lua app, and
`TrackdayTimer.lua` contains the timer, setup window, HUD, persistence, and session-end logic.
The app uses CSP Lua APIs and does not modify `race.ini`, controls, session modes, AI vehicles,
Content Manager, or the Assetto Corsa executable.

## How to Uninstall / Restore

Both original files are automatically backed up before any modifications:
- **Restore Content Manager**: Delete `Content Manager.exe` and rename `Content Manager.original.exe` back to `Content Manager.exe`.
- **Restore Engine**: No engine restore is required; This implementation does not modify `acs.exe`. CSP's AI Flood implementation is documented as
Track Day-only; the patch does not try to force Flood into Practice or Qualifying because those
modes use different AI behavior and changing CSP would risk the working baseline.
- **Remove Lua App**: Delete the folder `assettocorsa/apps/lua/TrackdayTimer/`.

## License

MIT License. Open source for the Assetto Corsa sim racing community.
