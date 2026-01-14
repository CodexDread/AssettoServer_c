--[[
    SXR SP Battle Plugin - TXR-Style In-Game UI
    Based on Tokyo Xtreme Racer spirit point system
    
    Features:
    - Dual SP bars (player vs rival)
    - Distance indicator
    - Drain rate visualization
    - Countdown display
    - Win/Loss announcements
    - Leaderboard panel
]]

local config = ac.configValues({
    enableLeaderboard = true,
    hudScale = 1.0,
    hudPosition = vec2(0, 25), -- Top center offset
    showDistance = true,
    barWidth = 400,
    barHeight = 25
})

-- Event types matching C# plugin
local EventType = {
    None = 0,
    Challenge = 1,
    Countdown = 2,
    BattleUpdate = 3,
    BattleEnded = 4
}

-- Battle state
local battleState = {
    active = false,
    ownHealth = 1.0,
    ownRate = 0,
    rivalHealth = 1.0,
    rivalRate = 0,
    rivalId = 0,
    rivalName = "",
    distance = 0,
    startTime = 0,
    endTime = 0,
    lastWinner = 0,
    lastEvent = EventType.None
}

local ownSessionId = ac.getCar(0).sessionID
local leaderboardUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/spbattle/leaderboard"

-- ============================================================================
-- NETWORK EVENTS
-- ============================================================================

-- Battle status event (challenge, countdown, ended)
local battleStatusEvent = ac.OnlineEvent({
    eventType = ac.StructItem.byte(),
    eventData = ac.StructItem.int64(),
}, function(sender, data)
    -- Only accept packets from server
    if sender ~= nil then return end
    
    ac.debug("SXR SP Battle Event", data.eventType)
    
    if data.eventType == EventType.Challenge then
        battleState.active = true
        battleState.rivalId = data.eventData
        battleState.rivalName = GetDriverNameBySessionId(data.eventData)
        battleState.ownHealth = 1.0
        battleState.rivalHealth = 1.0
        battleState.ownRate = 0
        battleState.rivalRate = 0
    end
    
    if data.eventType == EventType.Countdown then
        battleState.startTime = data.eventData
    end
    
    if data.eventType == EventType.BattleEnded then
        battleState.lastWinner = data.eventData
        battleState.endTime = GetSessionTime()
        -- Keep showing for 3 seconds then hide
        setTimeout(function()
            battleState.active = false
            battleState.lastEvent = EventType.None
        end, 3000)
    end
    
    battleState.lastEvent = data.eventType
end)

-- Battle update event (SP values, distance)
local battleUpdateEvent = ac.OnlineEvent({
    ownHealth = ac.StructItem.float(),
    ownRate = ac.StructItem.float(),
    rivalHealth = ac.StructItem.float(),
    rivalRate = ac.StructItem.float(),
    distance = ac.StructItem.float()
}, function(sender, data)
    -- Only accept packets from server
    if sender ~= nil then return end
    
    battleState.ownHealth = data.ownHealth
    battleState.ownRate = data.ownRate
    battleState.rivalHealth = data.rivalHealth
    battleState.rivalRate = data.rivalRate
    battleState.distance = data.distance
end)

-- ============================================================================
-- HELPER FUNCTIONS
-- ============================================================================

function GetSessionTime()
    return ac.getSim().timeToSessionStart * -1
end

function GetDriverNameBySessionId(sessionId)
    local count = ac.getSim().carsCount
    for i = 0, count - 1 do
        local car = ac.getCar(i)
        if car.sessionID == sessionId then
            return ac.getDriverName(car.index)
        end
    end
    return "Unknown"
end

function GetLeaderboard(callback)
    web.get(leaderboardUrl, function(err, response)
        if err then
            callback(nil)
        else
            callback(stringify.parse(response.body))
        end
    end)
end

function GetOwnRanking(callback)
    web.get(leaderboardUrl .. "/" .. ac.getUserSteamID(), function(err, response)
        if err then
            callback(nil)
        else
            callback(stringify.parse(response.body))
        end
    end)
end

-- ============================================================================
-- UI DRAWING
-- ============================================================================

local lastUiUpdate = GetSessionTime()
local drainColors = {
    none = rgbm(0.2, 0.8, 0.2, 1),   -- Green - no drain
    low = rgbm(0.8, 0.8, 0.2, 1),    -- Yellow - low drain
    medium = rgbm(0.8, 0.5, 0.2, 1), -- Orange - medium drain
    high = rgbm(0.8, 0.2, 0.2, 1)   -- Red - high drain
}

function GetDrainColor(rate)
    rate = math.abs(rate)
    if rate < 0.02 then return drainColors.none end
    if rate < 0.05 then return drainColors.low end
    if rate < 0.1 then return drainColors.medium end
    return drainColors.high
end

function DrawSPBar(pos, size, health, rate, isOwn, name)
    local barColor = rgbm(1, 1, 1, 1)
    local drainColor = GetDrainColor(rate)
    
    health = math.clamp(health, 0, 1)
    
    -- Lerp color based on health
    barColor:setLerp(rgbm.colors.red, rgbm.colors.cyan, health)
    
    -- Background
    ui.drawRectFilled(pos, pos + size, rgbm(0.1, 0.1, 0.1, 0.8))
    
    -- Border
    ui.drawRect(pos, pos + size, rgbm(0.5, 0.5, 0.5, 1), 2)
    
    -- Health fill
    local fillWidth = size.x * health
    if isOwn then
        -- Own bar fills right to left
        ui.drawRectFilled(
            pos + vec2(size.x - fillWidth, 2),
            pos + vec2(size.x - 2, size.y - 2),
            barColor
        )
    else
        -- Rival bar fills left to right
        ui.drawRectFilled(
            pos + vec2(2, 2),
            pos + vec2(fillWidth, size.y - 2),
            barColor
        )
    end
    
    -- Drain rate indicator (pulsing edge)
    if rate < -0.01 then
        local pulseAlpha = 0.5 + 0.5 * math.sin(GetSessionTime() * 0.01)
        local drainIndicator = drainColor
        drainIndicator.mult = pulseAlpha
        
        if isOwn then
            ui.drawRect(pos + vec2(size.x - fillWidth - 3, 0), pos + vec2(size.x - fillWidth + 3, size.y), drainIndicator, 3)
        else
            ui.drawRect(pos + vec2(fillWidth - 3, 0), pos + vec2(fillWidth + 3, size.y), drainIndicator, 3)
        end
    end
    
    -- Name label
    ui.pushFont(ui.Font.Small)
    if isOwn then
        ui.drawText(name, pos + vec2(size.x - ui.measureText(name).x - 5, size.y + 2), rgbm.colors.white)
    else
        ui.drawText(name, pos + vec2(5, size.y + 2), rgbm.colors.white)
    end
    ui.popFont()
    
    -- SP percentage
    local pctText = string.format("%.0f%%", health * 100)
    ui.pushFont(ui.Font.Main)
    local textSize = ui.measureText(pctText)
    if isOwn then
        ui.drawText(pctText, pos + vec2(5, (size.y - textSize.y) / 2), rgbm.colors.white)
    else
        ui.drawText(pctText, pos + vec2(size.x - textSize.x - 5, (size.y - textSize.y) / 2), rgbm.colors.white)
    end
    ui.popFont()
end

function DrawCenteredText(text, yOffset)
    local uiState = ac.getUI()
    yOffset = yOffset or 0
    
    ui.transparentWindow('spBattleText', vec2(uiState.windowSize.x / 2 - 250, uiState.windowSize.y / 2 - 200 + yOffset), vec2(500, 100), function()
        ui.pushFont(ui.Font.Huge)
        
        local size = ui.measureText(text)
        ui.setCursorX(ui.getCursorX() + ui.availableSpaceX() / 2 - (size.x / 2))
        ui.text(text)
        
        ui.popFont()
    end)
end

function DrawBattleHUD(elapsedTime)
    local uiState = ac.getUI()
    local scale = config.hudScale
    local barWidth = config.barWidth * scale
    local barHeight = config.barHeight * scale
    local spacing = 10 * scale
    local totalWidth = barWidth * 2 + spacing
    
    local hudX = uiState.windowSize.x / 2 - totalWidth / 2 + config.hudPosition.x
    local hudY = config.hudPosition.y
    
    ui.toolWindow('spBattleHUD', vec2(hudX, hudY), vec2(totalWidth, 120 * scale), function()
        ui.pushFont(ui.Font.Title)
        
        -- Header row
        ui.columns(3)
        ui.setColumnWidth(0, barWidth)
        ui.setColumnWidth(1, spacing)
        
        ui.text("YOU")
        ui.nextColumn()
        
        -- Timer in center
        local timerText = ac.lapTimeToString(math.max(0, elapsedTime))
        local timerSize = ui.measureText(timerText)
        ui.text(timerText)
        ui.nextColumn()
        
        ui.textAligned("RIVAL", ui.Alignment.End, vec2(-1, 0))
        ui.columns()
        
        ui.popFont()
        
        -- SP Bars
        local barY = ui.getCursorY()
        DrawSPBar(
            vec2(0, barY),
            vec2(barWidth, barHeight),
            battleState.ownHealth,
            battleState.ownRate,
            true,
            ac.getDriverName(0)
        )
        
        DrawSPBar(
            vec2(barWidth + spacing, barY),
            vec2(barWidth, barHeight),
            battleState.rivalHealth,
            battleState.rivalRate,
            false,
            battleState.rivalName
        )
        
        -- Distance indicator
        if config.showDistance and battleState.distance > 0 then
            ui.setCursorY(barY + barHeight + 25 * scale)
            ui.pushFont(ui.Font.Small)
            
            local distText = string.format("Distance: %.0fm", battleState.distance)
            local distColor = battleState.distance > 100 and drainColors.high or 
                              battleState.distance > 50 and drainColors.medium or
                              drainColors.none
            
            ui.textColored(distText, distColor)
            ui.popFont()
        end
    end)
end

function script.drawUI()
    if not battleState.active then return end
    
    local currentTime = GetSessionTime()
    local dt = currentTime - lastUiUpdate
    lastUiUpdate = currentTime
    
    local raceTimeElapsed = currentTime - battleState.startTime
    
    -- Countdown phase
    if battleState.lastEvent == EventType.Countdown then
        if raceTimeElapsed > -3000 and raceTimeElapsed < 0 then
            DrawBattleHUD(0)
            local countdownNum = math.ceil(-raceTimeElapsed / 1000)
            DrawCenteredText(tostring(countdownNum))
        elseif raceTimeElapsed >= 0 then
            -- Race started
            if raceTimeElapsed < 1000 then
                DrawCenteredText("GO!")
            end
            
            -- Interpolate SP drain locally for smooth animation
            battleState.ownHealth = math.max(0, battleState.ownHealth + battleState.ownRate * (dt / 1000))
            battleState.rivalHealth = math.max(0, battleState.rivalHealth + battleState.rivalRate * (dt / 1000))
            
            DrawBattleHUD(raceTimeElapsed)
        end
    end
    
    -- Battle ended phase
    if battleState.lastEvent == EventType.BattleEnded then
        DrawBattleHUD(battleState.endTime - battleState.startTime)
        
        if battleState.lastWinner == 255 then
            DrawCenteredText("BATTLE CANCELLED")
        elseif battleState.lastWinner == ownSessionId then
            DrawCenteredText("VICTORY!", 0)
            ui.pushFont(ui.Font.Title)
            DrawCenteredText("SP DEPLETED", 60)
            ui.popFont()
        else
            DrawCenteredText("DEFEAT", 0)
        end
    end
end

-- ============================================================================
-- LEADERBOARD PANEL
-- ============================================================================

local loadingLeaderboard = false
local leaderboard = nil
local ownRanking = nil

function PrintLeaderboardRow(rank, name, rating, wins, losses)
    ui.text(tostring(rank))
    ui.nextColumn()
    ui.text(name)
    ui.nextColumn()
    ui.text(tostring(rating))
    ui.nextColumn()
    ui.text(string.format("%d/%d", wins, losses))
    ui.nextColumn()
end

if config.enableLeaderboard then
    ui.registerOnlineExtra(ui.Icons.Leaderboard, "SXR SP Battle Rankings", function() return true end, function()
        if not loadingLeaderboard then
            loadingLeaderboard = true
            GetLeaderboard(function(response)
                leaderboard = response
            end)
            GetOwnRanking(function(response)
                ownRanking = response
            end)
        end
        
        local close = false
        ui.childWindow('spBattleLeaderboard', vec2(0, 300), false, ui.WindowFlags.None, function()
            if leaderboard == nil or ownRanking == nil then
                ui.text("Loading...")
            else
                ui.pushFont(ui.Font.Title)
                ui.text("SP BATTLE RANKINGS")
                ui.popFont()
                ui.separator()
                
                ui.columns(4)
                ui.setColumnWidth(0, 40)
                ui.setColumnWidth(1, 180)
                ui.setColumnWidth(2, 70)
                
                ui.pushFont(ui.Font.Small)
                PrintLeaderboardRow("#", "Driver", "Rating", "W/L")
                ui.popFont()
                
                ui.separator()
                
                if leaderboard then
                    for i, player in ipairs(leaderboard) do
                        PrintLeaderboardRow(
                            tostring(i) .. ".",
                            player.Name,
                            player.Rating,
                            player.Wins,
                            player.Losses
                        )
                    end
                end
                
                ui.separator()
                
                -- Own ranking
                if ownRanking then
                    local rankStr = ownRanking.Rank > 0 and (ownRanking.Rank .. ".") or "--"
                    ui.pushStyleColor(ui.StyleColor.Text, rgbm.colors.cyan)
                    PrintLeaderboardRow(
                        rankStr,
                        ac.getDriverName(0),
                        ownRanking.Rating,
                        ownRanking.Wins,
                        ownRanking.Losses
                    )
                    ui.popStyleColor()
                end
                
                ui.columns()
            end
            
            ui.offsetCursorY(ui.availableSpaceY() - 32)
            if ui.button("Close") then
                close = true
                loadingLeaderboard = false
            end
            ui.sameLine()
            if ui.button("Refresh") then
                leaderboard = nil
                ownRanking = nil
                loadingLeaderboard = false
            end
        end)
        
        return close
    end)
end

-- Debug output
ac.debug("SXR SP Battle UI", "Loaded")
ac.debug("Own Session ID", ownSessionId)
