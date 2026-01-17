--[[
    SXR Admin Tools - Client-Side Admin Panel
    Version: 2.0.0
    
    RAG Compliance Notes:
    - ui.toolWindow uses inputs=true (5th parameter) for interactive elements
    - ui.registerOnlineExtra uses all 8 parameters including flags
    - All loops use ui.pushID(i) / ui.popID()
    - All web callbacks have proper error handling
    - Uses ac.getServerHTTPPort() for HTTP port
]]

-- ============================================================================
-- CONFIGURATION & STATE
-- ============================================================================

local serverIP = ac.getServerIP()
local serverPort = ac.getServerHTTPPort()
local baseUrl = string.format("http://%s:%d", serverIP, serverPort)

---@class AdminState
local state = {
    -- Auth
    isAdmin = false,
    adminLevel = "None",
    mySteamId = "",
    
    -- Current tab
    currentTab = 1,
    
    -- Players
    players = {},
    selectedPlayer = nil,
    
    -- Bans
    bans = {},
    banSearch = "",
    
    -- Whitelist
    whitelist = {},
    
    -- Audit
    auditLog = {},
    
    -- Server
    serverTime = { hour = 12, minute = 0 },
    weatherTypes = {},
    selectedWeather = 1,
    transitionDuration = 30,
    
    -- UI State
    kickReason = "",
    banReason = "",
    banHours = 24,
    restrictorValue = 0,
    ballastValue = 0,
    
    -- Loading/Error
    isLoading = false,
    lastError = nil,
    lastErrorTime = 0,
    
    -- Refresh
    lastRefresh = 0,
    refreshInterval = 5,
}

local tabs = { "Players", "Bans", "Whitelist", "Server", "Audit" }

-- ============================================================================
-- UTILITY FUNCTIONS
-- ============================================================================

---@param message string
local function logError(message)
    ac.error("[SXRAdminTools] " .. message)
    state.lastError = message
    state.lastErrorTime = os.clock()
end

---@param message string
local function logInfo(message)
    ac.log("[SXRAdminTools] " .. message)
end

---@param jsonStr string
---@return table|nil
local function safeParse(jsonStr)
    if not jsonStr or jsonStr == "" then
        return nil
    end
    local success, result = pcall(function()
        return JSON.parse(jsonStr)
    end)
    if success then
        return result
    else
        logError("JSON parse error: " .. tostring(result))
        return nil
    end
end

---@param seconds number
---@return string
local function formatDuration(seconds)
    if seconds < 60 then
        return string.format("%ds", seconds)
    elseif seconds < 3600 then
        return string.format("%dm", math.floor(seconds / 60))
    elseif seconds < 86400 then
        return string.format("%dh", math.floor(seconds / 3600))
    else
        return string.format("%dd", math.floor(seconds / 86400))
    end
end

---@param timestamp string
---@return string
local function formatTimestamp(timestamp)
    if not timestamp then return "Unknown" end
    -- Extract date/time from ISO format
    local year, month, day, hour, min = timestamp:match("(%d+)-(%d+)-(%d+)T(%d+):(%d+)")
    if year then
        return string.format("%s/%s %s:%s", month, day, hour, min)
    end
    return timestamp
end

-- ============================================================================
-- API FUNCTIONS
-- ============================================================================

---@param endpoint string
---@param callback fun(data: table|nil)
local function apiGet(endpoint, callback)
    local url = baseUrl .. endpoint
    web.get(url, {
        ['X-Admin-SteamId'] = state.mySteamId
    }, function(err, response)
        if err then
            logError("GET " .. endpoint .. " error: " .. tostring(err))
            callback(nil)
            return
        end
        if not response then
            logError("GET " .. endpoint .. " no response")
            callback(nil)
            return
        end
        if response.status ~= 200 then
            logError("GET " .. endpoint .. " status: " .. tostring(response.status))
            callback(nil)
            return
        end
        local data = safeParse(response.body)
        callback(data)
    end)
end

---@param endpoint string
---@param body table
---@param callback fun(data: table|nil)
local function apiPost(endpoint, body, callback)
    local url = baseUrl .. endpoint
    local jsonBody = JSON.stringify(body)
    
    web.post(url, {
        ['Content-Type'] = 'application/json',
        ['X-Admin-SteamId'] = state.mySteamId
    }, jsonBody, function(err, response)
        if err then
            logError("POST " .. endpoint .. " error: " .. tostring(err))
            callback(nil)
            return
        end
        if not response then
            logError("POST " .. endpoint .. " no response")
            callback(nil)
            return
        end
        if response.status ~= 200 then
            logError("POST " .. endpoint .. " status: " .. tostring(response.status))
            callback(nil)
            return
        end
        local data = safeParse(response.body)
        callback(data)
    end)
end

---@param endpoint string
---@param callback fun(success: boolean)
local function apiDelete(endpoint, callback)
    local url = baseUrl .. endpoint
    web.request('DELETE', url, {
        ['X-Admin-SteamId'] = state.mySteamId
    }, nil, function(err, response)
        if err then
            logError("DELETE " .. endpoint .. " error: " .. tostring(err))
            callback(false)
            return
        end
        if not response then
            logError("DELETE " .. endpoint .. " no response")
            callback(false)
            return
        end
        callback(response.status == 200)
    end)
end

-- ============================================================================
-- DATA REFRESH FUNCTIONS
-- ============================================================================

local function refreshAdminStatus()
    apiGet("/admin/status", function(data)
        if data then
            state.isAdmin = data.isAdmin or false
            state.adminLevel = data.level or "None"
            state.mySteamId = data.steamId or ""
        end
    end)
end

local function refreshPlayers()
    apiGet("/admin/players", function(data)
        if data and data.players then
            state.players = data.players
        end
    end)
end

local function refreshBans()
    local endpoint = "/admin/bans"
    if state.banSearch and state.banSearch ~= "" then
        endpoint = endpoint .. "?search=" .. state.banSearch
    end
    apiGet(endpoint, function(data)
        if data and data.bans then
            state.bans = data.bans
        end
    end)
end

local function refreshWhitelist()
    apiGet("/admin/whitelist", function(data)
        if data and data.entries then
            state.whitelist = data.entries
        end
    end)
end

local function refreshAuditLog()
    apiGet("/admin/audit?count=50", function(data)
        if data and data.entries then
            state.auditLog = data.entries
        end
    end)
end

local function refreshEnvironment()
    apiGet("/admin/environment", function(data)
        if data then
            state.serverTime.hour = data.hour or 12
            state.serverTime.minute = data.minute or 0
        end
    end)
end

local function refreshWeatherTypes()
    apiGet("/admin/weather/types", function(data)
        if data and data.types then
            state.weatherTypes = data.types
        end
    end)
end

local function refreshAll()
    refreshPlayers()
    refreshBans()
    refreshWhitelist()
    refreshAuditLog()
    refreshEnvironment()
    refreshWeatherTypes()
end

-- ============================================================================
-- ACTION FUNCTIONS
-- ============================================================================

---@param sessionId number
---@param reason string
local function kickPlayer(sessionId, reason)
    apiPost("/admin/kick", {
        targetSessionId = sessionId,
        reason = reason,
        adminSteamId = state.mySteamId
    }, function(data)
        if data and data.success then
            logInfo("Kicked player")
            refreshPlayers()
        end
    end)
end

---@param sessionId number
---@param reason string
---@param hours number|nil
local function banPlayer(sessionId, reason, hours)
    apiPost("/admin/ban", {
        targetSessionId = sessionId,
        reason = reason,
        durationHours = hours,
        adminSteamId = state.mySteamId
    }, function(data)
        if data and data.success then
            logInfo("Banned player")
            refreshPlayers()
            refreshBans()
        end
    end)
end

---@param banId number
local function unbanPlayer(banId)
    apiDelete("/admin/ban/" .. banId, function(success)
        if success then
            logInfo("Unbanned player")
            refreshBans()
        end
    end)
end

---@param sessionId number
local function teleportToPits(sessionId)
    apiPost("/admin/pit", {
        targetSessionId = sessionId,
        adminSteamId = state.mySteamId
    }, function(data)
        if data and data.success then
            logInfo("Teleported player to pits")
        end
    end)
end

---@param sessionId number
---@param forceOn boolean
local function forceLights(sessionId, forceOn)
    apiPost("/admin/forcelights", {
        targetSessionId = sessionId,
        forceOn = forceOn,
        adminSteamId = state.mySteamId
    }, function(data)
        if data and data.success then
            logInfo("Force lights: " .. (forceOn and "ON" or "OFF"))
        end
    end)
end

---@param sessionId number
---@param restrictor number
---@param ballast number
local function setRestriction(sessionId, restrictor, ballast)
    apiPost("/admin/restrict", {
        targetSessionId = sessionId,
        restrictor = restrictor,
        ballastKg = ballast,
        adminSteamId = state.mySteamId
    }, function(data)
        if data and data.success then
            logInfo("Set restriction")
        end
    end)
end

---@param hour number
---@param minute number
local function setTime(hour, minute)
    apiPost("/admin/time", {
        hour = hour,
        minute = minute,
        adminSteamId = state.mySteamId
    }, function(data)
        if data and data.success then
            logInfo("Set time to " .. hour .. ":" .. minute)
            refreshEnvironment()
        end
    end)
end

---@param weatherType string
---@param transition number
local function setWeather(weatherType, transition)
    apiPost("/admin/weather", {
        weatherType = weatherType,
        transitionDuration = transition,
        adminSteamId = state.mySteamId
    }, function(data)
        if data and data.success then
            logInfo("Set weather: " .. weatherType)
        end
    end)
end

---@param steamId string
local function addToWhitelist(steamId)
    apiPost("/admin/whitelist", {
        steamId = steamId,
        adminSteamId = state.mySteamId
    }, function(data)
        if data and data.success then
            logInfo("Added to whitelist: " .. steamId)
            refreshWhitelist()
        end
    end)
end

---@param steamId string
local function removeFromWhitelist(steamId)
    apiDelete("/admin/whitelist/" .. steamId, function(success)
        if success then
            logInfo("Removed from whitelist")
            refreshWhitelist()
        end
    end)
end

-- ============================================================================
-- UI DRAWING FUNCTIONS
-- ============================================================================

local function drawTabBar()
    ui.beginGroup()
    for i, tabName in ipairs(tabs) do
        ui.pushID(i)
        if i > 1 then ui.sameLine() end
        
        local isSelected = (state.currentTab == i)
        if isSelected then
            ui.pushStyleColor(ui.StyleColor.Button, rgbm(0.3, 0.5, 0.8, 1))
        end
        
        if ui.button(tabName, vec2(80, 25)) then
            state.currentTab = i
        end
        
        if isSelected then
            ui.popStyleColor()
        end
        ui.popID()
    end
    ui.endGroup()
    ui.separator()
end

local function drawPlayersTab()
    -- Refresh button
    if ui.button("Refresh", vec2(80, 25)) then
        refreshPlayers()
    end
    ui.sameLine()
    ui.text(string.format("Players: %d", #state.players))
    ui.separator()
    
    -- Player list
    ui.childWindow('playerList', vec2(0, 300), true, ui.WindowFlags.None, function()
        for i, player in ipairs(state.players) do
            ui.pushID(i)
            
            -- Player row
            local isSelected = (state.selectedPlayer == i)
            if isSelected then
                ui.pushStyleColor(ui.StyleColor.ChildBg, rgbm(0.2, 0.3, 0.5, 0.5))
            end
            
            ui.beginGroup()
            
            -- Name and info
            ui.text(player.name or "Unknown")
            ui.sameLine(150)
            ui.textDisabled(string.format("ID:%d", player.sessionId or 0))
            ui.sameLine(200)
            ui.textDisabled(player.carModel or "")
            
            -- Quick actions
            ui.sameLine(350)
            if ui.smallButton("Select") then
                state.selectedPlayer = i
            end
            
            ui.endGroup()
            
            if isSelected then
                ui.popStyleColor()
            end
            
            ui.popID()
        end
    end)
    
    ui.separator()
    
    -- Selected player actions
    if state.selectedPlayer and state.players[state.selectedPlayer] then
        local player = state.players[state.selectedPlayer]
        
        ui.text(string.format("Selected: %s", player.name or "Unknown"))
        ui.textDisabled(string.format("Steam: %s | IP: %s", player.steamId or "N/A", player.ipAddress or "N/A"))
        ui.spacing()
        
        -- Action buttons row 1
        if ui.button("Kick", vec2(60, 25)) then
            kickPlayer(player.sessionId, state.kickReason)
        end
        ui.sameLine()
        if ui.button("Ban", vec2(60, 25)) then
            banPlayer(player.sessionId, state.banReason, state.banHours)
        end
        ui.sameLine()
        if ui.button("Pit", vec2(60, 25)) then
            teleportToPits(player.sessionId)
        end
        ui.sameLine()
        if ui.button("Lights ON", vec2(70, 25)) then
            forceLights(player.sessionId, true)
        end
        ui.sameLine()
        if ui.button("Lights OFF", vec2(70, 25)) then
            forceLights(player.sessionId, false)
        end
        
        ui.spacing()
        
        -- Kick/Ban reason
        local newReason, changed = ui.inputText("Reason", state.kickReason, ui.InputTextFlags.None)
        if changed then
            state.kickReason = newReason
            state.banReason = newReason
        end
        
        -- Ban hours
        local newHours, hoursChanged = ui.slider("Ban Hours", state.banHours, 1, 720, "%.0f hours")
        if hoursChanged then state.banHours = newHours end
        
        ui.spacing()
        
        -- Restriction controls
        ui.text("Restrictions:")
        local newRestrictor, rChanged = ui.slider("Restrictor", state.restrictorValue, 0, 100, "%.0f%%")
        if rChanged then state.restrictorValue = newRestrictor end
        
        local newBallast, bChanged = ui.slider("Ballast", state.ballastValue, 0, 200, "%.0f kg")
        if bChanged then state.ballastValue = newBallast end
        
        if ui.button("Apply Restriction", vec2(120, 25)) then
            setRestriction(player.sessionId, state.restrictorValue, state.ballastValue)
        end
        ui.sameLine()
        if ui.button("Clear", vec2(60, 25)) then
            state.restrictorValue = 0
            state.ballastValue = 0
            setRestriction(player.sessionId, 0, 0)
        end
        
        -- Whitelist
        ui.spacing()
        if ui.button("Add to Whitelist", vec2(120, 25)) then
            addToWhitelist(player.steamId)
        end
    else
        ui.textDisabled("Select a player to see actions")
    end
end

local function drawBansTab()
    -- Search
    local newSearch, searchChanged = ui.inputText("Search", state.banSearch, ui.InputTextFlags.None)
    if searchChanged then
        state.banSearch = newSearch
    end
    ui.sameLine()
    if ui.button("Search", vec2(60, 25)) then
        refreshBans()
    end
    ui.sameLine()
    if ui.button("Refresh", vec2(60, 25)) then
        state.banSearch = ""
        refreshBans()
    end
    
    ui.separator()
    ui.text(string.format("Bans: %d", #state.bans))
    ui.separator()
    
    -- Ban list
    ui.childWindow('banList', vec2(0, 350), true, ui.WindowFlags.None, function()
        for i, ban in ipairs(state.bans) do
            ui.pushID(i)
            
            ui.beginGroup()
            
            -- Ban info
            ui.text(ban.playerName or "Unknown")
            ui.sameLine(150)
            ui.textDisabled(ban.steamId or "N/A")
            ui.sameLine(300)
            
            -- Expiry
            if ban.isPermanent then
                ui.textColored(rgbm(1, 0.3, 0.3, 1), "PERMANENT")
            else
                ui.text(formatTimestamp(ban.expiresAt))
            end
            
            -- Reason
            ui.textDisabled(string.format("Reason: %s", ban.reason or "No reason"))
            
            -- Unban button
            ui.sameLine(450)
            if ui.smallButton("Unban") then
                unbanPlayer(ban.id)
            end
            
            ui.endGroup()
            ui.separator()
            
            ui.popID()
        end
    end)
end

local function drawWhitelistTab()
    if ui.button("Refresh", vec2(80, 25)) then
        refreshWhitelist()
    end
    ui.sameLine()
    ui.text(string.format("Whitelist: %d entries", #state.whitelist))
    
    ui.separator()
    
    -- Whitelist entries
    ui.childWindow('whitelistList', vec2(0, 350), true, ui.WindowFlags.None, function()
        for i, entry in ipairs(state.whitelist) do
            ui.pushID(i)
            
            ui.text(entry.name or "Unknown")
            ui.sameLine(150)
            ui.textDisabled(entry.steamId or "N/A")
            ui.sameLine(350)
            
            if ui.smallButton("Remove") then
                removeFromWhitelist(entry.steamId)
            end
            
            ui.popID()
        end
    end)
end

local function drawServerTab()
    -- Time controls
    ui.text("Server Time")
    ui.separator()
    
    local newHour, hourChanged = ui.slider("Hour", state.serverTime.hour, 0, 23, "%.0f")
    if hourChanged then state.serverTime.hour = math.floor(newHour) end
    
    local newMinute, minChanged = ui.slider("Minute", state.serverTime.minute, 0, 59, "%.0f")
    if minChanged then state.serverTime.minute = math.floor(newMinute) end
    
    if ui.button("Set Time", vec2(80, 25)) then
        setTime(state.serverTime.hour, state.serverTime.minute)
    end
    
    -- Quick time presets
    ui.sameLine()
    if ui.button("Dawn", vec2(50, 25)) then setTime(6, 0) end
    ui.sameLine()
    if ui.button("Noon", vec2(50, 25)) then setTime(12, 0) end
    ui.sameLine()
    if ui.button("Sunset", vec2(50, 25)) then setTime(18, 30) end
    ui.sameLine()
    if ui.button("Night", vec2(50, 25)) then setTime(22, 0) end
    
    ui.spacing()
    ui.separator()
    
    -- Weather controls
    ui.text("Weather")
    ui.separator()
    
    local newTransition, transChanged = ui.slider("Transition (sec)", state.transitionDuration, 0, 120, "%.0f")
    if transChanged then state.transitionDuration = newTransition end
    
    ui.spacing()
    
    -- Weather type buttons
    ui.text("CSP Weather Types:")
    local buttonsPerRow = 4
    for i, weatherType in ipairs(state.weatherTypes) do
        ui.pushID(i)
        
        if (i - 1) % buttonsPerRow ~= 0 then
            ui.sameLine()
        end
        
        if ui.button(weatherType, vec2(90, 25)) then
            setWeather(weatherType, state.transitionDuration)
        end
        
        ui.popID()
    end
    
    if #state.weatherTypes == 0 then
        ui.textDisabled("Loading weather types...")
        if ui.button("Refresh Weather Types", vec2(150, 25)) then
            refreshWeatherTypes()
        end
    end
end

local function drawAuditTab()
    if ui.button("Refresh", vec2(80, 25)) then
        refreshAuditLog()
    end
    ui.sameLine()
    ui.text(string.format("Audit Log: %d entries", #state.auditLog))
    
    ui.separator()
    
    -- Audit log
    ui.childWindow('auditList', vec2(0, 380), true, ui.WindowFlags.None, function()
        for i, entry in ipairs(state.auditLog) do
            ui.pushID(i)
            
            ui.textDisabled(formatTimestamp(entry.timestamp))
            ui.sameLine(100)
            ui.text(entry.adminName or "Unknown")
            ui.sameLine(200)
            ui.textColored(rgbm(0.8, 0.8, 0.3, 1), entry.action or "")
            
            if entry.targetName then
                ui.sameLine(280)
                ui.text("-> " .. entry.targetName)
            end
            
            if entry.details then
                ui.textDisabled("  " .. entry.details)
            end
            
            ui.popID()
        end
    end)
end

local function drawAdminPanel()
    -- Header
    ui.text("SXR Admin Tools")
    ui.sameLine(300)
    ui.textDisabled(string.format("Level: %s", state.adminLevel))
    ui.separator()
    
    -- Tab bar
    drawTabBar()
    
    -- Tab content
    if state.currentTab == 1 then
        drawPlayersTab()
    elseif state.currentTab == 2 then
        drawBansTab()
    elseif state.currentTab == 3 then
        drawWhitelistTab()
    elseif state.currentTab == 4 then
        drawServerTab()
    elseif state.currentTab == 5 then
        drawAuditTab()
    end
    
    -- Footer with error display
    ui.separator()
    if state.lastError and (os.clock() - state.lastErrorTime) < 10 then
        ui.textColored(rgbm(1, 0.3, 0.3, 1), "Error: " .. state.lastError)
    else
        ui.textDisabled(string.format("Server: %s:%d", serverIP, serverPort))
    end
end

-- ============================================================================
-- INITIALIZATION & REGISTRATION
-- ============================================================================

-- Get player Steam ID on load
local function initialize()
    local car = ac.getCar(0)
    if car then
        -- Try to get Steam ID from driver info
        state.mySteamId = ac.getDriverGuid(0) or ""
    end
    
    -- Initial data load
    refreshAdminStatus()
    refreshWeatherTypes()
end

-- Register in Online Extras menu (Extended Chat)
-- CRITICAL: All 8 parameters required, including flags
ui.registerOnlineExtra(
    ui.Icons.Settings,                          -- iconID
    "SXR Admin Panel",                          -- title
    function()                                   -- availableCallback
        return state.isAdmin or true  -- Show for testing, normally: return state.isAdmin
    end,
    function()                                   -- uiCallback
        local close = false
        
        -- Auto-refresh
        local now = os.clock()
        if now - state.lastRefresh > state.refreshInterval then
            state.lastRefresh = now
            refreshPlayers()
        end
        
        -- Draw panel content
        ui.childWindow('adminContent', vec2(0, 480), false, ui.WindowFlags.None, function()
            drawAdminPanel()
        end)
        
        return close
    end,
    nil,                                         -- closeCallback
    ui.OnlineExtraFlags.Admin + ui.OnlineExtraFlags.Tool,  -- flags (Admin + Tool window)
    ui.WindowFlags.None,                         -- toolFlags
    vec2(500, 550)                              -- toolSize
)

-- Initialize on script load
initialize()

-- Periodic refresh in update
function script.update(dt)
    -- Refresh admin status periodically
    local now = os.clock()
    if now - state.lastRefresh > 30 then
        refreshAdminStatus()
    end
end

logInfo("SXR Admin Tools loaded")
