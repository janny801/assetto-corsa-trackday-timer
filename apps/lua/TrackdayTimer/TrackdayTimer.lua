-- TrackdayTimer.lua
-- Assetto Corsa Track Day Session Concluder
-- Automatically brings car back to pits and ends session when timer expires.

local isTrackDay = false
local sessionOvertime = false
local sessionEnded = false
local initialOvertimeLap = nil
local stoppedTimer = 0
local overtimeTimer = 0
local notifiedOvertime = false
local notifiedEnd = false

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
        end

        if not notifiedOvertime then
            notifiedOvertime = true
            ac.setMessage("TRACK DAY OVER", "Complete your lap or return to pits to conclude.")
        end

        overtimeTimer = overtimeTimer + dt

        -- Check if car has stopped after overtime
        if math.abs(car.speedKmh) < 5 then
            stoppedTimer = stoppedTimer + dt
        else
            stoppedTimer = 0
        end

        -- Session completion conditions (like other competitive modes):
        -- 1. Lap completed after overtime started
        -- 2. Car entered pitlane or pit stall
        -- 3. Car came to a stop on track (> 3.5 seconds)
        -- 4. Overtime limit reached (safety timeout: 120 seconds)
        local lapCompleted = initialOvertimeLap and (car.lapCount > initialOvertimeLap)
        local inPits = car.isInPitlane or car.isInPit
        local carStopped = stoppedTimer >= 3.5
        local timedOut = overtimeTimer >= 120.0

        if not sessionEnded and (lapCompleted or inPits or carStopped or timedOut) then
            sessionEnded = true
            if not notifiedEnd then
                notifiedEnd = true
                ac.setMessage("TRACK DAY HAS ENDED", "Returning to pits...")
            end

            -- Teleport car to pit stall
            ac.tryToTeleportToPits()

            -- Open race menu (summary/times/exit)
            ac.tryToOpenRaceMenu('time')
        end
    end

    -- If session has officially ended, keep player in pits
    if sessionEnded then
        if not car.isInPit and not car.isInPitlane then
            ac.tryToTeleportToPits()
            ac.tryToOpenRaceMenu('time')
        end
    end
end
