using JetBrains.Annotations;
using YamlDotNet.Serialization;

namespace SXRSXRRealisticTrafficPlugin;

/// <summary>
/// Configuration for the realistic traffic system.
/// Loaded from YAML configuration file.
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class SXRTrafficConfiguration
{
    // === SPATIAL SETTINGS ===
    
    /// <summary>
    /// Size of spatial grid cells in meters. Smaller = more precise but more memory.
    /// </summary>
    public int SpatialCellSize { get; set; } = 200;
    
    /// <summary>
    /// Maximum number of lanes on any road section
    /// </summary>
    public int MaxLaneCount { get; set; } = 6;
    
    /// <summary>
    /// Lane width in meters
    /// </summary>
    public float LaneWidth { get; set; } = 3.5f;
    
    /// <summary>
    /// Total spline length in meters (for wraparound)
    /// </summary>
    public float SplineLength { get; set; } = 100000f;
    
    // === SPAWN/DESPAWN SETTINGS ===
    
    /// <summary>
    /// Distance ahead of player to spawn traffic (meters)
    /// </summary>
    public float SpawnDistanceAhead { get; set; } = 1000f;
    
    /// <summary>
    /// Distance behind player to maintain traffic (meters)
    /// </summary>
    public float SpawnDistanceBehind { get; set; } = 500f;
    
    /// <summary>
    /// Distance at which to despawn traffic from all players (meters)
    /// </summary>
    public float DespawnDistance { get; set; } = 2000f;
    
    /// <summary>
    /// Minimum gap between spawned vehicles (meters)
    /// </summary>
    public float MinSpawnGap { get; set; } = 50f;
    
    /// <summary>
    /// Maximum vehicles to spawn per update tick
    /// </summary>
    public int MaxSpawnsPerTick { get; set; } = 5;
    
    // === DENSITY SETTINGS ===
    
    /// <summary>
    /// Base vehicles per kilometer (before zone/time modifiers)
    /// </summary>
    public float BaseDensityPerKm { get; set; } = 30f;
    
    /// <summary>
    /// Maximum total AI vehicles
    /// </summary>
    public int MaxTotalVehicles { get; set; } = 100;
    
    /// <summary>
    /// AI vehicles per player (target)
    /// </summary>
    public int VehiclesPerPlayer { get; set; } = 30;
    
    // === IDM DEFAULTS ===
    
    /// <summary>
    /// Default desired speed for cars (km/h)
    /// </summary>
    public float DefaultDesiredSpeedKph { get; set; } = 100f;
    
    /// <summary>
    /// Default desired speed for trucks (km/h)
    /// </summary>
    public float TruckDesiredSpeedKph { get; set; } = 80f;
    
    /// <summary>
    /// Minimum gap at standstill (meters)
    /// </summary>
    public float MinimumGap { get; set; } = 2.0f;
    
    /// <summary>
    /// Default time headway (seconds)
    /// </summary>
    public float DefaultTimeHeadway { get; set; } = 1.2f;
    
    // === MOBIL DEFAULTS ===
    
    /// <summary>
    /// Default politeness factor (0 = selfish, 0.5 = cooperative)
    /// </summary>
    public float DefaultPoliteness { get; set; } = 0.25f;
    
    /// <summary>
    /// Safe deceleration threshold for lane changes (m/s²)
    /// </summary>
    public float SafeDeceleration { get; set; } = 4.0f;
    
    /// <summary>
    /// Minimum acceleration advantage to change lanes (m/s²)
    /// </summary>
    public float LaneChangeThreshold { get; set; } = 0.15f;
    
    /// <summary>
    /// Cooldown between lane change attempts (seconds)
    /// </summary>
    public float LaneChangeCooldown { get; set; } = 3.0f;
    
    // === DRIVER PERSONALITY DISTRIBUTION ===
    
    /// <summary>
    /// Percentage of timid drivers (0.0-1.0)
    /// </summary>
    public float TimidDriverRatio { get; set; } = 0.25f;
    
    /// <summary>
    /// Percentage of normal drivers (0.0-1.0)
    /// </summary>
    public float NormalDriverRatio { get; set; } = 0.50f;
    
    /// <summary>
    /// Percentage of aggressive drivers (0.0-1.0)
    /// </summary>
    public float AggressiveDriverRatio { get; set; } = 0.25f;
    
    /// <summary>
    /// Percentage of vehicles that are trucks (0.0-1.0)
    /// </summary>
    public float TruckRatio { get; set; } = 0.15f;
    
    // === TIME OF DAY ===
    
    /// <summary>
    /// Enable time-of-day traffic density variation
    /// </summary>
    public bool EnableTimeOfDayTraffic { get; set; } = true;
    
    /// <summary>
    /// Minimum density multiplier (late night)
    /// </summary>
    public float MinTimeOfDayDensity { get; set; } = 0.05f;
    
    // === ZONE OVERRIDES ===
    
    /// <summary>
    /// Custom zone configurations (override defaults)
    /// </summary>
    public Dictionary<string, ZoneOverride> ZoneOverrides { get; set; } = new();
    
    // === PERFORMANCE ===
    
    /// <summary>
    /// Update tick rate in Hz (50 recommended)
    /// </summary>
    public int UpdateTickRate { get; set; } = 50;
    
    /// <summary>
    /// Enable parallel processing for vehicle updates
    /// </summary>
    public bool EnableParallelProcessing { get; set; } = true;
    
    /// <summary>
    /// Number of threads for parallel processing (0 = auto)
    /// </summary>
    public int ParallelThreadCount { get; set; } = 0;
    
    // === DEBUG ===
    
    /// <summary>
    /// Enable debug logging
    /// </summary>
    public bool DebugLogging { get; set; } = false;
    
    /// <summary>
    /// Log vehicle spawn/despawn events
    /// </summary>
    public bool LogSpawnEvents { get; set; } = false;
    
    /// <summary>
    /// Log lane change decisions
    /// </summary>
    public bool LogLaneChanges { get; set; } = false;
}

/// <summary>
/// Override settings for a specific zone
/// </summary>
[UsedImplicitly(ImplicitUseKindFlags.Assign, ImplicitUseTargetFlags.WithMembers)]
public class ZoneOverride
{
    public float? DensityMultiplier { get; set; }
    public float? SpeedLimitKph { get; set; }
    public float? TruckRatio { get; set; }
    public int? LaneCount { get; set; }
    public bool? Enabled { get; set; }
}

/// <summary>
/// Example configuration YAML
/// </summary>
public static class ConfigurationExamples
{
    public const string ExampleYaml = @"
# Realistic Traffic Plugin Configuration
# ======================================

# Spatial Settings
SpatialCellSize: 200
MaxLaneCount: 6
LaneWidth: 3.5
SplineLength: 100000

# Spawn/Despawn
SpawnDistanceAhead: 1000
SpawnDistanceBehind: 500
DespawnDistance: 2000
MinSpawnGap: 50
MaxSpawnsPerTick: 5

# Density
BaseDensityPerKm: 30
MaxTotalVehicles: 100
VehiclesPerPlayer: 30

# IDM Parameters
DefaultDesiredSpeedKph: 100
TruckDesiredSpeedKph: 80
MinimumGap: 2.0
DefaultTimeHeadway: 1.2

# MOBIL Parameters  
DefaultPoliteness: 0.25
SafeDeceleration: 4.0
LaneChangeThreshold: 0.15
LaneChangeCooldown: 3.0

# Driver Distribution
TimidDriverRatio: 0.25
NormalDriverRatio: 0.50
AggressiveDriverRatio: 0.25
TruckRatio: 0.15

# Time of Day
EnableTimeOfDayTraffic: true
MinTimeOfDayDensity: 0.05

# Zone Overrides (optional)
ZoneOverrides:
  c1_inner:
    DensityMultiplier: 0.4      # Even lower for tight curves
    SpeedLimitKph: 45
    TruckRatio: 0.05            # Almost no trucks on C1
  wangan:
    DensityMultiplier: 1.2      # Higher density on wide road
    SpeedLimitKph: 100          # People drive faster here
  hakozaki_junction:
    DensityMultiplier: 0.8      # Reduce spawns at congested junction

# Performance
UpdateTickRate: 50
EnableParallelProcessing: true
ParallelThreadCount: 0          # Auto-detect

# Debug (disable in production)
DebugLogging: false
LogSpawnEvents: false
LogLaneChanges: false
";
}
