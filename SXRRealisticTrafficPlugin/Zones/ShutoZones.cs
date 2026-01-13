namespace RealisticTrafficPlugin.Zones;

/// <summary>
/// Defines traffic zones on Shuto Expressway with varying density and behavior parameters.
/// Based on real Metropolitan Expressway Company traffic data.
/// </summary>
public static class ShutoZones
{
    /// <summary>
    /// Get all predefined Shuto Expressway zones
    /// </summary>
    public static List<TrafficZone> GetDefaultZones()
    {
        return new List<TrafficZone>
        {
            // === C1 Inner Circular Route ===
            // Tight 2-lane sections, 50 km/h speed limits
            // 100,000+ vehicles/day but spread across the loop
            new TrafficZone
            {
                Id = "c1_inner",
                Name = "C1 Inner Loop",
                ZoneType = ShutoZoneType.C1Inner,
                LaneCount = 2,
                SpeedLimitKph = 50,
                BaseSpeedKph = 45,
                DensityMultiplier = 0.5f,        // Lower density due to tight curves
                MaxVehiclesPerKm = 15,
                SpawnPriority = 0.6f,
                
                // Tight curves encourage slower, more cautious driving
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.1f,
                    NormalRatio = 0.6f,
                    TimidRatio = 0.3f,
                    TruckRatio = 0.1f,           // Few trucks on C1
                    DesiredSpeedVariance = 0.15f // Less variance on tight road
                },
                
                // C1 covers roughly spline positions 0-15000 (example values)
                SplineRanges = new List<SplineRange>
                {
                    new SplineRange { Start = 0, End = 15000 }
                }
            },
            
            // === C2 Central Circular Route ===
            // 2-3 lane sections, better flow than C1
            new TrafficZone
            {
                Id = "c2_central",
                Name = "C2 Central Loop",
                ZoneType = ShutoZoneType.C2Central,
                LaneCount = 3,
                SpeedLimitKph = 60,
                BaseSpeedKph = 55,
                DensityMultiplier = 0.7f,
                MaxVehiclesPerKm = 25,
                SpawnPriority = 0.7f,
                
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.2f,
                    NormalRatio = 0.5f,
                    TimidRatio = 0.3f,
                    TruckRatio = 0.15f,
                    DesiredSpeedVariance = 0.2f
                },
                
                SplineRanges = new List<SplineRange>
                {
                    new SplineRange { Start = 15001, End = 35000 }
                }
            },
            
            // === Wangan Line (Bayshore Route) ===
            // Wide 4-6 lane sections, 80 km/h limit
            // Heaviest absolute volume but spreads across many lanes
            new TrafficZone
            {
                Id = "wangan",
                Name = "Wangan Bayshore Route",
                ZoneType = ShutoZoneType.Wangan,
                LaneCount = 5,  // Average across sections
                SpeedLimitKph = 80,
                BaseSpeedKph = 90, // Drivers typically exceed limit
                DensityMultiplier = 1.0f,       // Full density
                MaxVehiclesPerKm = 50,
                SpawnPriority = 1.0f,           // Highest spawn priority
                
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.25f,    // More aggressive on straights
                    NormalRatio = 0.45f,
                    TimidRatio = 0.2f,
                    TruckRatio = 0.1f,
                    DesiredSpeedVariance = 0.25f // More variance allowed
                },
                
                // Big main stretch
                SplineRanges = new List<SplineRange>
                {
                    new SplineRange { Start = 35001, End = 70000 }
                }
            },
            
            // === Shibuya Route (Route 3) ===
            // Mixed urban highway, variable lanes
            new TrafficZone
            {
                Id = "shibuya_route",
                Name = "Shibuya Route 3",
                ZoneType = ShutoZoneType.UrbanRoute,
                LaneCount = 3,
                SpeedLimitKph = 60,
                BaseSpeedKph = 50,
                DensityMultiplier = 0.8f,
                MaxVehiclesPerKm = 30,
                SpawnPriority = 0.8f,
                
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.15f,
                    NormalRatio = 0.55f,
                    TimidRatio = 0.3f,
                    TruckRatio = 0.2f,          // More delivery trucks
                    DesiredSpeedVariance = 0.2f
                },
                
                SplineRanges = new List<SplineRange>
                {
                    new SplineRange { Start = 70001, End = 90000 }
                }
            },
            
            // === Junction/Merge Zones ===
            // High congestion areas: Hakozaki, Tatsumi-Kasai, Horikiri-Kosuge
            new TrafficZone
            {
                Id = "hakozaki_junction",
                Name = "Hakozaki Junction",
                ZoneType = ShutoZoneType.Junction,
                LaneCount = 4,
                SpeedLimitKph = 40,
                BaseSpeedKph = 25,             // Often congested
                DensityMultiplier = 1.2f,       // Higher density due to merging
                MaxVehiclesPerKm = 60,
                SpawnPriority = 0.4f,           // Lower spawn priority (traffic flows in)
                IsCongestedArea = true,
                
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.1f,     // More cautious in junctions
                    NormalRatio = 0.5f,
                    TimidRatio = 0.4f,
                    TruckRatio = 0.25f,
                    DesiredSpeedVariance = 0.1f // Tight spacing, less variance
                },
                
                SplineRanges = new List<SplineRange>
                {
                    new SplineRange { Start = 12000, End = 14000 }
                }
            }
        };
    }
}

/// <summary>
/// Represents a traffic zone with specific characteristics
/// </summary>
public class TrafficZone
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public ShutoZoneType ZoneType { get; set; }
    
    // Road characteristics
    public int LaneCount { get; set; }
    public int SpeedLimitKph { get; set; }
    public int BaseSpeedKph { get; set; }
    
    // Density parameters
    public float DensityMultiplier { get; set; } = 1.0f;
    public int MaxVehiclesPerKm { get; set; } = 30;
    public float SpawnPriority { get; set; } = 1.0f;
    public bool IsCongestedArea { get; set; }
    
    // Driver behavior
    public ZoneDriverProfile DefaultDriverProfile { get; set; } = new();
    
    // Spline position ranges this zone covers
    public List<SplineRange> SplineRanges { get; set; } = new();
    
    /// <summary>
    /// Check if a spline position is within this zone
    /// </summary>
    public bool ContainsPosition(float splinePosition)
    {
        return SplineRanges.Any(r => splinePosition >= r.Start && splinePosition <= r.End);
    }
    
    /// <summary>
    /// Get the target speed for this zone in m/s
    /// </summary>
    public float GetTargetSpeedMs() => BaseSpeedKph / 3.6f;
    
    /// <summary>
    /// Get desired speed for a vehicle spawning in this zone
    /// </summary>
    public float GetRandomizedSpeedMs(Random rng)
    {
        float baseMs = BaseSpeedKph / 3.6f;
        float variance = DefaultDriverProfile.DesiredSpeedVariance;
        float factor = 1.0f + (float)(rng.NextDouble() * 2 - 1) * variance;
        return baseMs * factor;
    }
}

/// <summary>
/// Driver behavior profile for a zone
/// </summary>
public class ZoneDriverProfile
{
    public float AggressiveRatio { get; set; } = 0.2f;
    public float NormalRatio { get; set; } = 0.5f;
    public float TimidRatio { get; set; } = 0.3f;
    public float TruckRatio { get; set; } = 0.15f;
    
    /// <summary>
    /// How much desired speed can vary from base (0.0-1.0)
    /// </summary>
    public float DesiredSpeedVariance { get; set; } = 0.2f;
    
    /// <summary>
    /// Select a random personality based on zone ratios
    /// </summary>
    public Models.DriverPersonality GetRandomPersonality(Random rng)
    {
        float roll = (float)rng.NextDouble();
        
        if (roll < TimidRatio)
            return Models.DriverPersonality.Timid;
        if (roll < TimidRatio + NormalRatio)
            return Models.DriverPersonality.Normal;
        if (roll < TimidRatio + NormalRatio + AggressiveRatio)
            return Models.DriverPersonality.Aggressive;
        
        return Models.DriverPersonality.Normal;
    }
    
    /// <summary>
    /// Determine if spawned vehicle should be a truck
    /// </summary>
    public bool ShouldSpawnTruck(Random rng)
    {
        return rng.NextDouble() < TruckRatio;
    }
}

public class SplineRange
{
    public float Start { get; set; }
    public float End { get; set; }
    
    public float Length => End - Start;
    public float Center => (Start + End) / 2f;
}

public enum ShutoZoneType
{
    C1Inner,        // Tight inner loop
    C2Central,      // Central circular
    Wangan,         // Wide bayshore route
    UrbanRoute,     // Mixed urban sections
    Junction,       // Merge/junction areas
    OnRamp,         // On-ramp areas
    OffRamp,        // Off-ramp areas
    Tunnel,         // Tunnel sections (may need special handling)
    ServiceArea     // PA/SA areas
}

/// <summary>
/// Time-of-day traffic modifiers based on real Shuto data
/// </summary>
public static class TimeOfDayTraffic
{
    /// <summary>
    /// Get density multiplier based on time of day
    /// </summary>
    public static float GetDensityMultiplier(int hour)
    {
        return hour switch
        {
            // Late night (02:00-04:00) - Lowest traffic, "roulette-zoku" era
            >= 2 and < 4 => 0.05f,
            
            // Early morning (04:00-06:00)
            >= 4 and < 6 => 0.15f,
            
            // Morning rush (07:00-09:00)
            >= 7 and < 9 => 1.0f,
            
            // Mid-morning (09:00-11:00)
            >= 9 and < 11 => 0.7f,
            
            // Lunch dip (13:00-14:00) - Lowest daytime
            >= 13 and < 14 => 0.5f,
            
            // Afternoon (14:00-17:00)
            >= 14 and < 17 => 0.65f,
            
            // Evening rush (17:00-19:00)
            >= 17 and < 19 => 1.0f,
            
            // Evening (19:00-22:00)
            >= 19 and < 22 => 0.6f,
            
            // Night (22:00-02:00)
            >= 22 or < 2 => 0.25f,
            
            // Default
            _ => 0.5f
        };
    }
    
    /// <summary>
    /// Get speed multiplier based on time of day
    /// (Traffic flows faster at night, slower during rush)
    /// </summary>
    public static float GetSpeedMultiplier(int hour)
    {
        return hour switch
        {
            // Late night - Fast empty roads
            >= 2 and < 5 => 1.3f,
            
            // Rush hours - Slow congestion
            >= 7 and < 9 => 0.6f,
            >= 17 and < 19 => 0.6f,
            
            // Normal hours
            _ => 1.0f
        };
    }
}
