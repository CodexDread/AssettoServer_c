local config = ac.configValues({
    minimumSpeed = 80,
    showUI = false,
    collisionMessages = "",
    overtakeMessages = "",
    closeOvertakeMessages = ""
})

local collisionMessages = stringify.parse(config.collisionMessages)
local overtakeMessages = stringify.parse(config.overtakeMessages)
local closeOvertakeMessages = stringify.parse(config.closeOvertakeMessages)

ui.registerOnlineExtra(ui.Icons.CarFront, "Overtake Run", nil, nil, function (okClicked)
  config.showUI = not config.showUI
end)

-- Event state:
local timePassed = 0
local totalScore = 0
local comboMeter = 1
local comboColor = 0
local currentRank = 0

local personalBest = 0
local ownRank = 0

local messages = {}
local glitter = {}
local glitterCount = 0

local overtakeEventType = {
    NONE = 0,
    OVERTAKE = 1,
    COLLISION = 2,
    FINISHED = 3,
    TOO_SLOW = 4,
    CLOSE_OVERTAKE = 5
}

local function addMessage(text, mood)
  for i = math.min(#messages + 1, 4), 2, -1 do
    messages[i] = messages[i - 1]
    messages[i].targetPos = i
  end
  messages[1] = { text = text, age = 0, targetPos = 1, currentPos = 1, mood = mood }
  if mood == 1 then
    for i = 1, 60 do
      local dir = vec2(math.random() - 0.5, math.random() - 0.5)
      glitterCount = glitterCount + 1
      glitter[glitterCount] = { 
        color = rgbm.new(hsv(math.random() * 360, 1, 1):rgb(), 1), 
        pos = vec2(80, 140) + dir * vec2(40, 20),
        velocity = dir:normalize():scale(0.2 + math.random()),
        life = 0.5 + 0.5 * math.random()
      }
    end
  end
end

local numUpdates = 0
local overtakeUpdateEvent = ac.OnlineEvent({
    ac.StructItem.key("overtakeUpdate"),
    score = ac.StructItem.int64(),
    combo = ac.StructItem.float(),
    events = ac.StructItem.array(ac.StructItem.byte(), 10),
    rank = ac.StructItem.int32()
}, function (sender, message)
    if sender ~= nil then return end

    numUpdates = numUpdates + 1
    ac.debug("No. of Updates", numUpdates)

    comboMeter = tonumber(message.combo)
    totalScore = tonumber(message.score)
    currentRank = tonumber(message.rank)

    if totalScore > personalBest then
        personalBest = totalScore
    end

    for i = 0, 9 do
        if     message.events[i] == overtakeEventType.NONE           then break
        elseif message.events[i] == overtakeEventType.OVERTAKE       then addMessage(overtakeMessages[math.random(#overtakeMessages)], 0)
        elseif message.events[i] == overtakeEventType.CLOSE_OVERTAKE then addMessage(closeOvertakeMessages[math.random(#closeOvertakeMessages)], 1)
        elseif message.events[i] == overtakeEventType.TOO_SLOW       then addMessage("Speed up!", -1)
        elseif message.events[i] == overtakeEventType.COLLISION      then addMessage(collisionMessages[math.random(#collisionMessages)], -1)
        end
    end
end)

local overtakePersonalBestEvent = ac.OnlineEvent({
    ac.StructItem.key("overtakePersonalBest"),
    score = ac.StructItem.int64(),
    rank = ac.StructItem.int32()
}, function (sender, message)
    if sender ~= nil then return end

    personalBest = tonumber(message.score)
    ownRank = tonumber(message.rank)
end)

overtakePersonalBestEvent({})

function script.update(dt)
  if not config.showUI then return end

  local comboFadingRate = 0.5 * math.lerp(1, 0.1, math.lerpInvSat(car.speedKmh, 80, 200))
  comboMeter = math.max(1, comboMeter - dt * comboFadingRate)
end

local function updateMessages(dt)
  comboColor = comboColor + math.min(25, dt * 3 * comboMeter)
  if comboColor > 360 then comboColor = comboColor - 360 end
  for i = 1, #messages do
    local m = messages[i]
    m.age = m.age + dt
    m.currentPos = math.applyLag(m.currentPos, m.targetPos, 0.8, dt)
  end
  for i = glitterCount, 1, -1 do
    local g = glitter[i]
    g.pos:add(g.velocity)
    g.velocity.y = g.velocity.y + 0.02
    g.life = g.life - dt
    g.color.mult = math.saturate(g.life * 4)
    if g.life < 0 then
      if i < glitterCount then
        glitter[i] = glitter[glitterCount]
      end
      glitterCount = glitterCount - 1
    end
  end
  if comboMeter > 10 and math.random() > 0.98 then
    for i = 1, math.floor(comboMeter) do
      local dir = vec2(math.random() - 0.5, math.random() - 0.5)
      glitterCount = glitterCount + 1
      glitter[glitterCount] = { 
        color = rgbm.new(hsv(math.random() * 360, 1, 1):rgb(), 1), 
        pos = vec2(195, 75) + dir * vec2(40, 20),
        velocity = dir:normalize():scale(0.2 + math.random()),
        life = 0.5 + 0.5 * math.random()
      }
    end
  end
end

local speedWarning = 0
function script.drawUI()
  if not config.showUI then return end

  local uiState = ac.getUI()
  updateMessages(uiState.dt)

  local speedRelative = math.saturate(math.floor(ac.getCarState(1).speedKmh) / config.minimumSpeed)
  speedWarning = math.applyLag(speedWarning, speedRelative < 1 and 1 or 0, 0.5, uiState.dt)

  local colorDark = rgbm(0.4, 0.4, 0.4, 1)
  local colorGrey = rgbm(0.7, 0.7, 0.7, 1)
  local colorAccent = rgbm.new(hsv(speedRelative * 120, 1, 1):rgb(), 1)
  local colorCombo = rgbm.new(hsv(comboColor, math.saturate(comboMeter / 10), 1):rgb(), math.saturate(comboMeter / 4))

  local function speedMeter(ref)
    ui.drawRectFilled(ref + vec2(0, -4), ref + vec2(180, 5), colorDark, 1)
    ui.drawLine(ref + vec2(0, -4), ref + vec2(0, 4), colorGrey, 1)
    ui.drawLine(ref + vec2(config.minimumSpeed, -4), ref + vec2(config.minimumSpeed, 4), colorGrey, 1)

    local speed = math.min(ac.getCarState(1).speedKmh, 180)
    if speed > 1 then
      ui.drawLine(ref + vec2(0, 0), ref + vec2(speed, 0), colorAccent, 4)
    end
  end

  ui.beginTransparentWindow("overtakeScore", vec2(uiState.windowSize.x * 0.5 - 600, 100), vec2(400, 400), false)
  ui.beginOutline()

  ui.pushStyleVar(ui.StyleVar.Alpha, 1 - speedWarning)
  ui.pushFont(ui.Font.Title)
  ui.text("Overtake Run")
  ui.popFont()
  ui.popStyleVar()

  local pbText = "PB: " .. personalBest .. " pts"
  if ownRank > 0 then
    pbText = pbText .. " (" .. ownRank .. ".)"
  end
  ui.text(pbText)

  ui.pushFont(ui.Font.Huge)
  ui.text(totalScore .. " pts")
  ui.sameLine(0, 40)
  if comboMeter > 20 then
    ui.beginRotation()
  end
  ui.textColored(math.ceil(comboMeter * 10) / 10 .. "x", colorCombo)
  if comboMeter > 20 then
    ui.endRotation(math.sin(comboMeter / 180 * 3141.5) * 3 * math.lerpInvSat(comboMeter, 20, 30) + 90)
  end
  ui.popFont()

  if currentRank > 0 then
    ui.offsetCursorY(-5)
    ui.pushFont(ui.Font.Title)
    ui.text("Current Rank: " .. currentRank .. ".")
    ui.popFont()
  end
  
  ui.endOutline(rgbm(0, 0, 0, 0.3))

  ui.offsetCursorY(20)
  ui.pushFont(ui.Font.Title)
  local startPos = ui.getCursor()
  for i = 1, #messages do
    local m = messages[i]
    local f = math.saturate(4 - m.currentPos) * math.saturate(8 - m.age)
    ui.setCursor(startPos + vec2(20 + math.saturate(1 - m.age * 10) ^ 2 * 100, (m.currentPos - 1) * 30))
    ui.beginOutline()
    ui.textColored(m.text, m.mood == 1 and rgbm(0, 1, 0, f) or m.mood == -1 and rgbm(1, 0, 0, f) or rgbm(1, 1, 1, f))
    ui.endOutline(rgbm(0, 0, 0, 0.3 * f))
  end
  for i = 1, glitterCount do
    local g = glitter[i]
    if g ~= nil then
      ui.drawLine(g.pos, g.pos + g.velocity * 4, g.color, 2)
    end
  end
  ui.popFont()
  ui.setCursor(startPos + vec2(0, 4 * 30))

  ui.pushStyleVar(ui.StyleVar.Alpha, speedWarning)
  ui.setCursorY(0)
  ui.pushFont(ui.Font.Main)
  ui.textColored("Keep speed above " .. config.minimumSpeed .. " km/h:", colorAccent)
  speedMeter(ui.getCursor() + vec2(-9, 4))
  ui.popFont()
  ui.popStyleVar()

  ui.endTransparentWindow()
end

--[[
  This script is based on the overtake mode by x4fab licensed under the MIT license:
  https://github.com/ac-custom-shaders-patch/acc-lua-internal/blob/main/included-new-modes/overtake/mode.lua

  Please note that the MIT license only applies to code written by x4fab.
  All other parts of this file are (c) 2023 AssettoServer Development Team.

  MIT License

  Copyright (c) 2022 Ilja Jusupov

  Permission is hereby granted, free of charge, to any person obtaining a copy
  of this software and associated documentation files (the "Software"), to deal
  in the Software without restriction, including without limitation the rights
  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
  copies of the Software, and to permit persons to whom the Software is
  furnished to do so, subject to the following conditions:

  The above copyright notice and this permission notice shall be included in all
  copies or substantial portions of the Software.

  THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
  SOFTWARE.
]]