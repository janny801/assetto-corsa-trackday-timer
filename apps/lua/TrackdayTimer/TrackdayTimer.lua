-- TrackdayTimer.lua
-- CSP app timer which leaves the native Track Day session and AI Flood untouched.

local durationMinutes = 1
local durationStarted = false
local elapsed = 0
local sessionOver = false
local finishRequested = false
local autoOpened = false
local shutdownAt = nil
local positionSet = false
local hudPositionSet = false
local sessionIndex = nil
local lapAtExpiry = nil

local function isTrackDay()
    local sim = ac.getSim()
    if not sim then return false end
    local current = string.lower(ac.getSessionName(sim.currentSessionIndex) or '')
    local first = string.lower(ac.getSessionName(0) or '')
    return (current:find('track') ~= nil and current:find('day') ~= nil) or
           (first:find('track') ~= nil and first:find('day') ~= nil)
end

local function showMessage(title, description)
    ac.setMessage(title, description, nil, 10)
end

local function requestFinishSession()
    if finishRequested then return end
    finishRequested = true
    showMessage('TRACK DAY OVER', 'Returning to the pits and ending Assetto Corsa.')
    ac.tryToTeleportToPits()
    shutdownAt = 3
end

local function openTimerApp()
    if not autoOpened then
        ac.setWindowOpen('main', true)
        ac.setWindowOpen('hud', true)
        autoOpened = true
    end
end

function script.windowMain(dt)
    local sim = ac.getSim()
    if not sim or not sim.isSessionStarted or not isTrackDay() then
        ui.text('Start a Track Day session to use the timer.')
        return
    end

    if not positionSet then
        ui.setNextWindowPosition(vec2(760, 360))
        positionSet = true
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
            lapAtExpiry = nil
            showMessage('TRACK DAY TIMER', string.format('%d-minute timer started.', durationMinutes))
        end
        return
    end

    local remaining = math.max(0, durationMinutes * 60 - elapsed)
    ui.text(string.format('Remaining: %02d:%02d', math.floor(remaining / 60), math.floor(remaining % 60)))
    if sessionOver then
        ui.textWrapped('Timer expired. Finish this lap or enter the pits to end the session.')
    else
        ui.text('AI Flood and native Track Day mode are unchanged.')
    end
end

function script.windowHUD(dt)
    local sim = ac.getSim()
    if not sim or not sim.isSessionStarted or not isTrackDay() or not durationStarted then return end

    if not hudPositionSet then
        ui.setNextWindowPosition(vec2(760, 35))
        hudPositionSet = true
    end

    local remaining = math.max(0, durationMinutes * 60 - elapsed)
    if sessionOver then
        ui.pushFont(ui.Font.Main)
        ui.textColored('TRACK DAY OVER', rgbm(1, 0.2, 0.1, 1))
        ui.popFont()
    else
        ui.pushFont(ui.Font.Main)
        ui.text(string.format('TRACK DAY TIMER  %02d:%02d', math.floor(remaining / 60), math.floor(remaining % 60)))
        ui.popFont()
    end
end

function script.update(dt)
    local sim = ac.getSim()
    if not sim or not sim.isSessionStarted or not isTrackDay() then
        autoOpened = false
        positionSet = false
        hudPositionSet = false
        sessionIndex = nil
        return
    end

    if sessionIndex ~= sim.currentSessionIndex then
        sessionIndex = sim.currentSessionIndex
        durationStarted = false
        elapsed = 0
        sessionOver = false
        finishRequested = false
        shutdownAt = nil
        lapAtExpiry = nil
    end

    openTimerApp()
    if shutdownAt then
        shutdownAt = shutdownAt - dt
        if shutdownAt <= 0 then
            ac.tryToSkipSession()
            shutdownAt = math.huge
            if ac.shutdownAssettoCorsa then
                ac.shutdownAssettoCorsa()
            end
        end
        return
    end

    if not durationStarted then return end

    local car = ac.getCar(0)
    if not car then return end

    if not sessionOver then
        elapsed = elapsed + dt
        if elapsed >= durationMinutes * 60 then
            sessionOver = true
            lapAtExpiry = car.lapCount
            showMessage('TRACK DAY OVER', 'Finish this lap or enter the pits to end the session.')
        end
        return
    end

    local completedLap = lapAtExpiry and car.lapCount > lapAtExpiry
    local inPits = car.isInPitlane or car.isInPit
    if completedLap or inPits then
        requestFinishSession()
    end
end
