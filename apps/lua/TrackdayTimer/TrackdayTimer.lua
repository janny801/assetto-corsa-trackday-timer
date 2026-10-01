-- TrackdayTimer.lua
-- CSP app timer which leaves the native Track Day session and AI Flood untouched.

local durationMinutes = 1
local durationText = '1'
local durationStarted = false
local elapsed = 0
local sessionOver = false
local finishRequested = false
local shutdownAt = nil
local positionSet = false
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

local function showMessage(title, description, duration)
    ac.setMessage(title, description, nil, duration or 5)
end

local function requestFinishSession()
    if finishRequested then return end
    finishRequested = true
    showMessage('TRACK DAY OVER', 'Returning to the pits and ending Assetto Corsa.')
    ac.tryToTeleportToPits()
    shutdownAt = 3
end

local function openTimerApp()
    if not durationStarted then
        ac.setWindowOpen('main', true)
    end
end

function script.windowMain(dt)
    local sim = ac.getSim()
    if not sim or not sim.isSessionStarted or not isTrackDay() then
        ui.text('Start a Track Day session to use the timer.')
        return
    end

    if not positionSet then
        ui.setNextWindowPosition(vec2(960, 540), vec2(0.5, 0.5))
        ui.setNextWindowSize(vec2(900, 560))
        ac.setWindowSizeConstraints('main', vec2(520, 360), vec2(1400, 900))
        positionSet = true
    end

    ui.drawRectFilled(vec2(0, 0), ui.windowSize(), rgbm(0.02, 0.03, 0.05, 0.96), 12)
    ui.drawRectFilled(vec2(0, 0), vec2(ui.windowSize().x, 12), rgbm(0.85, 0.08, 0.04, 1), 12)
    ui.pushFont(ui.Font.Title)
    ui.header('Track Day Timer')
    ui.popFont()
    if not durationStarted then
        ui.pushFont(ui.Font.Main)
        ui.text('SET YOUR TRACK DAY DURATION')
        ui.popFont()
        ui.textWrapped('Choose how long this session should run. AI Flood and the native Track Day mode remain unchanged.')
        ui.separator()
        ui.text('Session length (minutes)')
        ui.setNextItemWidth(ui.availableSpaceX())
        if ui.isWindowAppearing() then
            ui.setKeyboardFocusHere()
        end
        local editedText, _, enterPressed = ui.inputText(
            '##duration',
            durationText,
            ui.InputTextFlags.CharsDecimal
        )
        durationText = editedText
        local enteredMinutes = tonumber(durationText)
        if enteredMinutes then
            durationMinutes = math.max(1, math.min(180, math.floor(enteredMinutes + 0.5)))
        end
        ui.pushFont(ui.Font.Title)
        if ui.button('START TRACK DAY', vec2(ui.availableSpaceX(), 55)) or enterPressed then
            durationText = tostring(durationMinutes)
            durationStarted = true
            elapsed = 0
            sessionOver = false
            finishRequested = false
            lapAtExpiry = nil
            showMessage('TRACK DAY TIMER', string.format('%d-minute timer started.', durationMinutes))
            ac.setWindowOpen('main', false)
            ac.setWindowOpen('hud', true)
        end
        ui.popFont()
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

    local remaining = math.max(0, durationMinutes * 60 - elapsed)
    ui.pushFont(ui.Font.Title)
    if sessionOver then
        ui.textColored('TRACK DAY OVER', rgbm(1, 0.2, 0.1, 1))
    else
        ui.textColored(string.format('TRACK DAY TIMER  %02d:%02d', math.floor(remaining / 60), math.floor(remaining % 60)),
            rgbm(1, 1, 1, 1))
    end
    ui.popFont()
end

function script.update(dt)
    local sim = ac.getSim()
    if not sim or not sim.isSessionStarted or not isTrackDay() then
        positionSet = false
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
            showMessage('TRACK DAY OVER', 'Finish this lap or enter the pits to end the session.', 10)
        end
        return
    end

    local completedLap = lapAtExpiry and car.lapCount > lapAtExpiry
    local inPits = car.isInPitlane or car.isInPit
    if completedLap or inPits then
        requestFinishSession()
    end
end
