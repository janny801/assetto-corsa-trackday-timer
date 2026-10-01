-- TrackdayTimer.lua
-- Displays a persistent notification when Assetto Corsa's native Track Day
-- countdown expires. The native session controls the actual transition.

local isTrackDay = false
local sessionOver = false
local messageTimer = 0

local function isTrackDaySession()
    local sim = ac.getSim()
    if not sim then return false end

    local firstName = string.lower(ac.getSessionName(0) or "")
    local currentName = string.lower(ac.getSessionName(sim.currentSessionIndex) or "")
    return (firstName:find("track") and firstName:find("day")) or
           (currentName:find("track") and currentName:find("day"))
end

local function showSessionOver()
    ac.setMessage(
        "TRACK DAY OVER",
        "The Track Day timer has expired. Assetto Corsa is ending the session.",
        nil,
        3600
    )
end

function script.update(dt)
    local sim = ac.getSim()
    if not sim or not sim.isSessionStarted then return end

    if not isTrackDay then
        isTrackDay = isTrackDaySession()
        if not isTrackDay then return end
    end

    if not sessionOver and sim.sessionTimeLeft <= 0 then
        sessionOver = true
        messageTimer = 0
        showSessionOver()
    elseif sessionOver then
        messageTimer = messageTimer + dt
        if messageTimer >= 2.5 then
            messageTimer = 0
            showSessionOver()
        end
    end
end
