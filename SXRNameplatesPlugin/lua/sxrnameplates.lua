--[[
    SXR Nameplates - Player Nameplates Above Cars
    
    Settings accessible via Extended Chat / Online Extras menu (TAB key)
    
    Displays floating nameplates above other players' cars showing:
    - Driver Level badge
    - Player Name (color coded by Safety Rating)
    - Car Class (color coded by class)
    - Racer Club tag
    - Leaderboard Rank
    
    Design:
    [DL] PlayerName | CarClass
           ClubTag  #Rank
]]

-- API URL
local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/sxrnameplates"

-- State
local state = {
    enabled = true,
    loading = false,
    lastRefresh = 0,
    players = {},           -- Session ID -> NameplateData
    displayConfig = {
        showDriverLevel = true,
        showCarClass = true,
        showClubTag = true,
        showRank = true,
        showSafetyRating = true,
        maxDistance = 500,
        fadeDistance = 300,
        heightOffset = 2.5
    },
    -- User settings
    scale = 1.0,
    showOwnNameplate = false,
    refreshInterval = 5
}

-- Color definitions
local colors = {
    -- Safety Rating colors
    safetyS = rgbm(1, 0.84, 0, 1),      -- Gold
    safetyA = rgbm(0, 1, 0, 1),          -- Green
    safetyB = rgbm(0, 0.75, 1, 1),       -- Blue
    safetyC = rgbm(1, 1, 0, 1),          -- Yellow
    safetyD = rgbm(1, 0.65, 0, 1),       -- Orange
    safetyF = rgbm(1, 0, 0, 1),          -- Red
    
    -- Car Class colors
    classS = rgbm(0.6, 0.2, 0.8, 1),     -- Purple
    classA = rgbm(1, 0.27, 0.27, 1),     -- Red
    classB = rgbm(1, 0.65, 0, 1),        -- Orange
    classC = rgbm(1, 1, 0, 1),           -- Yellow
    classD = rgbm(0, 1, 0, 1),           -- Green
    classE = rgbm(0, 0.75, 1, 1),        -- Blue
    
    -- Prestige colors
    prestige0 = rgbm(1, 1, 1, 1),         -- White (no prestige)
    prestige1 = rgbm(1, 0.84, 0, 1),      -- Gold
    prestige2 = rgbm(1, 0.42, 0.42, 1),   -- Coral Red
    prestige3 = rgbm(0.61, 0.35, 0.71, 1), -- Purple
    prestige4 = rgbm(0.2, 0.6, 0.86, 1),   -- Blue
    prestige5 = rgbm(0.18, 0.8, 0.44, 1),  -- Emerald
    prestige6 = rgbm(0.91, 0.3, 0.24, 1),  -- Red
    prestige7 = rgbm(0.95, 0.61, 0.07, 1), -- Orange
    prestige8 = rgbm(0.1, 0.74, 0.61, 1),  -- Turquoise
    prestige9 = rgbm(0.91, 0.12, 0.39, 1), -- Pink
    prestige10 = rgbm(1, 0.08, 0.58, 1),   -- Deep Pink
    prestigeHigh = rgbm(1, 0, 1, 1),       -- Magenta (P11-19)
    prestigeLegend = rgbm(0, 1, 1, 1),     -- Aqua (P20-49)
    -- P50+ uses rainbow gradient (calculated dynamically)
    
    -- UI colors
    background = rgbm(0.05, 0.05, 0.08, 0.85),
    backgroundLight = rgbm(0.1, 0.1, 0.15, 0.9),
    levelBadge = rgbm(0.15, 0.15, 0.2, 0.95),
    white = rgbm(1, 1, 1, 1),
    dimmed = rgbm(0.6, 0.6, 0.6, 1),
    accent = rgbm(0, 0.8, 1, 1)
}

-- ============================================================================
-- COLOR HELPERS
-- ============================================================================

function GetSafetyRatingColor(rating)
    if rating == "S" then return colors.safetyS
    elseif rating == "A" then return colors.safetyA
    elseif rating == "B" then return colors.safetyB
    elseif rating == "C" then return colors.safetyC
    elseif rating == "D" then return colors.safetyD
    elseif rating == "F" then return colors.safetyF
    else return colors.white
    end
end

function GetCarClassColor(carClass)
    if carClass == "S" then return colors.classS
    elseif carClass == "A" then return colors.classA
    elseif carClass == "B" then return colors.classB
    elseif carClass == "C" then return colors.classC
    elseif carClass == "D" then return colors.classD
    elseif carClass == "E" then return colors.classE
    else return colors.dimmed
    end
end

-- HSV to RGB conversion for rainbow effect
function HSVtoRGB(h, s, v)
    local r, g, b
    local i = math.floor(h * 6)
    local f = h * 6 - i
    local p = v * (1 - s)
    local q = v * (1 - f * s)
    local t = v * (1 - (1 - f) * s)
    
    i = i % 6
    if i == 0 then r, g, b = v, t, p
    elseif i == 1 then r, g, b = q, v, p
    elseif i == 2 then r, g, b = p, v, t
    elseif i == 3 then r, g, b = p, q, v
    elseif i == 4 then r, g, b = t, p, v
    elseif i == 5 then r, g, b = v, p, q
    end
    
    return r, g, b
end

-- Get rainbow color based on time (cycles through spectrum)
function GetRainbowColor(speed)
    speed = speed or 1.0
    local hue = (os.clock() * speed) % 1.0
    local r, g, b = HSVtoRGB(hue, 1.0, 1.0)
    return rgbm(r, g, b, 1)
end

function GetPrestigeColor(prestigeRank)
    if prestigeRank == 0 then return colors.prestige0
    elseif prestigeRank == 1 then return colors.prestige1
    elseif prestigeRank == 2 then return colors.prestige2
    elseif prestigeRank == 3 then return colors.prestige3
    elseif prestigeRank == 4 then return colors.prestige4
    elseif prestigeRank == 5 then return colors.prestige5
    elseif prestigeRank == 6 then return colors.prestige6
    elseif prestigeRank == 7 then return colors.prestige7
    elseif prestigeRank == 8 then return colors.prestige8
    elseif prestigeRank == 9 then return colors.prestige9
    elseif prestigeRank == 10 then return colors.prestige10
    elseif prestigeRank < 20 then return colors.prestigeHigh
    elseif prestigeRank < 50 then return colors.prestigeLegend
    else 
        -- P50+ gets animated rainbow gradient!
        return GetRainbowColor(0.5)
    end
end

function FormatDriverLevel(driverLevel, prestigeRank)
    if prestigeRank and prestigeRank > 0 then
        return string.format("P%d - %d", prestigeRank, driverLevel)
    else
        return tostring(driverLevel)
    end
end

-- ============================================================================
-- API FUNCTIONS
-- ============================================================================

function FetchNameplateData()
    if state.loading then return end
    state.loading = true
    
    web.get(baseUrl .. "/sync", function(err, response)
        state.loading = false
        if not err and response.body then
            local data = stringify.parse(response.body)
            if data then
                -- Update display config
                if data.DisplayConfig then
                    state.displayConfig = data.DisplayConfig
                end
                
                -- Update player data
                state.players = {}
                if data.Players then
                    for _, player in ipairs(data.Players) do
                        state.players[player.SessionId] = player
                    end
                end
            end
        end
        state.lastRefresh = os.clock()
    end)
end

-- ============================================================================
-- NAMEPLATE RENDERING
-- ============================================================================

function DrawNameplate(car, data, distance, opacity)
    if not data then return end
    
    local scale = state.scale * (1 - (distance / state.displayConfig.maxDistance) * 0.3)
    scale = math.max(scale, 0.5)
    
    -- Calculate sizes
    local baseHeight = 14 * scale
    local padding = 4 * scale
    local cornerRadius = 3 * scale
    
    -- Measure text widths
    ui.pushFont(ui.Font.Small)
    
    -- Format level with prestige: [P# - DL] or just [DL]
    local prestigeRank = data.PrestigeRank or 0
    local driverLevel = data.DriverLevel or 1
    local levelText = "[" .. FormatDriverLevel(driverLevel, prestigeRank) .. "]"
    
    local nameText = data.Name or "Unknown"
    local carClassText = data.CarClass or "D"
    local clubText = data.ClubTag or ""
    local rankText = data.LeaderboardRank and data.LeaderboardRank > 0 
        and string.format("#%d", data.LeaderboardRank) or ""
    
    -- Calculate widths
    local levelWidth = ui.measureText(levelText).x
    local nameWidth = ui.measureText(nameText).x
    local classWidth = ui.measureText(carClassText).x
    local clubWidth = clubText ~= "" and ui.measureText(clubText).x or 0
    local rankWidth = rankText ~= "" and ui.measureText(rankText).x or 0
    
    -- Row 1: [P# - DL] Name | Class
    local row1Width = 0
    if state.displayConfig.showDriverLevel then
        row1Width = row1Width + levelWidth + padding * 2 + padding
    end
    row1Width = row1Width + nameWidth + padding
    if state.displayConfig.showCarClass then
        row1Width = row1Width + padding + classWidth
    end
    row1Width = row1Width + padding * 2
    
    -- Row 2: ClubTag  #Rank
    local row2Width = 0
    local hasRow2 = false
    if state.displayConfig.showClubTag and clubText ~= "" then
        row2Width = row2Width + clubWidth + padding * 2
        hasRow2 = true
    end
    if state.displayConfig.showRank and rankText ~= "" then
        row2Width = row2Width + rankWidth + padding * 2
        hasRow2 = true
    end
    
    local totalWidth = math.max(row1Width, row2Width)
    local totalHeight = baseHeight + padding * 2
    if hasRow2 then
        totalHeight = totalHeight + baseHeight + padding
    end
    
    -- Center the nameplate
    local startX = -totalWidth / 2
    local startY = -totalHeight
    
    -- Apply opacity
    local bgColor = rgbm(colors.background.r, colors.background.g, colors.background.b, colors.background.a * opacity)
    local badgeColor = rgbm(colors.levelBadge.r, colors.levelBadge.g, colors.levelBadge.b, colors.levelBadge.a * opacity)
    
    -- Draw background
    ui.drawRectFilled(
        vec2(startX, startY),
        vec2(startX + totalWidth, startY + totalHeight),
        bgColor,
        cornerRadius
    )
    
    -- Draw subtle border
    ui.drawRect(
        vec2(startX, startY),
        vec2(startX + totalWidth, startY + totalHeight),
        rgbm(0.3, 0.3, 0.3, 0.5 * opacity),
        cornerRadius
    )
    
    -- Row 1 content
    local x = startX + padding
    local y = startY + padding
    
    -- Driver Level badge (color changes based on prestige)
    if state.displayConfig.showDriverLevel then
        local badgeWidth = levelWidth + padding * 2
        
        -- Badge background - slightly tinted by prestige color for high prestige
        local prestigeBadgeColor = badgeColor
        if prestigeRank >= 10 then
            local pColor = GetPrestigeColor(prestigeRank)
            prestigeBadgeColor = rgbm(
                colors.levelBadge.r + pColor.r * 0.1,
                colors.levelBadge.g + pColor.g * 0.1,
                colors.levelBadge.b + pColor.b * 0.1,
                colors.levelBadge.a * opacity
            )
        end
        
        ui.drawRectFilled(
            vec2(x, y),
            vec2(x + badgeWidth, y + baseHeight),
            prestigeBadgeColor,
            cornerRadius / 2
        )
        
        -- Level text - color based on prestige rank
        ui.setCursor(vec2(x + padding, y + (baseHeight - 10 * scale) / 2))
        local levelColor = GetPrestigeColor(prestigeRank)
        levelColor = rgbm(levelColor.r, levelColor.g, levelColor.b, opacity)
        ui.textColored(levelText, levelColor)
        
        x = x + badgeWidth + padding
    end
    
    -- Player Name (color by safety rating)
    ui.setCursor(vec2(x, y + (baseHeight - 10 * scale) / 2))
    local nameColor = GetSafetyRatingColor(data.SafetyRating or "C")
    nameColor = rgbm(nameColor.r, nameColor.g, nameColor.b, opacity)
    ui.textColored(nameText, nameColor)
    
    x = x + nameWidth
    
    -- Separator and Car Class
    if state.displayConfig.showCarClass then
        -- Separator
        ui.setCursor(vec2(x + padding / 2, y + (baseHeight - 10 * scale) / 2))
        ui.textColored("|", rgbm(0.4, 0.4, 0.4, opacity))
        
        x = x + padding
        
        -- Car Class
        ui.setCursor(vec2(x + padding / 2, y + (baseHeight - 10 * scale) / 2))
        local classColor = GetCarClassColor(carClassText)
        classColor = rgbm(classColor.r, classColor.g, classColor.b, opacity)
        ui.textColored(carClassText, classColor)
    end
    
    -- Row 2 content (centered)
    if hasRow2 then
        y = y + baseHeight + padding
        x = startX + (totalWidth - row2Width) / 2 + padding
        
        -- Club Tag
        if state.displayConfig.showClubTag and clubText ~= "" then
            ui.setCursor(vec2(x, y + (baseHeight - 10 * scale) / 2))
            ui.textColored(clubText, rgbm(colors.dimmed.r, colors.dimmed.g, colors.dimmed.b, opacity))
            x = x + clubWidth + padding * 2
        end
        
        -- Leaderboard Rank
        if state.displayConfig.showRank and rankText ~= "" then
            ui.setCursor(vec2(x, y + (baseHeight - 10 * scale) / 2))
            ui.textColored(rankText, rgbm(colors.accent.r, colors.accent.g, colors.accent.b, opacity))
        end
    end
    
    ui.popFont()
end

-- ============================================================================
-- MAIN 3D RENDER LOOP
-- ============================================================================

function script.draw3D()
    if not state.enabled then return end
    
    -- Refresh data if needed
    if os.clock() - state.lastRefresh > state.refreshInterval then
        FetchNameplateData()
    end
    
    local camera = ac.getCameraPosition()
    local cameraDir = ac.getCameraForward()
    local mySessionId = ac.getCar(0).sessionID
    
    -- Iterate through all cars
    for i = 0, ac.getSim().carsCount - 1 do
        local car = ac.getCar(i)
        if car and car.isConnected then
            local sessionId = car.sessionID
            
            -- Skip self unless debug mode
            if sessionId == mySessionId and not state.showOwnNameplate then
                goto continue
            end
            
            -- Get nameplate data
            local data = state.players[sessionId]
            if not data then
                -- Create placeholder data from car info
                data = {
                    SessionId = sessionId,
                    Name = ac.getDriverName(i) or "Driver",
                    DriverLevel = 1,
                    CarClass = "D",
                    SafetyRating = "C",
                    ClubTag = "",
                    LeaderboardRank = 0
                }
            end
            
            -- Get car position
            local carPos = car.position
            local nameplatePos = vec3(
                carPos.x,
                carPos.y + state.displayConfig.heightOffset,
                carPos.z
            )
            
            -- Calculate distance
            local distance = (nameplatePos - camera):length()
            
            -- Skip if too far
            if distance > state.displayConfig.maxDistance then
                goto continue
            end
            
            -- Check if behind camera (dot product)
            local toNameplate = (nameplatePos - camera):normalize()
            local dot = toNameplate:dot(cameraDir)
            if dot < 0 then
                goto continue
            end
            
            -- Calculate opacity based on distance
            local opacity = 1.0
            if distance > state.displayConfig.fadeDistance then
                local fadeRange = state.displayConfig.maxDistance - state.displayConfig.fadeDistance
                local fadeProgress = (distance - state.displayConfig.fadeDistance) / fadeRange
                opacity = 1.0 - fadeProgress * 0.7
            end
            
            -- Draw nameplate in 3D space
            ui.beginPivot(nameplatePos, vec2(0, 1), false)
            DrawNameplate(car, data, distance, opacity)
            ui.endPivot()
        end
        
        ::continue::
    end
end

-- ============================================================================
-- SETTINGS PANEL (Online Extras)
-- ============================================================================

function DrawSettingsPanel()
    ui.pushFont(ui.Font.Title)
    ui.textColored("SXR Nameplates", colors.accent)
    ui.popFont()
    ui.spacing()
    
    -- Enable toggle
    local changed, newValue = ui.checkbox("Enable Nameplates", state.enabled)
    if changed then
        state.enabled = newValue
    end
    
    ui.spacing()
    ui.separator()
    ui.spacing()
    
    -- Scale slider
    ui.text("Scale:")
    ui.sameLine(80)
    ui.setNextItemWidth(150)
    state.scale = ui.slider("##scale", state.scale, 0.5, 2.0, "%.1fx")
    
    -- Refresh interval
    ui.text("Refresh:")
    ui.sameLine(80)
    ui.setNextItemWidth(150)
    state.refreshInterval = ui.slider("##refresh", state.refreshInterval, 1, 15, "%.0fs")
    
    ui.spacing()
    
    -- Show own nameplate (debug)
    local changed2, newValue2 = ui.checkbox("Show Own Nameplate (Debug)", state.showOwnNameplate)
    if changed2 then
        state.showOwnNameplate = newValue2
    end
    
    ui.spacing()
    ui.separator()
    ui.spacing()
    
    -- Display options
    ui.textColored("Display Options", colors.accent)
    ui.spacing()
    
    local c1, v1 = ui.checkbox("Show Driver Level", state.displayConfig.showDriverLevel)
    if c1 then state.displayConfig.showDriverLevel = v1 end
    
    local c2, v2 = ui.checkbox("Show Car Class", state.displayConfig.showCarClass)
    if c2 then state.displayConfig.showCarClass = v2 end
    
    local c3, v3 = ui.checkbox("Show Club Tag", state.displayConfig.showClubTag)
    if c3 then state.displayConfig.showClubTag = v3 end
    
    local c4, v4 = ui.checkbox("Show Rank", state.displayConfig.showRank)
    if c4 then state.displayConfig.showRank = v4 end
    
    ui.spacing()
    ui.separator()
    ui.spacing()
    
    -- Info
    ui.textColored("Players tracked: " .. TableLength(state.players), colors.dimmed)
    
    -- Return false to keep panel open
    return false
end

-- ============================================================================
-- HELPERS
-- ============================================================================

function TableLength(t)
    local count = 0
    for _ in pairs(t) do count = count + 1 end
    return count
end

-- ============================================================================
-- INITIALIZATION
-- ============================================================================

-- Initial data fetch
FetchNameplateData()

-- Register in Online Extras menu
setTimeout(function()
    ui.registerOnlineExtra(
        ui.Icons.Eye,
        "SXR Nameplates",
        function() return true end,  -- Always visible
        DrawSettingsPanel,
        vec2(280, 350)
    )
    ac.log("SXR Nameplates registered in Online Extras")
end, 1000)

ac.debug("SXR Nameplates", "Loaded")
