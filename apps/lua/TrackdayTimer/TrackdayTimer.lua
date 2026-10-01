-- TrackdayTimer.lua
-- Assetto Corsa Track Day Session Concluder
-- Automatically manages Track Day overtime, returns car to pits,
-- and cleanly concludes the session back to Content Manager.

local isTrackDay = false
local sessionOvertime = false
local sessionEnded = false
local initialOvertimeLap = nil
local stoppedTimer = 0
local overtimeTimer = 0
local messageRefreshTimer = 0
local endStageTimer = 0
local exitTriggered = false

-- LuaJIT FFI for Win32 API
local ffi_ok, ffi = pcall(require, 'ffi')
if ffi_ok and ffi then
    pcall(function()
        ffi.cdef[[
            int PostMessageA(void* hWnd, unsigned int Msg, unsigned long long wParam, long long lParam);
            int SendMessageA(void* hWnd, unsigned int Msg, unsigned long long wParam, long long lParam);
            void* FindWindowA(const char* lpClassName, const char* lpWindowName);
        ]]
    end)
end

local function closeSession()
    if exitTriggered then return end
    exitTriggered = true

    -- 1. Pause physics cleanly to eliminate any controller input fighting/glitching
    pcall(ac.tryToPause, true)

    -- 2. Send WM_CLOSE via Win32 user32.dll (matching pit screen power button)
    if ffi_ok and ffi then
        pcall(function()
            local user32 = ffi.load('user32')
            if user32 then
                local hwnd = user32.FindWindowA(nil, 'Assetto Corsa')
                if hwnd and hwnd ~= ffi.cast('void*', 0) then
                    user32.PostMessageA(hwnd, 0x0010, 0, 0) -- WM_CLOSE
                    user32.SendMessageA(hwnd, 0x0010, 0, 0) -- WM_CLOSE
                end
            end
        end)
    end

    -- 3. Trigger graceful exit via PowerShell CloseMainWindow() with safety fallback
    pcall(function()
        if os and os.execute then
            os.execute('powershell -NoProfile -WindowStyle Hidden -Command "$p = Get-Process acs -ErrorAction SilentlyContinue; if ($p) { $p.CloseMainWindow(); Start-Sleep -Milliseconds 2000; if (-not $p.HasExited) { $p.Kill() } }"', 3000, true)
        end
    end)
end

local function checkIsTrackDay()
    local sim = ac.getSim()
    if not sim then return false end
    local s0 = ac.getSessionName(0) or ""
    local sCurrent = ac.getSessionName(sim.currentSessionIndex) or ""
    local sLower0 = string.lower(s0)
    local sLowerCurr = string.lower(sCurrent)
    if (sLower0:find("track") and sLower0:find("day")) or 
       (sLowerCurr:find("track") and sLowerCurr:find("day")) then
        return true
    end
    return false
end

function script.update(dt)
    local sim = ac.getSim()
    if not sim or not sim.isSessionStarted then
        return
    end

    if not isTrackDay then
        isTrackDay = checkIsTrackDay()
        if not isTrackDay then
            return
        end
    end

    local car = ac.getCar(0)
    if not car then return end

    -- Check if session duration has expired (timer <= 0)
    if sim.sessionTimeLeft <= 0 then
        if not sessionOvertime then
            sessionOvertime = true
            initialOvertimeLap = car.lapCount
            overtimeTimer = 0
            stoppedTimer = 0
            messageRefreshTimer = 0
            -- Show persistent message immediately (3600 seconds)
            ac.setMessage("TRACK DAY OVER", "Complete your lap or return to pits to conclude.", nil, 3600)
        end

        overtimeTimer = overtimeTimer + dt
        messageRefreshTimer = messageRefreshTimer + dt

        -- Keep the overtime message persistent on screen until driver returns to pits
        if not sessionEnded and messageRefreshTimer >= 2.5 then
            messageRefreshTimer = 0
            ac.setMessage("TRACK DAY OVER", "Complete your lap or return to pits to conclude.", nil, 3600)
        end

        -- Check if car has stopped after overtime
        if math.abs(car.speedKmh) < 5 then
            stoppedTimer = stoppedTimer + dt
        else
            stoppedTimer = 0
        end

        -- Session completion conditions:
        -- 1. Lap completed after overtime started (lapCount > initialOvertimeLap)
        -- 2. Car entered pitlane or pit stall
        -- 3. Car came to a stop on track (> 3.5 seconds)
        -- 4. Overtime safety timeout (> 120 seconds)
        local lapCompleted = initialOvertimeLap and (car.lapCount > initialOvertimeLap)
        local inPits = car.isInPitlane or car.isInPit
        local carStopped = stoppedTimer >= 3.5
        local timedOut = overtimeTimer >= 120.0

        if not sessionEnded and (lapCompleted or inPits or carStopped or timedOut) then
            sessionEnded = true
            endStageTimer = 0
            
            -- Display persistent session finished message
            ac.setMessage("TRACK DAY HAS ENDED", "Session concluded. Returning to pits...", nil, 3600)
            
            -- Teleport car immediately to pit stall
            ac.tryToTeleportToPits()
            ac.tryToOpenRaceMenu('time')
        end
    end

    -- Session ended sequence
    if sessionEnded then
        endStageTimer = endStageTimer + dt

        -- Keep pit menu active and display closing banner
        if endStageTimer < 0.6 then
            ac.tryToOpenRaceMenu('time')
            ac.setMessage("TRACK DAY HAS ENDED", "Session concluded. Exiting to Content Manager...", nil, 3600)
        end

        -- After 0.6s (allowing teleport to pit stall to settle), cleanly exit
        if endStageTimer >= 0.6 and not exitTriggered then
            closeSession()
        end
    end
end
