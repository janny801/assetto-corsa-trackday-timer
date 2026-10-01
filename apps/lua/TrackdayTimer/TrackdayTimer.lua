-- TrackdayTimer.lua
-- CSP app timer which leaves the native Track Day session and AI Flood untouched.

local durationMinutes = 1
local durationStarted = false
local elapsed = 0
local sessionOver = false
local lapAtExpiry = nil
local finishRequested = false
local messageTimer = 0

local function isTrackDay()
    local sim = ac.getSim()
    if not sim then return false end
    local current = string.lower(ac.getSessionName(sim.currentSessionIndex) or '')
    local first = string.lower(ac.getSessionName(0) or '')
    return (current:find('track') ~= nil and current:find('day') ~= nil) or
           (first:find('track') ~= nil and first:find('day') ~= nil)
end

local function showMessage(title, description)
    ac.setMessage(title, description, nil, 3600)
end

local function finishSession()
    if finishRequested then return end
    finishRequested = true
    showMessage('TRACK DAY OVER', 'Returning to pits and opening the session results.')
    ac.tryToTeleportToPits()
    ac.tryToSkipSession()
end

function script.windowMain(dt)
    local sim = ac.getSim()
    if not sim or not sim.isSessionStarted or not isTrackDay() then
        ui.text('Start a Track Day session to use the timer.')
        return
    end

    ui.header('Track Day Timer')
    if not durationStarted then
        ui.textWrapped('Choose how long this Track Day should run, then press Start.')
        ui.setNextItemWidth(ui.availableSpaceX())
        durationMinutes = math.floor(ui.slider('##duration', durationMinutes, 1, 180, 'Session length: %.0f min') + 0.5)
        if ui.button('Start timer', vec2(ui.availableSpaceX(), 0)) then
            durationStarted = true
            elapsed = 0
            sessionOver = false
            finishRequested = false
            showMessage('TRACK DAY TIMER', string.format('%d-minute timer started.', durationMinutes))
        end
        return
    end

    local remaining = math.max(0, durationMinutes * 60 - elapsed)
    ui.text(string.format('Remaining: %02d:%02d', math.floor(remaining / 60), math.floor(remaining % 60)))
    if sessionOver then
        ui.textWrapped('Timer expired. Complete the current lap or enter the pits.')
    else
        ui.text('AI Flood and native Track Day mode are unchanged.')
    end
end

function script.update(dt)
    local sim = ac.getSim()
    if not sim or not sim.isSessionStarted or not isTrackDay() or not durationStarted then return end

    local car = ac.getCar(0)
    if not car then return end

    if not sessionOver then
        elapsed = elapsed + dt
        if sim.sessionTimeLeft <= 0 or elapsed >= durationMinutes * 60 then
            sessionOver = true
            lapAtExpiry = car.lapCount
            messageTimer = 0
            showMessage('TRACK DAY OVER', 'Complete your current lap or return to the pits.')
        end
        return
    end

    messageTimer = messageTimer + dt
    if messageTimer >= 2.5 and not finishRequested then
        messageTimer = 0
        showMessage('TRACK DAY OVER', 'Complete your current lap or return to the pits.')
    end

    local completedLap = lapAtExpiry and car.lapCount > lapAtExpiry
    local inPits = car.isInPitlane or car.isInPit
    if completedLap or inPits then
        finishSession()
    end
end
