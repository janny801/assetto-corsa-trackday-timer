-- TrackdayTimer.lua
-- Assetto Corsa Track Day Session Concluder
-- Automatically brings car back to pits, keeps overtime message active,
-- and concludes session when timer expires.

local isTrackDay = false
local sessionOvertime = false
local sessionEnded = false
local initialOvertimeLap = nil
local stoppedTimer = 0
local overtimeTimer = 0
local messageRefreshTimer = 0
local endStage = 0
local endStageTimer = 0

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
        if not sessionEnded and messageRefreshTimer >= 3.0 then
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
            endStage = 1
            endStageTimer = 0
            
            -- Display persistent session finished message
            ac.setMessage("TRACK DAY HAS ENDED", "Session concluded. Returning to pits...", nil, 3600)
            
            -- Teleport car to pit stall
            ac.tryToTeleportToPits()
        end
    end

    -- Two-stage session end sequence to ensure smooth teleport and menu opening
    if sessionEnded then
        endStageTimer = endStageTimer + dt

        -- Lock vehicle controls in pits
        local controls = ac.overrideCarControls(0)
        if controls then
            controls.gas = 0
            controls.handbrake = 1
            controls.gear = 0
        end

        if endStage == 1 and endStageTimer >= 0.5 then
            endStage = 2
            
            -- Update message
            ac.setMessage("TRACK DAY HAS ENDED", "Session concluded. Select Exit to return to Content Manager.", nil, 3600)
            
            -- Tell AC engine to finish/skip session
            ac.tryToSkipSession()
            
            -- Open race menu & pause game so Exit/Restart menu is presented
            ac.tryToOpenRaceMenu('time')
            ac.tryToPause(true)

            -- Show interactive popup dialog in case pause menu was dismissed
            ui.modalPopup(
                "TRACK DAY HAS ENDED",
                "Track Day session has ended.\nYour car has returned to the pits.\n\nSelect Exit to view final results in Content Manager.",
                "Exit to Menu",
                "Stay in Pits",
                ui.Icons.Exit,
                nil,
                function(okPressed)
                    if okPressed then
                        ac.tryToPause(true)
                    end
                end
            )
        end

        -- If user somehow leaves pit box after session ended, return them to pits
        if not car.isInPit and not car.isInPitlane then
            ac.tryToTeleportToPits()
        end
    end
end
