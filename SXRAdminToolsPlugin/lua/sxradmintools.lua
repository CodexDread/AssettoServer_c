--[[
    SXR Admin Tools - In-Game Admin Panel
    
    Features:
    - Player list with quick actions (kick, ban, pit, lights, restrict)
    - Ban management
    - Server environment control (time, weather)
    - Whitelist management
    - Audit log viewer
    - Hotkey support
]]

local config = ac.configValues({
    hotkey = 0x79, -- F10
    refreshInterval = 5
})

-- API URLs
local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/admin"
local steamId = ac.getUserSteamID()

-- State
local state = {
    isAdmin = false,
    adminLevel = 0,
    panelOpen = false,
    currentTab = 1,
    loading = false,
    lastRefresh = 0,
    players = {},
    bans = {},
    audit = {},
    whitelist = {},
    status = nil,
    environment = nil,
    cspWeatherTypes = {},
    selectedPlayer = nil,
    kickReason = "",
    banReason = "",
    banDuration = 24,
    -- Time/Weather controls
    timeHour = 12,
    timeMinute = 0,
    selectedWeatherId = 0,
    selectedCspWeather = "Clear",
    weatherTransition = 30,
    -- Restriction controls
    restrictorValue = 0,
    ballastValue = 0,
    -- Whitelist
    whitelistSteamId = ""
}

local tabNames = { "Players", "Server", "Bans", "Whitelist", "Audit" }

local colors = {
    admin = rgbm(1, 0.84, 0, 1),      -- Gold for admins
    moderator = rgbm(0.5, 0.8, 1, 1), -- Light blue for mods
    online = rgbm(0.2, 0.8, 0.2, 1),  -- Green
    offline = rgbm(0.5, 0.5, 0.5, 1), -- Gray
    danger = rgbm(0.9, 0.2, 0.2, 1),  -- Red
    warning = rgbm(0.9, 0.7, 0.2, 1), -- Orange
    accent = rgbm(0, 0.8, 1, 1),
    bg = rgbm(0.1, 0.1, 0.12, 0.95),
    bgLight = rgbm(0.15, 0.15, 0.18, 1)
}

-- ============================================================================
-- API FUNCTIONS
-- ============================================================================

function CheckAdminStatus()
    web.get(baseUrl .. "/status", function(err, response)
        if err then
            state.isAdmin = false
            return
        end
        
        local data = stringify.parse(response.body)
        if data then
            state.status = data
            -- Check if we're in the connected admins
            for _, admin in ipairs(data.ConnectedAdmins or {}) do
                if admin.SteamId == steamId then
                    state.isAdmin = true
                    state.adminLevel = admin.Level
                    return
                end
            end
        end
        state.isAdmin = false
    end)
end

function FetchPlayers()
    if not state.isAdmin then return end
    state.loading = true
    
    web.get(baseUrl .. "/players", function(err, response)
        state.loading = false
        if not err and response.body then
            state.players = stringify.parse(response.body) or {}
        end
        state.lastRefresh = os.clock()
    end)
end

function FetchBans()
    if not state.isAdmin then return end
    
    web.get(baseUrl .. "/bans?activeOnly=true", function(err, response)
        if not err and response.body then
            state.bans = stringify.parse(response.body) or {}
        end
    end)
end

function FetchAudit()
    if not state.isAdmin then return end
    
    web.get(baseUrl .. "/audit?count=20", function(err, response)
        if not err and response.body then
            state.audit = stringify.parse(response.body) or {}
        end
    end)
end

function FetchStatus()
    if not state.isAdmin then return end
    
    web.get(baseUrl .. "/status", function(err, response)
        if not err and response.body then
            state.status = stringify.parse(response.body)
        end
    end)
end

function FetchEnvironment()
    if not state.isAdmin then return end
    
    web.get(baseUrl .. "/environment", function(err, response)
        if not err and response.body then
            state.environment = stringify.parse(response.body)
            if state.environment then
                state.timeHour = state.environment.TimeHour or 12
                state.timeMinute = state.environment.TimeMinute or 0
            end
        end
    end)
end

function FetchCspWeatherTypes()
    if not state.isAdmin then return end
    
    web.get(baseUrl .. "/weather/types", function(err, response)
        if not err and response.body then
            state.cspWeatherTypes = stringify.parse(response.body) or {}
        end
    end)
end

function FetchWhitelist()
    if not state.isAdmin then return end
    
    web.get(baseUrl .. "/whitelist", function(err, response)
        if not err and response.body then
            state.whitelist = stringify.parse(response.body) or {}
        end
    end)
end

function KickPlayer(sessionId, reason)
    local body = stringify({
        TargetSessionId = sessionId,
        Reason = reason,
        AdminSteamId = steamId
    })
    
    web.post(baseUrl .. "/kick", body, "application/json", function(err, response)
        if not err then
            local result = stringify.parse(response.body)
            if result then
                ac.log("Kick result: " .. (result.Message or "Unknown"))
            end
            FetchPlayers()
        end
    end)
end

function BanPlayer(targetSteamId, targetName, reason, hours)
    local body = stringify({
        TargetSteamId = targetSteamId,
        TargetName = targetName,
        Reason = reason,
        DurationHours = hours,
        AdminSteamId = steamId
    })
    
    web.post(baseUrl .. "/ban", body, "application/json", function(err, response)
        if not err then
            local result = stringify.parse(response.body)
            if result then
                ac.log("Ban result: " .. (result.Message or "Unknown"))
            end
            FetchPlayers()
            FetchBans()
        end
    end)
end

function UnbanPlayer(banId)
    web.request(baseUrl .. "/bans/" .. banId .. "?adminSteamId=" .. steamId, {
        method = "DELETE"
    }, function(err, response)
        if not err then
            FetchBans()
        end
    end)
end

function TeleportToPits(sessionId)
    local body = stringify({
        TargetSessionId = sessionId,
        AdminSteamId = steamId
    })
    
    web.post(baseUrl .. "/pit", body, "application/json", function(err, response)
        if not err then
            local result = stringify.parse(response.body)
            if result then
                ac.log("Pit result: " .. (result.Message or "Unknown"))
            end
        end
    end)
end

function ForceLights(sessionId, forceOn)
    local body = stringify({
        TargetSessionId = sessionId,
        ForceOn = forceOn,
        AdminSteamId = steamId
    })
    
    web.post(baseUrl .. "/forcelights", body, "application/json", function(err, response)
        if not err then
            local result = stringify.parse(response.body)
            if result then
                ac.log("Force lights result: " .. (result.Message or "Unknown"))
            end
        end
    end)
end

function SetRestriction(sessionId, restrictor, ballast)
    local body = stringify({
        TargetSessionId = sessionId,
        Restrictor = restrictor,
        BallastKg = ballast,
        AdminSteamId = steamId
    })
    
    web.post(baseUrl .. "/restrict", body, "application/json", function(err, response)
        if not err then
            local result = stringify.parse(response.body)
            if result then
                ac.log("Restriction result: " .. (result.Message or "Unknown"))
            end
        end
    end)
end

function SetServerTime(hour, minute)
    local body = stringify({
        Hour = hour,
        Minute = minute,
        AdminSteamId = steamId
    })
    
    web.post(baseUrl .. "/time", body, "application/json", function(err, response)
        if not err then
            local result = stringify.parse(response.body)
            if result then
                ac.log("Set time result: " .. (result.Message or "Unknown"))
            end
            FetchEnvironment()
        end
    end)
end

function SetWeatherConfig(weatherId)
    local body = stringify({
        WeatherConfigId = weatherId,
        AdminSteamId = steamId
    })
    
    web.post(baseUrl .. "/weather", body, "application/json", function(err, response)
        if not err then
            local result = stringify.parse(response.body)
            if result then
                ac.log("Set weather result: " .. (result.Message or "Unknown"))
            end
            FetchEnvironment()
        end
    end)
end

function SetCspWeather(weatherType, transitionSec)
    local body = stringify({
        WeatherType = weatherType,
        TransitionDuration = transitionSec,
        AdminSteamId = steamId
    })
    
    web.post(baseUrl .. "/weather", body, "application/json", function(err, response)
        if not err then
            local result = stringify.parse(response.body)
            if result then
                ac.log("Set CSP weather result: " .. (result.Message or "Unknown"))
            end
            FetchEnvironment()
        end
    end)
end

function AddToWhitelist(targetSteamId)
    web.post(baseUrl .. "/whitelist?steamId=" .. targetSteamId .. "&adminSteamId=" .. steamId, "", "application/json", function(err, response)
        if not err then
            local result = stringify.parse(response.body)
            if result then
                ac.log("Whitelist result: " .. (result.Message or "Unknown"))
            end
            FetchWhitelist()
        end
    end)
end

function RemoveFromWhitelist(targetSteamId)
    web.request(baseUrl .. "/whitelist/" .. targetSteamId .. "?adminSteamId=" .. steamId, {
        method = "DELETE"
    }, function(err, response)
        if not err then
            FetchWhitelist()
        end
    end)
end

-- ============================================================================
-- UI DRAWING
-- ============================================================================

function DrawPlayerRow(player, index)
    local isSelected = state.selectedPlayer == player.SessionId
    
    -- Row background
    if isSelected then
        ui.pushStyleColor(ui.StyleColor.Button, colors.accent)
    elseif index % 2 == 0 then
        ui.pushStyleColor(ui.StyleColor.Button, colors.bgLight)
    else
        ui.pushStyleColor(ui.StyleColor.Button, colors.bg)
    end
    
    -- Selectable row
    if ui.button("##player" .. player.SessionId, vec2(ui.availableSpaceX(), 25)) then
        state.selectedPlayer = isSelected and nil or player.SessionId
    end
    ui.popStyleColor()
    
    -- Draw content on top
    ui.sameLine(10)
    
    -- Admin indicator
    local nameColor = rgbm.colors.white
    if player.AdminLevel == 3 then
        nameColor = colors.admin
    elseif player.AdminLevel == 2 then
        nameColor = colors.admin
    elseif player.AdminLevel == 1 then
        nameColor = colors.moderator
    end
    
    ui.textColored(string.format("#%d", player.SessionId), colors.accent)
    ui.sameLine(50)
    ui.textColored(player.Name:sub(1, 20), nameColor)
    ui.sameLine(200)
    ui.textColored(player.CarModel:sub(1, 15), colors.offline)
    ui.sameLine(320)
    ui.textColored(string.format("%dms", player.Ping), 
        player.Ping > 150 and colors.warning or colors.online)
    ui.sameLine(380)
    ui.textColored(string.format("%.0f km/h", player.SpeedKph), colors.accent)
    
    -- AFK indicator
    if player.IsAfk then
        ui.sameLine(450)
        ui.textColored("[AFK]", colors.warning)
    end
end

function DrawPlayersTab()
    -- Header
    ui.columns(1)
    ui.textColored("ID", colors.offline)
    ui.sameLine(50)
    ui.textColored("Name", colors.offline)
    ui.sameLine(200)
    ui.textColored("Car", colors.offline)
    ui.sameLine(320)
    ui.textColored("Ping", colors.offline)
    ui.sameLine(380)
    ui.textColored("Speed", colors.offline)
    ui.separator()
    
    -- Player list
    ui.childWindow('playerList', vec2(0, 200), true, ui.WindowFlags.None, function()
        for i, player in ipairs(state.players) do
            DrawPlayerRow(player, i)
        end
    end)
    
    ui.separator()
    
    -- Selected player actions
    if state.selectedPlayer then
        local player = nil
        for _, p in ipairs(state.players) do
            if p.SessionId == state.selectedPlayer then
                player = p
                break
            end
        end
        
        if player then
            ui.textColored("Selected: " .. player.Name, colors.accent)
            ui.text("Steam: " .. player.SteamId)
            ui.text("IP: " .. (player.IpAddress or "Unknown"))
            ui.spacing()
            
            -- Quick action buttons row 1
            if ui.button("Teleport to Pits") then
                TeleportToPits(player.SessionId)
            end
            ui.sameLine()
            if ui.button("Lights ON") then
                ForceLights(player.SessionId, true)
            end
            ui.sameLine()
            if ui.button("Lights OFF") then
                ForceLights(player.SessionId, false)
            end
            ui.sameLine()
            if ui.button("Whitelist") then
                AddToWhitelist(player.SteamId)
            end
            
            ui.spacing()
            
            -- Kick section
            ui.text("Reason:")
            ui.sameLine(70)
            ui.setNextItemWidth(250)
            state.kickReason = ui.inputText("##kickreason", state.kickReason, ui.InputTextFlags.None)
            ui.sameLine()
            
            if ui.button("Kick") then
                KickPlayer(player.SessionId, state.kickReason)
                state.kickReason = ""
            end
            
            -- Ban section (if Admin+)
            if state.adminLevel >= 2 then
                ui.sameLine()
                ui.setNextItemWidth(60)
                state.banDuration = ui.slider("##bandur", state.banDuration, 1, 720, "%.0fh")
                ui.sameLine()
                
                ui.pushStyleColor(ui.StyleColor.Button, colors.danger)
                if ui.button("Temp Ban") then
                    BanPlayer(player.SteamId, player.Name, state.kickReason, state.banDuration)
                    state.kickReason = ""
                end
                ui.popStyleColor()
                
                ui.sameLine()
                ui.pushStyleColor(ui.StyleColor.Button, rgbm(0.5, 0, 0, 1))
                if ui.button("Perma Ban") then
                    BanPlayer(player.SteamId, player.Name, state.kickReason, 0)
                    state.kickReason = ""
                end
                ui.popStyleColor()
            end
            
            -- Restrictions section (Admin+)
            if state.adminLevel >= 2 then
                ui.spacing()
                ui.separator()
                ui.textColored("Restrictions", colors.warning)
                
                ui.text("Restrictor:")
                ui.sameLine(80)
                ui.setNextItemWidth(100)
                state.restrictorValue = ui.slider("##restrictor", state.restrictorValue, 0, 400, "%.0f")
                
                ui.sameLine()
                ui.text("Ballast:")
                ui.sameLine(240)
                ui.setNextItemWidth(100)
                state.ballastValue = ui.slider("##ballast", state.ballastValue, 0, 200, "%.0f kg")
                
                ui.sameLine()
                if ui.button("Apply") then
                    SetRestriction(player.SessionId, state.restrictorValue, state.ballastValue)
                end
                ui.sameLine()
                if ui.button("Clear") then
                    state.restrictorValue = 0
                    state.ballastValue = 0
                    SetRestriction(player.SessionId, 0, 0)
                end
            end
        end
    else
        ui.textColored("Click a player to select", colors.offline)
    end
end

function DrawServerTab()
    if state.adminLevel < 2 then
        ui.textColored("Requires Admin level", colors.danger)
        return
    end
    
    -- Server Status
    if state.status then
        ui.columns(2)
        ui.text("Players Online:")
        ui.nextColumn()
        ui.textColored(tostring(state.status.PlayersOnline), colors.accent)
        ui.nextColumn()
        ui.text("Admins Online:")
        ui.nextColumn()
        ui.textColored(tostring(state.status.AdminsOnline), colors.admin)
        ui.columns()
    end
    
    ui.spacing()
    ui.separator()
    ui.spacing()
    
    -- Time Control
    ui.pushFont(ui.Font.Title)
    ui.textColored("Time Control", colors.accent)
    ui.popFont()
    ui.spacing()
    
    ui.text("Hour:")
    ui.sameLine(60)
    ui.setNextItemWidth(150)
    state.timeHour = math.floor(ui.slider("##hour", state.timeHour, 0, 23, "%.0f"))
    
    ui.sameLine()
    ui.text("Minute:")
    ui.sameLine(270)
    ui.setNextItemWidth(150)
    state.timeMinute = math.floor(ui.slider("##minute", state.timeMinute, 0, 59, "%.0f"))
    
    ui.sameLine()
    if ui.button("Set Time") then
        SetServerTime(state.timeHour, state.timeMinute)
    end
    
    -- Quick time presets
    ui.text("Presets:")
    ui.sameLine(60)
    if ui.button("Dawn") then SetServerTime(6, 0) end
    ui.sameLine()
    if ui.button("Morning") then SetServerTime(9, 0) end
    ui.sameLine()
    if ui.button("Noon") then SetServerTime(12, 0) end
    ui.sameLine()
    if ui.button("Afternoon") then SetServerTime(15, 0) end
    ui.sameLine()
    if ui.button("Sunset") then SetServerTime(18, 30) end
    ui.sameLine()
    if ui.button("Night") then SetServerTime(22, 0) end
    
    ui.spacing()
    ui.separator()
    ui.spacing()
    
    -- Weather Control
    ui.pushFont(ui.Font.Title)
    ui.textColored("Weather Control", colors.accent)
    ui.popFont()
    ui.spacing()
    
    -- Weather config ID
    ui.text("Config ID:")
    ui.sameLine(80)
    ui.setNextItemWidth(80)
    state.selectedWeatherId = math.floor(ui.slider("##weatherid", state.selectedWeatherId, 0, 10, "%.0f"))
    ui.sameLine()
    if ui.button("Set Weather Config") then
        SetWeatherConfig(state.selectedWeatherId)
    end
    
    ui.spacing()
    
    -- CSP Weather Types
    ui.text("CSP Weather:")
    ui.sameLine(80)
    ui.setNextItemWidth(150)
    
    -- Common weather type buttons
    local weatherTypes = {"Clear", "FewClouds", "ScatteredClouds", "BrokenClouds", "OvercastClouds", 
                          "Fog", "Mist", "Rain", "HeavyRain", "Thunderstorm", "Snow", "HeavySnow"}
    
    for i, wType in ipairs(weatherTypes) do
        if i > 1 and (i - 1) % 6 ~= 0 then ui.sameLine() end
        if i == 1 or (i - 1) % 6 == 0 then
            if i > 1 then ui.dummy(vec2(80, 0)) end
        end
        
        local isRain = wType:find("Rain") or wType:find("Thunder")
        local isSnow = wType:find("Snow")
        local isClear = wType == "Clear" or wType:find("FewClouds")
        
        local btnColor = colors.bgLight
        if isRain then btnColor = rgbm(0.2, 0.3, 0.5, 1)
        elseif isSnow then btnColor = rgbm(0.7, 0.7, 0.8, 1)
        elseif isClear then btnColor = rgbm(0.3, 0.5, 0.7, 1)
        end
        
        ui.pushStyleColor(ui.StyleColor.Button, btnColor)
        if ui.button(wType) then
            SetCspWeather(wType, state.weatherTransition)
        end
        ui.popStyleColor()
    end
    
    ui.spacing()
    ui.text("Transition:")
    ui.sameLine(80)
    ui.setNextItemWidth(150)
    state.weatherTransition = ui.slider("##transition", state.weatherTransition, 1, 300, "%.0f sec")
    
    -- Current environment info
    if state.environment then
        ui.spacing()
        ui.separator()
        ui.spacing()
        ui.textColored("Current Environment", colors.offline)
        ui.text(string.format("Time: %02d:%02d", state.environment.TimeHour or 0, state.environment.TimeMinute or 0))
        ui.sameLine(150)
        ui.text(string.format("Weather: %s", state.environment.WeatherType or "Unknown"))
        ui.text(string.format("Ambient: %.1f°C", state.environment.AmbientTemp or 20))
        ui.sameLine(150)
        ui.text(string.format("Road: %.1f°C", state.environment.RoadTemp or 25))
    end
end

function DrawBansTab()
    ui.text("Active Bans: " .. #state.bans)
    ui.separator()
    
    ui.childWindow('banList', vec2(0, 350), true, ui.WindowFlags.None, function()
        for i, ban in ipairs(state.bans) do
            ui.pushID(i)
            
            ui.textColored(ban.PlayerName, colors.accent)
            ui.sameLine(150)
            ui.textColored(ban.Id, colors.offline)
            ui.sameLine(270)
            
            local expiryColor = ban.IsPermanent and colors.danger or colors.warning
            ui.textColored(ban.IsPermanent and "Permanent" or "Temp", expiryColor)
            
            ui.sameLine(350)
            if ui.button("Unban##" .. ban.Id) then
                UnbanPlayer(ban.Id)
            end
            
            -- Second row - details
            ui.textColored("Reason: " .. ban.Reason:sub(1, 50), colors.offline)
            ui.textColored("By: " .. ban.BannedByName, colors.offline)
            
            ui.separator()
            ui.popID()
        end
    end)
end

function DrawWhitelistTab()
    if state.adminLevel < 2 then
        ui.textColored("Requires Admin level", colors.danger)
        return
    end
    
    ui.text("Whitelist Entries: " .. #state.whitelist)
    
    -- Add to whitelist
    ui.spacing()
    ui.text("Add Steam ID:")
    ui.sameLine(100)
    ui.setNextItemWidth(200)
    state.whitelistSteamId = ui.inputText("##wlsteamid", state.whitelistSteamId, ui.InputTextFlags.None)
    ui.sameLine()
    if ui.button("Add to Whitelist") then
        if state.whitelistSteamId ~= "" then
            AddToWhitelist(state.whitelistSteamId)
            state.whitelistSteamId = ""
        end
    end
    
    ui.separator()
    
    ui.childWindow('whitelistList', vec2(0, 300), true, ui.WindowFlags.None, function()
        for i, entry in ipairs(state.whitelist) do
            ui.pushID(i)
            
            ui.textColored(entry.Name or "Unknown", colors.accent)
            ui.sameLine(150)
            ui.text(entry.SteamId)
            ui.sameLine(350)
            
            ui.pushStyleColor(ui.StyleColor.Button, colors.danger)
            if ui.button("Remove##" .. entry.SteamId) then
                RemoveFromWhitelist(entry.SteamId)
            end
            ui.popStyleColor()
            
            ui.textColored("Added by: " .. (entry.AddedBy or "Unknown"), colors.offline)
            
            ui.separator()
            ui.popID()
        end
    end)
end

function DrawAuditTab()
    ui.text("Recent Admin Actions")
    if ui.button("Refresh") then
        FetchAudit()
    end
    ui.separator()
    
    ui.childWindow('auditList', vec2(0, 350), true, ui.WindowFlags.None, function()
        for _, entry in ipairs(state.audit) do
            -- Format timestamp
            local time = entry.Timestamp:sub(12, 19) or "??:??:??"
            
            ui.textColored(time, colors.offline)
            ui.sameLine(70)
            ui.textColored(entry.AdminName, colors.admin)
            ui.sameLine(180)
            
            local actionColor = colors.accent
            if entry.Action == "Kick" then actionColor = colors.warning
            elseif entry.Action == "Ban" or entry.Action == "TempBan" then actionColor = colors.danger
            elseif entry.Action == "Unban" then actionColor = colors.online
            elseif entry.Action == "Teleport" then actionColor = rgbm(0.5, 0.8, 1, 1)
            elseif entry.Action == "ConfigChange" then actionColor = rgbm(0.8, 0.5, 1, 1)
            end
            
            ui.textColored(entry.Action, actionColor)
            
            if entry.TargetName and entry.TargetName ~= "" then
                ui.sameLine(280)
                ui.text("-> " .. entry.TargetName)
            end
            
            if entry.Details and entry.Details ~= "" then
                ui.textColored("  " .. entry.Details:sub(1, 60), colors.offline)
            end
        end
    end)
end

-- ============================================================================
-- MAIN PANEL
-- ============================================================================

function DrawAdminPanel()
    if not state.panelOpen or not state.isAdmin then return end
    
    -- Refresh data periodically
    if os.clock() - state.lastRefresh > config.refreshInterval then
        FetchPlayers()
        FetchBans()
        FetchAudit()
        FetchStatus()
        FetchEnvironment()
        FetchWhitelist()
    end
    
    local uiState = ac.getUI()
    local panelSize = vec2(600, 550)
    local panelPos = vec2(
        uiState.windowSize.x / 2 - panelSize.x / 2,
        uiState.windowSize.y / 2 - panelSize.y / 2
    )
    
    ui.toolWindow('adminPanel', panelPos, panelSize, true, function()
        -- Title bar
        ui.pushFont(ui.Font.Title)
        ui.textColored("Admin Panel", colors.admin)
        ui.popFont()
        
        local levelName = "Unknown"
        if state.adminLevel == 3 then levelName = "SuperAdmin"
        elseif state.adminLevel == 2 then levelName = "Admin"
        elseif state.adminLevel == 1 then levelName = "Moderator"
        end
        ui.sameLine(ui.availableSpaceX() - 120)
        ui.textColored("[" .. levelName .. "]", colors.accent)
        
        ui.sameLine(ui.availableSpaceX() - 30)
        if ui.button("X") then
            state.panelOpen = false
        end
        
        ui.separator()
        
        -- Tab bar
        for i, name in ipairs(tabNames) do
            if i > 1 then ui.sameLine() end
            
            local isActive = state.currentTab == i
            if isActive then
                ui.pushStyleColor(ui.StyleColor.Button, colors.accent)
            end
            
            if ui.button(name) then
                state.currentTab = i
                if i == 1 then FetchPlayers()
                elseif i == 2 then FetchEnvironment()
                elseif i == 3 then FetchBans()
                elseif i == 4 then FetchWhitelist()
                elseif i == 5 then FetchAudit()
                end
            end
            
            if isActive then
                ui.popStyleColor()
            end
        end
        
        ui.separator()
        ui.spacing()
        
        -- Tab content
        if state.currentTab == 1 then
            DrawPlayersTab()
        elseif state.currentTab == 2 then
            DrawServerTab()
        elseif state.currentTab == 3 then
            DrawBansTab()
        elseif state.currentTab == 4 then
            DrawWhitelistTab()
        elseif state.currentTab == 5 then
            DrawAuditTab()
        end
        
        -- Footer
        ui.spacing()
        if state.loading then
            ui.textColored("Loading...", colors.offline)
        end
    end)
end

-- ============================================================================
-- INPUT HANDLING
-- ============================================================================

function script.update(dt)
    -- Check hotkey
    if ac.isKeyPressed(config.hotkey) then
        if state.isAdmin then
            state.panelOpen = not state.panelOpen
            if state.panelOpen then
                FetchPlayers()
                FetchBans()
                FetchStatus()
                FetchEnvironment()
                FetchWhitelist()
            end
        end
    end
end

function script.drawUI()
    DrawAdminPanel()
end

-- ============================================================================
-- INITIALIZATION
-- ============================================================================

-- Check admin status on load
CheckAdminStatus()

-- Register in online extras if admin
setTimeout(function()
    if state.isAdmin then
        ui.registerOnlineExtra(ui.Icons.Settings, "Admin Panel", function() return state.isAdmin end, function()
            state.panelOpen = true
            FetchPlayers()
            FetchBans()
            FetchStatus()
            FetchEnvironment()
            FetchWhitelist()
            FetchCspWeatherTypes()
            return false -- Keep panel open
        end)
    end
end, 2000)

ac.debug("Admin Tools UI", "Loaded")
ac.debug("Steam ID", steamId)
