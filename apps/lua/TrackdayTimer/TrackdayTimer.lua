-- TrackdayTimer.lua
-- Assetto Corsa Track Day Session Concluder
-- Automatically manages Track Day overtime, brings car back to pits,
-- prevents car from driving away, displays persistent banners,
-- and closes the session back to Content Manager.

local isTrackDay = false
local sessionOvertime = false
local sessionEnded = false
local initialOvertimeLap = nil
local stoppedTimer = 0
local overtimeTimer = 0
local messageRefreshTimer = 0
local endStageTimer = 0
local closeAttemptTimer = 0

-- LuaJIT FFI for Win32 PostMessage (to send WM_CLOSE to acs.exe, matching pit power button)
local ffi_ok, ffi = pcall(require, 'ffi')
if ffi_ok then
    pcall(function()
        ffi.cdef[[
            int PostMessageA(void* hWnd, unsigned int Msg, unsigned long long wParam, long long lParam);
            void* FindWindowA(const char* lpClassName, const char* lpWindowName);
        ]]
    end)
end

local function closeSession()
    if not ffi_ok or not ffi then return false end
    local closed = false
    pcall(function()
        local sim = ac.getSim()
        local hwnd = nil
        if sim and sim.windowHandle and sim.windowHandle ~= 0 then
            hwnd = ffi.cast('void*', ffi.cast('uintptr_t', sim.windowHandle))
        end
        if hwnd == nil or hwnd == ffi.cast('void*', 0) then
            local user32 = ffi.load('user32')
            hwnd = user32.FindWindowA(nil, 'Assetto Corsa')
        end
        if hwnd and hwnd ~= ffi.cast('void*', 0) then
            -- WM_CLOSE = 0x0010 (Gracefully triggers AC shutdown, writes race_out.json, returns to CM)
            ffi.C.PostMessageA(hwnd, 0x0010, 0, 0)
            closed = true
        end
    end)
    return closed
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

        -- Session completion conditions (like other competitive modes):
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
            closeAttemptTimer = 0
            
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
        closeAttemptTimer = closeAttemptTimer + dt

        -- 1. Hard lock vehicle controls every single frame so car cannot drive
        local controls = ac.overrideCarControls(0)
        if controls then
            controls.gas = 0
            controls.brake = 1
            controls.handbrake = 1
            controls.clutch = 1
            controls.gear = 0
        end

        -- 2. Prevent player from moving away in the pits: if outside pit stall, snap back immediately
        if not car.isInPit then
            ac.tryToTeleportToPits()
            ac.tryToOpenRaceMenu('time')
        end

        -- 3. Continuously maintain pit race menu
        if endStageTimer < 2.0 then
            ac.tryToOpenRaceMenu('time')
            ac.setMessage("TRACK DAY HAS ENDED", "Session concluded. Exiting to Content Manager...", nil, 3600)
        end

        -- 4. Gracefully close session after 1.5 seconds in pits (giving smooth pit arrival transition)
        -- Trigger WM_CLOSE like the pit power button
        if endStageTimer >= 1.5 and closeAttemptTimer >= 1.0 then
            closeAttemptTimer = 0
            closeSession()
        end
    end
end
