local config = ac.configValues({
  enableLeaderboard = true
})

ac.debug("enableLeaderboard", config.enableLeaderboard)

local EventType = {
  None = 0,
  RaceChallenge = 1,
  RaceCountdown = 2,
  RaceEnded = 3
}

local rivalHealth = 1.0
local rivalRate = 0
local ownHealth = 1.0
local ownRate = 0
local rivalId = 0
local rivalName = ""
--local lastEvent = EventType.RaceCountdown
local lastEvent = EventType.None
local raceStartTime = -1
--local raceStartTime = ac.getSim().currentServerTime + 3000
local raceEndTime = 0
local lastWinner = 0
local ownSessionId = ac.getCar(0).sessionID
local leaderboardUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/racechallenge/leaderboard"

function GetLeaderboard(callback)
  web.get(leaderboardUrl, function (err, response)
      callback(stringify.parse(response.body))
  end)
end

function GetOwnRanking(callback)
  web.get(leaderboardUrl .. "/" .. ac.getUserSteamID(), function (err, response)
    callback(stringify.parse(response.body))
  end)
end

function GetDriverNameBySessionId(sessionId)
  local count = ac.getSim().carsCount
  for i = 0, count do
    local car = ac.getCar(i)
    if car.sessionID == sessionId then
      return ac.getDriverName(car.index)
    end
  end
end

local raceStatusEvent = ac.OnlineEvent({
  eventType = ac.StructItem.byte(),
  eventData = ac.StructItem.int64(),
}, function (sender, data)
    -- only accept packets from server
    if sender ~= nil then
      return
    end

    ac.debug("eventType", data.eventType)
    ac.debug("eventData", data.eventData)

    if data.eventType == EventType.RaceChallenge then
      rivalHealth = 1.0
      rivalRate = 0
      ownHealth = 1.0
      rivalRate = 0

      rivalId = data.eventData
      rivalName = GetDriverNameBySessionId(data.eventData)
      ac.debug("rivalName", rivalName)
    end

    if data.eventType == EventType.RaceCountdown then
      raceStartTime = data.eventData
    end

    if data.eventType == EventType.RaceEnded then
      lastWinner = data.eventData
      raceEndTime = GetSessionTime()
    end

    lastEvent = data.eventType
end)

local raceUpdateEvent = ac.OnlineEvent({
    ownHealth = ac.StructItem.float(),
    ownRate = ac.StructItem.float(),
    rivalHealth = ac.StructItem.float(),
    rivalRate = ac.StructItem.float()
  }, function (sender, data)
    -- only accept packets from server
    if sender ~= nil then
      return
    end

    ac.debug("sender", sender)

    ownHealth = data.ownHealth
    ownRate = data.ownRate
    rivalHealth = data.rivalHealth
    rivalRate = data.rivalRate

    ac.debug("ownHealth", ownHealth)
    ac.debug("ownRate", ownRate)
    ac.debug("rivalHealth", rivalHealth)
    ac.debug("rivalRate", rivalRate)
  end)

function GetSessionTime()
  return ac.getSim().timeToSessionStart * -1
end

-- sending a new message:
--raceUpdateEvent{ ownHealth = 0.6, ownRate = -0.2, rivalHealth = 0.3, rivalRate = -0.5 }
--raceStatusEvent{ eventType = 1, eventData = 2 }

local lastUiUpdate = GetSessionTime()
function script.drawUI()

  ac.debug("lastEvent", lastEvent)
  ac.debug("rivalId", rivalId)

  local currentTime = GetSessionTime()
  local dt = currentTime - lastUiUpdate
  lastUiUpdate = currentTime
  local raceTimeElapsed = currentTime - raceStartTime

  ac.debug("raceTimeElapsed", raceTimeElapsed)
  ac.debug("dt", dt)

  if lastEvent == EventType.RaceCountdown then
    if raceTimeElapsed > -3000 and raceTimeElapsed < 0 then
      DrawHealthHud(0)
      local text = math.ceil(tonumber(raceTimeElapsed) / 1000 * -1)
      DrawTextCentered(text)
    elseif raceTimeElapsed > 0 then
      if raceTimeElapsed < 1000 then
        DrawTextCentered("Go!")
      end

      ownHealth = ownHealth + ownRate * (dt / 1000)
      rivalHealth = rivalHealth + rivalRate * (dt / 1000)
      
      DrawHealthHud(raceTimeElapsed)
    end
  end

  if lastEvent == EventType.RaceEnded and raceEndTime > currentTime - 3000 then
    DrawHealthHud(raceEndTime - raceStartTime)

    if lastWinner == 255 then
      DrawTextCentered("Race cancelled")
    elseif lastWinner == ownSessionId then
      DrawTextCentered("You won the race!")
    else
      DrawTextCentered("You lost the race.")
    end
  end
end

function DrawTextCentered(text)
  local uiState = ac.getUI()

  ui.transparentWindow('raceText', vec2(uiState.windowSize.x / 2 - 250, uiState.windowSize.y / 2 - 250), vec2(500,100), function ()
    ui.pushFont(ui.Font.Huge)
    
    local size = ui.measureText(text)
    ui.setCursorX(ui.getCursorX() + ui.availableSpaceX() / 2 - (size.x / 2))
    ui.text(text)

    ui.popFont()
  end)
end

local barColor = rgbm(1,1,1,1)
function HealthBar(size, progress, direction)
  progress = math.clamp(progress, 0, 1)

  barColor:setLerp(rgbm.colors.red, rgbm.colors.white, progress)
  ui.drawRect(ui.getCursor(), ui.getCursor() + size, barColor)

  local p1, p2
  if direction == -1 then
    p1 = ui.getCursor() + vec2(size.x * (1 - progress), 0)
    p2 = ui.getCursor() + size
  else
    p1 = ui.getCursor()
    p2 = ui.getCursor() + vec2(size.x * progress, size.y)
  end

  ui.drawRectFilled(p1, p2, barColor)

  ui.dummy(size)
end

function DrawHealthHud(time)
  local uiState = ac.getUI()

  ui.toolWindow('raceChallengeHUD', vec2(uiState.windowSize.x / 2 - 500, 25), vec2(1000, 120), function ()
    ui.pushFont(ui.Font.Title)

    ui.columns(3)
    ui.text("PLAYER")
    ui.nextColumn()

    local laptime = ac.lapTimeToString(time)
    local size = ui.measureText(laptime)
    
    ui.setCursorX(ui.getCursorX() + ui.availableSpaceX() / 2 - (size.x / 2))
    ui.text(laptime)
    ui.nextColumn()
    ui.textAligned("RIVAL", ui.Alignment.End, vec2(-1,0))

    ui.columns(2)
    HealthBar(vec2(ui.availableSpaceX(), 30), ownHealth, -1)
    ui.textAligned(ac.getDriverName(0), ui.Alignment.Start, vec2(-1,0))
    ui.nextColumn()
    HealthBar(vec2(ui.availableSpaceX(), 30), rivalHealth, 1)
    ui.textAligned(rivalName, ui.Alignment.End, vec2(-1,0))

    ui.popFont()
  end)
end

function PrintLeaderboardRow(rank, name, rating)
  ui.text(tostring(rank))
  ui.nextColumn()
  ui.text(name)
  ui.nextColumn()
  ui.text(tostring(rating))
  ui.nextColumn()
end

local loadingLeaderboard = false
local leaderboard = nil
local ownRanking = nil

if config.enableLeaderboard then
  ui.registerOnlineExtra(ui.Icons.Leaderboard, "Race Challenge Leaderboard", function () return true end, function ()
    if not loadingLeaderboard then
      loadingLeaderboard = true
      GetLeaderboard(function (response)
        leaderboard = response
      end)
      GetOwnRanking(function (response)
        ownRanking = response
      end)
    end

    local close = false
    ui.childWindow('raceChallengeLeaderboard', vec2(0, 275), false, ui.WindowFlags.None, function ()
      if leaderboard == nil or ownRanking == nil then
        ui.text("Loading...")
      else
        ui.columns(3)
        ui.setColumnWidth(0, 45)
        ui.setColumnWidth(1, 200)

        PrintLeaderboardRow("#", "Name", "Rating")
        
        for i, player in ipairs(leaderboard) do
          PrintLeaderboardRow(tostring(i) .. ".", player.Name, player.Rating)
        end
        
        PrintLeaderboardRow("...", "", "")
        PrintLeaderboardRow(ownRanking.Rank .. ".", ac.getDriverName(0), ownRanking.Rating)

        ui.columns()
      end

      ui.offsetCursorY(ui.availableSpaceY() - 32)
      if ui.button("Close") then
        close = true
        loadingLeaderboard = false
      end
    end)

    return close
  end)
end
