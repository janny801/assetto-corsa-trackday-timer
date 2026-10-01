// Original implementation in AcTools.Processes.Game+TrackdayProperties.SetSessions:
//
// protected internal override void SetSessions(IniFile ini)
// {
//     IniFileSection section = ini["SESSION_0"];
//     section["NAME"] = base.SessionName;
//     section["DURATION_MINUTES"] = 720; // <-- Hardcoded 12 hours!
//     section["SPAWN_SET"] = StartType.Pit.Id;
//     section["TYPE"] = ((!this.UsePracticeSessionType) ? SessionType.TrackDay : SessionType.Practice);
//     section["__SPEED_LIMIT"] = this.SpeedLimit;
// }

// Patched implementation:
//
// protected internal override void SetSessions(IniFile ini)
// {
//     IniFileSection section = ini["SESSION_0"];
//     section["NAME"] = base.SessionName;
//     section["DURATION_MINUTES"] = (this.Duration > 0) ? (int)this.Duration : 720;
//     section["SPAWN_SET"] = StartType.Pit.Id;
//     section["TYPE"] = ((!this.UsePracticeSessionType) ? SessionType.TrackDay : SessionType.Practice);
//     section["__SPEED_LIMIT"] = this.SpeedLimit;
// }
