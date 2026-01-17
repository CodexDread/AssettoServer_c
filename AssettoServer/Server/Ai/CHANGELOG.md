# AI Traffic System Changelog

## [2026-01-14] Lane Change Safety Overhaul

### Fixed: Cars Changing Lanes INTO Other Cars
Added `HasAdjacentVehicle()` method that checks for vehicles at the SAME spline position but in the target lane. This prevents cars from changing lanes directly into another car.

- Checks both ahead and behind in target lane within a safety margin
- Uses world distance verification (not just spline position) for accuracy
- Safety margin scales with aggressiveness (20m passive → 12m aggressive)
- Applied to both `EvaluateLaneChange()` and `EvaluateProactiveLaneChange()`

### Fixed: Chain-Reaction Lane Changes
When a car changes lane and ends up close in front of another car, the following car would immediately panic and change lanes too. This caused cascading lane changes.

**Solution:** Added chain-reaction cooldown:
- Tracks when a new leader appears (`_lastKnownLeaderSessionId`)
- Records the time a new obstacle appeared (`_newObstacleAppearedTime`)
- Enforces cooldown before allowing lane change (3s passive → 1.5s aggressive)
- Clears any planned lane changes when situation changes

### Fixed: Preemptive Lane Change Timing
Previously, aggressive drivers would look ahead AND immediately execute lane changes. Now they:
1. **PLAN** lane changes when seeing slower traffic ahead
2. **WAIT** until actually blocked by the obstacle
3. **EXECUTE** the planned lane change only when `hasObstacle` is true

New method `TryPlanProactiveLaneChange()` stores planned lane change info:
- `_hasPlannedLaneChange` - whether a gap was found
- `_plannedLaneChangeIsLeft` - which direction
- `_plannedTargetPointId` - target lane point
- `_plannedLaneChangeGapDistance` - distance to the gap

### Technical Details
New fields added to `AiState`:
```csharp
private float _newObstacleAppearedTime;
private int _lastKnownLeaderSessionId = -1;
private bool _hasPlannedLaneChange;
private bool _plannedLaneChangeIsLeft;
private int _plannedTargetPointId;
private float _plannedLaneChangeGapDistance;
```

All new state properly reset in `Despawn()`.

---

## [2026-01-14] Mid-Merge Collision Detection & Smooth Abort

### Fixed: Cars Continue Merging Into Collisions
Previously, if two cars tried to merge into the same lane simultaneously, or if a fast player appeared in the target lane during a merge, the AI would continue merging anyway.

**Solution:** Added `HasCollisionInTargetLane()` that continuously checks during lane change:
- Checks AI traffic in target lane at current position
- Checks players with velocity-based margin (fast players get more margin)
- Dynamic collision margin: 8-20m based on lane change progress
  - Tightest margin (20m) at 50% progress (middle of two lanes)
  - Relaxed margin (8m) at edges (10% and 90% progress)
- Only checks between 10-90% progress (not at very start/end)

### Fixed: Teleport on Lane Change Abort
Previously, when a lane change was aborted (lane ended, collision detected), the car would instantly teleport back to its original position.

**Solution:** Added smooth abort animation system:
- `StartLaneChangeAbort()` - initiates abort, turns on hazards
- `UpdateLaneChangeAbort()` - smooth ease-out interpolation back to original lane
- Duration scales with how far into the lane change (0.5-2 seconds)
- Uses quadratic ease-out for natural deceleration feel

### Technical Details
New fields added to `AiState`:
```csharp
private bool _laneChangeAborting;
private float _laneChangeAbortStartTime;
private float _laneChangeAbortStartOffset;
```

The abort animation uses: `offset = startOffset * (1 - easeOut)` where `easeOut = 1 - (1-t)²`

---

## [2026-01-14] Natural Steering Rotation During Lane Changes

### Added: Steering Yaw During Lane Change
Previously, cars would slide sideways during lane changes which looked unnatural. Now cars rotate toward the target lane as they change, simulating actual steering.

**Implementation:** `CalculateLaneChangeSteeringYaw()` method:
- Uses derivative of quintic polynomial: `30t² - 60t³ + 30t⁴`
- This matches the rate of lateral movement (when moving fastest = steering hardest)
- Peak steering occurs at ~33% progress
- Max steering angle: ~7° at baseline (108 km/h)
- Scales inversely with speed (faster = less steering needed)
- Also applies during abort (steers back toward original lane)

**Math Details:**
- Derivative normalized by max value (1.875)
- Direction: left lane = negative yaw, right lane = positive yaw
- Speed factor: `0.12 radians / (speed/30)` = less angle at higher speeds
