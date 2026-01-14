--[[
    SXR Welcome - Server Welcome Popup
    
    Displays on player join with:
    - Server info and rules
    - Car restriction warning (if applicable)
    - Available cars list
    - Driver level info
]]

-- API URL
local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP()
local steamId = ac.getUserSteamID()

-- State
local state = {
    show = false,
    loading = true,
    data = nil,
    showTime = 0,
    canDismiss = false,
    dismissed = false,
    scrollPosition = 0
}

-- Colors
local colors = {
    background = rgbm(0.08, 0.08, 0.1, 0.97),
    header = rgbm(0.12, 0.12, 0.15, 1),
    accent = rgbm(0, 0.8, 1, 1),
    gold = rgbm(1, 0.84, 0, 1),
    warning = rgbm(1, 0.6, 0, 1),
    danger = rgbm(1, 0.3, 0.3, 1),
    success = rgbm(0.3, 1, 0.3, 1),
    white = rgbm(1, 1, 1, 1),
    dimmed = rgbm(0.6, 0.6, 0.6, 1),
    rule = rgbm(0.9, 0.9, 0.9, 1),
    
    -- Car class colors
    classS = rgbm(0.6, 0.2, 0.8, 1),
    classA = rgbm(1, 0.27, 0.27, 1),
    classB = rgbm(1, 0.65, 0, 1),
    classC = rgbm(1, 1, 0, 1),
    classD = rgbm(0, 1, 0, 1),
    classE = rgbm(0, 0.75, 1, 1)
}

-- ============================================================================
-- API FUNCTIONS
-- ============================================================================

function FetchWelcomeData()
    state.loading = true
    
    web.get(baseUrl .. "/sxrwelcome/data/" .. steamId, function(err, response)
        state.loading = false
        if not err and response.body then
            state.data = stringify.parse(response.body)
            if state.data then
                state.show = true
                state.showTime = os.clock()
                ac.log("Welcome data loaded for " .. (state.data.PlayerName or "Unknown"))
            end
        else
            -- Fallback: just get server info
            web.get(baseUrl .. "/sxrwelcome/serverinfo", function(err2, response2)
                if not err2 and response2.body then
                    state.data = stringify.parse(response2.body)
                    state.data.PlayerName = ac.getDriverName(0)
                    state.data.DriverLevel = 1
                    state.show = true
                    state.showTime = os.clock()
                end
            end)
        end
    end)
end

-- ============================================================================
-- HELPER FUNCTIONS
-- ============================================================================

function GetClassColor(carClass)
    if carClass == "S" then return colors.classS
    elseif carClass == "A" then return colors.classA
    elseif carClass == "B" then return colors.classB
    elseif carClass == "C" then return colors.classC
    elseif carClass == "D" then return colors.classD
    elseif carClass == "E" then return colors.classE
    else return colors.dimmed
    end
end

function FormatCarName(model)
    if not model then return "Unknown" end
    -- Remove common prefixes and format
    local name = model:gsub("^ks_", ""):gsub("^traffic_", ""):gsub("_", " ")
    -- Capitalize first letters
    name = name:gsub("(%a)([%w_']*)", function(first, rest)
        return first:upper() .. rest
    end)
    return name
end

-- ============================================================================
-- DRAW FUNCTIONS
-- ============================================================================

function DrawWelcomePopup()
    if not state.show or state.dismissed or not state.data then return end
    
    local data = state.data
    local uiState = ac.getUI()
    
    -- Calculate panel size
    local panelWidth = 500
    local panelHeight = data.HasRestriction and 650 or 500
    
    local panelPos = vec2(
        uiState.windowSize.x / 2 - panelWidth / 2,
        uiState.windowSize.y / 2 - panelHeight / 2
    )
    
    -- Check if can dismiss (minimum display time)
    local elapsed = os.clock() - state.showTime
    local minTime = data.MinimumDisplaySeconds or 3
    state.canDismiss = elapsed >= minTime
    
    -- Auto dismiss check
    if data.AutoDismissSeconds and data.AutoDismissSeconds > 0 then
        if elapsed >= data.AutoDismissSeconds then
            state.dismissed = true
            return
        end
    end
    
    ui.toolWindow('sxrWelcome', panelPos, vec2(panelWidth, panelHeight), true, function()
        -- ====== HEADER ======
        ui.pushFont(ui.Font.Title)
        ui.textColored(data.ServerName or "SXR Server", colors.accent)
        ui.popFont()
        
        if data.ServerDescription then
            ui.textColored(data.ServerDescription, colors.dimmed)
        end
        
        ui.spacing()
        ui.separator()
        ui.spacing()
        
        -- ====== WELCOME MESSAGE ======
        if data.WelcomeMessage then
            ui.textWrapped(data.WelcomeMessage)
            ui.spacing()
        end
        
        -- ====== DRIVER LEVEL INFO ======
        ui.pushFont(ui.Font.Main)
        ui.text("Welcome, ")
        ui.sameLine(0, 0)
        ui.textColored(data.PlayerName or "Driver", colors.gold)
        ui.sameLine(0, 0)
        ui.text("!")
        
        -- Format driver level with prestige
        local prestigeRank = data.PrestigeRank or 0
        local driverLevel = data.DriverLevel or 1
        local levelDisplay = ""
        local levelColor = colors.white
        
        if prestigeRank > 0 then
            levelDisplay = string.format("P%d - %d", prestigeRank, driverLevel)
            -- Prestige color (cycle through colors)
            if prestigeRank == 1 then levelColor = colors.gold
            elseif prestigeRank == 2 then levelColor = rgbm(1, 0.42, 0.42, 1)
            elseif prestigeRank == 3 then levelColor = rgbm(0.6, 0.2, 0.8, 1)
            elseif prestigeRank >= 50 then
                -- Rainbow gradient for P50+ (Mythic tier)
                local hue = (os.clock() * 0.5) % 1.0
                local i = math.floor(hue * 6)
                local f = hue * 6 - i
                local q = 1 - f
                i = i % 6
                if i == 0 then levelColor = rgbm(1, f, 0, 1)
                elseif i == 1 then levelColor = rgbm(q, 1, 0, 1)
                elseif i == 2 then levelColor = rgbm(0, 1, f, 1)
                elseif i == 3 then levelColor = rgbm(0, q, 1, 1)
                elseif i == 4 then levelColor = rgbm(f, 0, 1, 1)
                else levelColor = rgbm(1, 0, q, 1)
                end
            elseif prestigeRank >= 20 then levelColor = rgbm(0, 1, 1, 1) -- Aqua
            elseif prestigeRank >= 10 then levelColor = rgbm(1, 0, 1, 1) -- Magenta
            elseif prestigeRank >= 5 then levelColor = rgbm(0.18, 0.8, 0.44, 1) -- Emerald
            else levelColor = colors.gold
            end
        else
            levelDisplay = tostring(driverLevel)
            levelColor = colors.white -- White for non-prestiged
        end
        
        ui.text("Driver Level: ")
        ui.sameLine(0, 0)
        ui.textColored(levelDisplay, levelColor)
        
        if data.DriverXp and data.XpToNextLevel then
            ui.sameLine(180)
            ui.textColored(string.format("XP: %d / %d", data.DriverXp, data.XpToNextLevel), colors.dimmed)
        end
        ui.popFont()
        
        ui.spacing()
        ui.separator()
        ui.spacing()
        
        -- ====== CAR RESTRICTION WARNING ======
        if data.HasRestriction then
            -- Warning box
            ui.drawRectFilled(
                ui.getCursor(),
                ui.getCursor() + vec2(ui.availableSpaceX(), 80),
                rgbm(0.4, 0.15, 0.1, 0.8),
                5
            )
            ui.setCursorX(ui.getCursorX() + 10)
            ui.setCursorY(ui.getCursorY() + 10)
            
            ui.pushFont(ui.Font.Main)
            ui.textColored("⚠️ CAR RESTRICTION", colors.danger)
            ui.popFont()
            
            ui.setCursorX(ui.getCursorX() + 10)
            ui.textWrapped(data.RestrictionWarning or "Your car requires a higher driver level.")
            
            ui.setCursorY(ui.getCursorY() + 15)
            
            -- Enforcement info
            if data.EnforcementMode and data.EnforcementMode ~= "Warning" then
                local remaining = math.max(0, (data.GracePeriodSeconds or 10) - elapsed)
                ui.textColored(string.format("Action in: %.0f seconds (%s)", remaining, data.EnforcementMode), colors.warning)
                ui.spacing()
            end
            
            ui.separator()
            ui.spacing()
            
            -- Available cars section
            if data.AvailableCars and #data.AvailableCars > 0 then
                ui.textColored("Cars you CAN drive:", colors.success)
                ui.spacing()
                
                ui.childWindow('availableCars', vec2(0, 120), true, ui.WindowFlags.None, function()
                    for i, car in ipairs(data.AvailableCars) do
                        local classColor = GetClassColor(car.CarClass)
                        
                        ui.textColored("[" .. car.CarClass .. "]", classColor)
                        ui.sameLine(40)
                        ui.text(FormatCarName(car.Model))
                        ui.sameLine(280)
                        ui.textColored(string.format("Lvl %d+", car.RequiredLevel), colors.dimmed)
                    end
                end)
                
                ui.spacing()
            end
            
            ui.separator()
            ui.spacing()
        end
        
        -- ====== SERVER RULES ======
        ui.textColored("Server Rules", colors.accent)
        ui.spacing()
        
        local rulesHeight = data.HasRestriction and 100 or 180
        ui.childWindow('rules', vec2(0, rulesHeight), true, ui.WindowFlags.None, function()
            if data.Rules then
                for i, rule in ipairs(data.Rules) do
                    ui.textColored(string.format("%d.", i), colors.accent)
                    ui.sameLine(25)
                    ui.textWrapped(rule)
                    ui.spacing()
                end
            end
        end)
        
        ui.spacing()
        
        -- ====== SOCIAL LINKS ======
        if data.DiscordUrl or data.WebsiteUrl then
            ui.separator()
            ui.spacing()
            
            if data.DiscordUrl and data.DiscordUrl ~= "" then
                ui.textColored("Discord: ", colors.dimmed)
                ui.sameLine(0, 0)
                ui.textColored(data.DiscordUrl, colors.accent)
            end
            
            if data.WebsiteUrl and data.WebsiteUrl ~= "" then
                ui.textColored("Website: ", colors.dimmed)
                ui.sameLine(0, 0)
                ui.textColored(data.WebsiteUrl, colors.accent)
            end
            
            ui.spacing()
        end
        
        -- ====== DISMISS BUTTON ======
        ui.separator()
        ui.spacing()
        
        local buttonWidth = 200
        local buttonX = (panelWidth - buttonWidth) / 2 - 10
        ui.setCursorX(buttonX)
        
        if state.canDismiss then
            ui.pushStyleColor(ui.StyleColor.Button, colors.accent)
            if ui.button("I Understand - Enter Server", vec2(buttonWidth, 35)) then
                state.dismissed = true
            end
            ui.popStyleColor()
        else
            local remaining = math.ceil(minTime - elapsed)
            ui.pushStyleColor(ui.StyleColor.Button, colors.dimmed)
            ui.button(string.format("Please wait... (%ds)", remaining), vec2(buttonWidth, 35))
            ui.popStyleColor()
        end
        
        -- Class requirements hint
        ui.spacing()
        ui.setCursorX(10)
        ui.textColored("Tip: Earn XP by driving to unlock higher class cars!", colors.dimmed)
    end)
end

-- ============================================================================
-- MAIN RENDER
-- ============================================================================

function script.drawUI()
    DrawWelcomePopup()
end

-- ============================================================================
-- INITIALIZATION
-- ============================================================================

-- Delay initial fetch to let game load
setTimeout(function()
    FetchWelcomeData()
end, 2000)

ac.debug("SXR Welcome", "Loaded")
