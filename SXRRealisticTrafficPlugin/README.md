# Realistic Traffic Plugin for AssettoServer

A comprehensive traffic simulation overhaul for AssettoServer, implementing physics-based car-following (IDM) and intelligent lane-changing (MOBIL) algorithms with zone-based density control optimized for Shuto Expressway.

## Features

### 🚗 Intelligent Driver Model (IDM)
- Physics-based car-following behavior
- Realistic acceleration and braking curves
- Speed variance based on driver personality
- Proper gap maintenance at all speeds

### 🛣️ MOBIL Lane Change Algorithm
- Considers safety (won't cut off other vehicles)
- Considers politeness (accounts for impact on other drivers)
- Keep-left bias for Japanese left-hand traffic
- Smooth quintic polynomial trajectories (no teleporting!)

### 🗺️ Zone-Based Traffic Density
- **C1 Inner Loop**: Low density, tight curves, slower traffic
- **C2 Central**: Medium density, mixed flow
- **Wangan Bayshore**: High density, wide lanes, faster traffic
- **Junctions**: Natural congestion points (Hakozaki, Tatsumi-Kasai)

### 🌙 Time-of-Day Traffic
- Rush hour congestion (07:00-09:00, 17:00-19:00)
- Empty late-night roads (02:00-04:00) - authentic "roulette-zoku" conditions
- Gradual density transitions

### 👤 Driver Personalities
- **Timid**: Slower speeds, larger gaps, less lane changing
- **Normal**: Balanced behavior
- **Aggressive**: Higher speeds, closer following, frequent lane changes
- **Trucks**: Slower acceleration, left-lane preference, speed-limited

## Architecture

```
SXRRealisticTrafficPlugin/
├── Models/
│   ├── IntelligentDriverModel.cs   # IDM car-following
│   ├── MobilLaneChange.cs          # MOBIL lane change decisions
│   └── LaneChangeTrajectory.cs     # Smooth trajectory generation
├── Spatial/
│   └── SpatialGrid.cs              # O(1) neighbor lookups
├── Zones/
│   └── ShutoZones.cs               # Zone definitions & time-of-day
├── TrafficManager.cs               # Main simulation orchestrator
├── TrafficConfiguration.cs         # YAML configuration
└── SXRRealisticTrafficPlugin.cs       # AssettoServer integration
```

## Installation

1. Build the plugin:
```bash
dotnet build SXRRealisticTrafficPlugin.csproj
```

2. Copy the DLL to your AssettoServer plugins folder

3. Add configuration to `extra_cfg.yml`:
```yaml
EnableRealisticTraffic: true
```

4. Create `realistic_traffic_cfg.yml` (see Configuration section)

## Configuration

### Basic Configuration
```yaml
# Density Settings
BaseDensityPerKm: 30
MaxTotalVehicles: 100
VehiclesPerPlayer: 30

# IDM Parameters
DefaultDesiredSpeedKph: 100
DefaultTimeHeadway: 1.2          # seconds
MinimumGap: 2.0                  # meters

# MOBIL Parameters
DefaultPoliteness: 0.25          # 0=selfish, 0.5=cooperative
LaneChangeCooldown: 3.0          # seconds

# Time of Day
EnableTimeOfDayTraffic: true
```

### Zone Overrides
```yaml
ZoneOverrides:
  c1_inner:
    DensityMultiplier: 0.4       # 40% of base density
    SpeedLimitKph: 45
    TruckRatio: 0.05             # Almost no trucks
  wangan:
    DensityMultiplier: 1.2       # 120% of base density
    SpeedLimitKph: 100
```

### Performance Tuning
```yaml
UpdateTickRate: 50               # Hz (50 recommended)
EnableParallelProcessing: true
SpatialCellSize: 200             # meters
```

## How It Works

### Car-Following (IDM)

The Intelligent Driver Model calculates acceleration based on:
- Desired speed vs current speed
- Gap to leading vehicle
- Approach rate (closing speed)

```
acceleration = a × [1 - (v/v₀)^δ - (s*/s)²]
```

Where `s*` is the desired gap considering speed and approach rate.

### Lane Changing (MOBIL)

MOBIL decides lane changes by evaluating:
1. **Safety**: Will the new follower need to brake too hard?
2. **Incentive**: Is my advantage worth the collective disadvantage?

```
change if: (acc_new - acc_current) > p × disadvantage_to_follower + threshold
```

### Smooth Trajectories

Lane changes use quintic polynomial curves:
```
y(t) = lane_width × [10t³ - 15t⁴ + 6t⁵]
```

This ensures zero velocity and acceleration at start/end for natural movement.

## Integration Notes

### Spline System
The plugin needs to convert between:
- **Spline Position**: Linear position along track (meters)
- **World Position**: 3D coordinates for rendering

You'll need to implement `SplineToWorld()` conversion based on your track's `fast_lane.ai` spline.

### Existing AI System
This plugin is designed to **enhance** the existing AssettoServer AI, not replace it. It takes over the behavior logic while using the existing:
- AI slot management
- Network synchronization
- Spline pathfinding

## Performance

- **50Hz update rate**: Smooth movement at high speeds
- **O(1) spatial queries**: Efficient neighbor lookups via grid
- **100+ vehicles**: Tested with 100 AI vehicles
- **~500 Kbps/client**: With batched position updates

## Credits

Based on research from:
- Treiber et al. (2000) - Intelligent Driver Model
- Kesting et al. (2007) - MOBIL lane change model
- Metropolitan Expressway Company - Shuto traffic data

## License

AGPL-3.0 (same as AssettoServer)

## Roadmap

- [ ] Integration testing with live server
- [ ] Fine-tune zone boundaries for Shuto Revival Project
- [ ] Add junction-aware behavior (merge yielding)
- [ ] Weather-based speed adjustments
- [ ] Incident/breakdown events
