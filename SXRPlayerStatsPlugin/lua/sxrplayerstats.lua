--[[
    Player Stats Plugin - In-Game Statistics UI
    
    Features:
    - Multi-tab interface (Overview, Racing, Driving, Records)
    - Driver Level progress bar
    - Car usage breakdown
    - Personal records
    - Leaderboard comparison
    - Milestone tracking
]]

local config = ac.configValues({
    refreshInterval = 30, -- Seconds between API refreshes
    showMiniHud = true,   -- Show compact HUD while driving
    hudPosition = vec2(10, 200)
})

-- API URLs
local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/sxrplayerstats"
local steamId = ac.getUserSteamID()

-- State
local state = {
    loading = false,
    lastRefresh = 0,
    currentTab = 1,
    stats = nil,
    carStats = nil,
    milestones = nil,
    leaderboard = nil,
    leaderboardCategory = "DriverLevel"
}

local tabNames = { "Overview", "Racing", "Driving", "Records", "Cars" }
local leaderboardCategories = {
    "DriverLevel", "TotalDistance", "TotalTime", "RaceWins", 
    "BattleWins", "TopSpeed", "AverageSpeed"
}

-- ============================================================================
-- API FUNCTIONS
-- ============================================================================

function FetchStats()
    if state.loading then return end
    state.loading = true
    
    web.get(baseUrl .. "/" .. steamId .. "/summary", function(err, response)
        if not err and response.body then
            local parsed = stringify.parse(response.body)
            if parsed then
                state.stats = parsed
            end
        end
        state.loading = false
        state.lastRefresh = os.clock()
    end)
end

function FetchCarStats()
    web.get(baseUrl .. "/" .. steamId .. "/cars", function(err, response)
        if not err and response.body then
            state.carStats = stringify.parse(response.body)
        end
    end)
end

function FetchMilestones()
    web.get(baseUrl .. "/" .. steamId .. "/milestones", function(err, response)
        if not err and response.body then
            state.milestones = stringify.parse(response.body)
        end
    end)
end

function FetchLeaderboard(category)
    web.get(baseUrl .. "/leaderboard/" .. category .. "?count=10", function(err, response)
        if not err and response.body then
            state.leaderboard = stringify.parse(response.body)
            state.leaderboardCategory = category
        end
    end)
end

-- ============================================================================
-- UI HELPERS
-- ============================================================================

local colors = {
    accent = rgbm(0, 0.8, 1, 1),
    gold = rgbm(1, 0.84, 0, 1),
    silver = rgbm(0.75, 0.75, 0.75, 1),
    bronze = rgbm(0.8, 0.5, 0.2, 1),
    green = rgbm(0.2, 0.8, 0.2, 1),
    red = rgbm(0.8, 0.2, 0.2, 1),
    dimmed = rgbm(0.6, 0.6, 0.6, 1),
    bg = rgbm(0.1, 0.1, 0.12, 0.95),
    bgLight = rgbm(0.15, 0.15, 0.18, 1)
}

function DrawProgressBar(pos, size, progress, color, bgColor)
    progress = math.clamp(progress or 0, 0, 1)
    color = color or colors.accent
    bgColor = bgColor or colors.bgLight
    
    -- Background
    ui.drawRectFilled(pos, pos + size, bgColor)
    
    -- Fill
    if progress > 0 then
        ui.drawRectFilled(pos, pos + vec2(size.x * progress, size.y), color)
    end
    
    -- Border
    ui.drawRect(pos, pos + size, rgbm(0.3, 0.3, 0.3, 1))
end

function DrawStatRow(label, value, unit)
    ui.text(label)
    ui.sameLine(180)
    ui.textColored(tostring(value), colors.accent)
    if unit then
        ui.sameLine()
        ui.textColored(unit, colors.dimmed)
    end
end

function FormatTime(seconds)
    if seconds < 60 then
        return string.format("%.0fs", seconds)
    elseif seconds < 3600 then
        return string.format("%.0fm", seconds / 60)
    else
        return string.format("%.1fh", seconds / 3600)
    end
end

function FormatDistance(km)
    if km < 1 then
        return string.format("%.0fm", km * 1000)
    elseif km < 100 then
        return string.format("%.1f km", km)
    else
        return string.format("%.0f km", km)
    end
end

function FormatNumber(n)
    if n >= 1000000 then
        return string.format("%.1fM", n / 1000000)
    elseif n >= 1000 then
        return string.format("%.1fK", n / 1000)
    else
        return string.format("%.0f", n)
    end
end

-- ============================================================================
-- TAB CONTENT DRAWING
-- ============================================================================

function DrawOverviewTab()
    if not state.stats then
        ui.text("Loading...")
        return
    end
    
    local s = state.stats
    
    -- Driver Level Header
    ui.pushFont(ui.Font.Title)
    ui.textColored("Driver Level " .. s.DriverLevel, colors.accent)
    ui.popFont()
    
    -- XP Progress Bar
    ui.spacing()
    local barPos = ui.getCursor()
    DrawProgressBar(barPos, vec2(350, 20), s.LevelProgress, colors.accent)
    ui.dummy(vec2(350, 20))
    
    ui.textColored(string.format("XP: %s / %s", FormatNumber(s.TotalXP), FormatNumber(s.XPToNextLevel)), colors.dimmed)
    
    ui.spacing()
    ui.separator()
    ui.spacing()
    
    -- Quick Stats Grid
    ui.columns(2)
    
    DrawStatRow("Distance", FormatDistance(s.TotalDistanceKm))
    DrawStatRow("Time", FormatTime(s.TotalTimeHours * 3600))
    DrawStatRow("Sessions", s.TotalSessions)
    DrawStatRow("Cars Used", s.UniqueCarsUsed)
    
    ui.nextColumn()
    
    DrawStatRow("Race Wins", s.RaceWins)
    DrawStatRow("Battle Wins", s.BattleWins)
    DrawStatRow("Top Speed", string.format("%.1f", s.TopSpeedKph), "km/h")
    DrawStatRow("Avg Speed", string.format("%.1f", s.AverageSpeedKph), "km/h")
    
    ui.columns()
    
    -- Favorite Car
    ui.spacing()
    ui.separator()
    ui.spacing()
    
    if s.FavoriteCar and s.FavoriteCar ~= "" then
        ui.text("Favorite Car:")
        ui.sameLine()
        ui.textColored(s.FavoriteCar, colors.gold)
    end
    
    -- Milestones Count
    ui.text("Milestones:")
    ui.sameLine()
    ui.textColored(tostring(s.MilestoneCount), colors.accent)
end

function DrawRacingTab()
    if not state.stats then return end
    local s = state.stats
    
    ui.pushFont(ui.Font.Title)
    ui.text("Racing Statistics")
    ui.popFont()
    ui.spacing()
    
    -- Wins section
    ui.textColored("VICTORIES", colors.gold)
    ui.spacing()
    
    DrawStatRow("Race Wins", s.RaceWins)
    DrawStatRow("Battle Wins", s.BattleWins)
    DrawStatRow("Total Wins", s.RaceWins + s.BattleWins)
    
    ui.spacing()
    ui.separator()
    ui.spacing()
    
    -- Collision stats
    ui.textColored("COLLISION RECORD", colors.red)
    ui.spacing()
    
    DrawStatRow("Total Collisions", s.TotalCollisions)
    DrawStatRow("Avg per Race", string.format("%.1f", s.AverageCollisionsPerRace))
    DrawStatRow("Clean Race Rate", string.format("%.0f%%", s.CleanRaceRate * 100))
end

function DrawDrivingTab()
    if not state.stats then return end
    local s = state.stats
    
    ui.pushFont(ui.Font.Title)
    ui.text("Driving Statistics")
    ui.popFont()
    ui.spacing()
    
    -- Distance
    ui.textColored("DISTANCE", colors.accent)
    ui.spacing()
    
    DrawStatRow("Total Distance", FormatDistance(s.TotalDistanceKm))
    
    -- Progress to next milestone
    local nextMilestone = 100
    if s.TotalDistanceKm >= 100 then nextMilestone = 1000 end
    if s.TotalDistanceKm >= 1000 then nextMilestone = 10000 end
    if s.TotalDistanceKm >= 10000 then nextMilestone = 100000 end
    
    local progress = s.TotalDistanceKm / nextMilestone
    ui.text("Next milestone: " .. FormatDistance(nextMilestone))
    local barPos = ui.getCursor()
    DrawProgressBar(barPos, vec2(300, 15), progress, colors.gold)
    ui.dummy(vec2(300, 15))
    
    ui.spacing()
    ui.separator()
    ui.spacing()
    
    -- Speed
    ui.textColored("SPEED", colors.accent)
    ui.spacing()
    
    DrawStatRow("Top Speed", string.format("%.1f", s.TopSpeedKph), "km/h")
    DrawStatRow("Average Speed", string.format("%.1f", s.AverageSpeedKph), "km/h")
    
    ui.spacing()
    ui.separator()
    ui.spacing()
    
    -- Time
    ui.textColored("TIME", colors.accent)
    ui.spacing()
    
    DrawStatRow("Total Time", FormatTime(s.TotalTimeHours * 3600))
    DrawStatRow("Sessions", s.TotalSessions)
    DrawStatRow("Avg Session", FormatTime(s.TotalTimeHours * 3600 / math.max(1, s.TotalSessions)))
end

function DrawRecordsTab()
    if not state.stats then return end
    
    ui.pushFont(ui.Font.Title)
    ui.text("Personal Records")
    ui.popFont()
    ui.spacing()
    
    -- Milestones
    ui.textColored("MILESTONES ACHIEVED", colors.gold)
    ui.spacing()
    
    if state.milestones and #state.milestones > 0 then
        for _, m in ipairs(state.milestones) do
            ui.bulletText(m.Name)
        end
    else
        ui.textColored("No milestones yet - keep driving!", colors.dimmed)
    end
    
    ui.spacing()
    ui.separator()
    ui.spacing()
    
    -- Leaderboard
    ui.textColored("LEADERBOARD", colors.accent)
    ui.spacing()
    
    -- Category selector
    if ui.button("< Prev") then
        local idx = 1
        for i, cat in ipairs(leaderboardCategories) do
            if cat == state.leaderboardCategory then idx = i break end
        end
        idx = idx - 1
        if idx < 1 then idx = #leaderboardCategories end
        FetchLeaderboard(leaderboardCategories[idx])
    end
    ui.sameLine()
    ui.text(state.leaderboardCategory)
    ui.sameLine()
    if ui.button("Next >") then
        local idx = 1
        for i, cat in ipairs(leaderboardCategories) do
            if cat == state.leaderboardCategory then idx = i break end
        end
        idx = idx + 1
        if idx > #leaderboardCategories then idx = 1 end
        FetchLeaderboard(leaderboardCategories[idx])
    end
    
    ui.spacing()
    
    if state.leaderboard then
        for _, entry in ipairs(state.leaderboard) do
            local color = rgbm.colors.white
            if entry.Rank == 1 then color = colors.gold
            elseif entry.Rank == 2 then color = colors.silver
            elseif entry.Rank == 3 then color = colors.bronze
            end
            
            ui.textColored(string.format("#%d", entry.Rank), color)
            ui.sameLine(40)
            ui.text(entry.Name)
            ui.sameLine(200)
            ui.textColored(entry.FormattedValue, colors.accent)
        end
    else
        ui.text("Loading...")
    end
end

function DrawCarsTab()
    if not state.carStats then 
        if not state.loading then
            FetchCarStats()
        end
        ui.text("Loading car stats...")
        return
    end
    
    ui.pushFont(ui.Font.Title)
    ui.text("Car Statistics")
    ui.popFont()
    ui.spacing()
    
    -- Sort by distance
    local sorted = {}
    for name, stats in pairs(state.carStats) do
        table.insert(sorted, { name = name, stats = stats })
    end
    table.sort(sorted, function(a, b) return a.stats.DistanceMeters > b.stats.DistanceMeters end)
    
    ui.columns(3)
    ui.setColumnWidth(0, 180)
    ui.setColumnWidth(1, 100)
    
    ui.textColored("Car", colors.dimmed)
    ui.nextColumn()
    ui.textColored("Distance", colors.dimmed)
    ui.nextColumn()
    ui.textColored("Top Speed", colors.dimmed)
    ui.nextColumn()
    
    ui.separator()
    
    local shown = 0
    for _, entry in ipairs(sorted) do
        if shown >= 10 then break end
        shown = shown + 1
        
        local isFavorite = state.stats and state.stats.FavoriteCar == entry.name
        local nameColor = isFavorite and colors.gold or rgbm.colors.white
        
        ui.textColored(entry.name:sub(1, 25), nameColor)
        ui.nextColumn()
        ui.text(FormatDistance(entry.stats.DistanceMeters / 1000))
        ui.nextColumn()
        ui.textColored(string.format("%.0f", entry.stats.TopSpeedKph), colors.accent)
        ui.nextColumn()
    end
    
    ui.columns()
end

-- ============================================================================
-- MAIN UI PANEL
-- ============================================================================

local panelOpen = false

ui.registerOnlineExtra(ui.Icons.User, "Player Statistics", function() return true end, function()
    -- Refresh data if needed
    if os.clock() - state.lastRefresh > config.refreshInterval then
        FetchStats()
        FetchMilestones()
        FetchLeaderboard(state.leaderboardCategory)
    end
    
    local close = false
    
    ui.childWindow('playerStatsPanel', vec2(400, 450), false, ui.WindowFlags.None, function()
        -- Tab bar
        ui.pushStyleColor(ui.StyleColor.Button, colors.bgLight)
        for i, name in ipairs(tabNames) do
            if i > 1 then ui.sameLine() end
            
            local isActive = state.currentTab == i
            if isActive then
                ui.pushStyleColor(ui.StyleColor.Button, colors.accent)
            end
            
            if ui.button(name) then
                state.currentTab = i
                if i == 5 and not state.carStats then
                    FetchCarStats()
                end
            end
            
            if isActive then
                ui.popStyleColor()
            end
        end
        ui.popStyleColor()
        
        ui.spacing()
        ui.separator()
        ui.spacing()
        
        -- Tab content
        if state.currentTab == 1 then
            DrawOverviewTab()
        elseif state.currentTab == 2 then
            DrawRacingTab()
        elseif state.currentTab == 3 then
            DrawDrivingTab()
        elseif state.currentTab == 4 then
            DrawRecordsTab()
        elseif state.currentTab == 5 then
            DrawCarsTab()
        end
        
        -- Footer
        ui.offsetCursorY(ui.availableSpaceY() - 35)
        ui.separator()
        
        if ui.button("Refresh") then
            FetchStats()
            FetchCarStats()
            FetchMilestones()
            FetchLeaderboard(state.leaderboardCategory)
        end
        ui.sameLine()
        if ui.button("Close") then
            close = true
        end
        
        if state.loading then
            ui.sameLine()
            ui.textColored("Loading...", colors.dimmed)
        end
    end)
    
    return close
end)

-- ============================================================================
-- MINI HUD (while driving)
-- ============================================================================

function script.drawUI()
    if not config.showMiniHud then return end
    if not state.stats then return end
    
    -- Don't show if main panel is open
    -- (The panel registration handles its own visibility)
    
    -- Mini HUD showing level and XP
    local s = state.stats
    local uiState = ac.getUI()
    
    ui.transparentWindow('playerStatsMini', config.hudPosition, vec2(150, 60), function()
        ui.pushFont(ui.Font.Small)
        
        ui.textColored("Lv." .. s.DriverLevel, colors.accent)
        ui.sameLine()
        ui.textColored(string.format("%.0f%%", s.LevelProgress * 100), colors.dimmed)
        
        -- Mini progress bar
        local barPos = ui.getCursor()
        DrawProgressBar(barPos, vec2(140, 8), s.LevelProgress, colors.accent)
        ui.dummy(vec2(140, 8))
        
        ui.textColored(FormatDistance(s.TotalDistanceKm) .. " driven", colors.dimmed)
        
        ui.popFont()
    end)
end

-- ============================================================================
-- INITIALIZATION
-- ============================================================================

-- Initial data fetch
FetchStats()
FetchMilestones()
FetchLeaderboard("DriverLevel")

ac.debug("Player Stats UI", "Loaded")
ac.debug("Steam ID", steamId)
