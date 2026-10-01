-- TrackdayTimer.lua
-- Safe timer overlay: it does not rewrite the session type or manipulate AI.

local durationMinutes = 1
local elapsed = 0
local sessionOver = false
local messageTimer = 0

local function loadDuration()
    local file = io.open('apps/lua/TrackdayTimer/settings.ini', 'r')
    if not file then return end
    for line in file:lines() do
        local value = line:match('^%s*DURATION_MINUTES%s*=%s*(%d+%.?%d*)')
        if value then
            durationMinutes = tonumber(value) or durationMinutes
            break
        end
    end
    file:close()
end

local function isTrackDay()
    local sim = ac.getSim()
    if not sim then return false end
    local name = string.lower(ac.getSessionName(sim.currentSessionIndex) or '')
    return name:find('track') ~= nil and name:find('day') ~= nil
end

local function showExpired()
    ac.setMessage(
        'TRACK DAY OVER',
        'The configured timer has expired. Return to the pits and end the session normally.',
        nil,
        3600
    )
end

function script.update(dt)
    local sim = ac.getSim()
    if not sim or not sim.isSessionStarted or not isTrackDay() then return end

    elapsed = elapsed + dt
    if not sessionOver and elapsed >= durationMinutes * 60 then
        sessionOver = true
        showExpired()
    elseif sessionOver then
        messageTimer = messageTimer + dt
        if messageTimer >= 2.5 then
            messageTimer = 0
            showExpired()
        end
    end
end

loadDuration()
