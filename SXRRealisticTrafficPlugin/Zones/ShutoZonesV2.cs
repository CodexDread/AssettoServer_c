namespace SXRRealisticTrafficPlugin.Zones;

/// <summary>
/// Shuto Revival Project zone definitions based on actual fast_lane.ai spline analysis.
/// Coordinates extracted from shutoko_revival_project_094_ptb1 splines.
/// 
/// Map bounds: X[-11049, 7190] Z[-10027, 16610] (18.2km x 26.6km)
/// </summary>
public static class ShutoZonesV2
{
    /// <summary>
    /// Get zones configured for Shuto Revival Project splines
    /// </summary>
    public static List<TrafficZone> GetShutoZones()
    {
        return new List<TrafficZone>
        {
            // ================================================================
            // C1/C2 INNER LOOP AREA (fast_lane1-6)
            // Tight 2-lane sections, heavy traffic, slower speeds
            // World bounds: X[-9392, 7190] Z[-4994, 16610]
            // ================================================================
            new TrafficZone
            {
                Id = "c1_inner_loop",
                Name = "C1 Inner Circular Route",
                ZoneType = ShutoZoneType.C1Inner,
                SplineNames = new[] { "fast_lane1.ai", "fast_lane2.ai", "fast_lane3.ai", 
                                      "fast_lane4.ai", "fast_lane5.ai", "fast_lane6.ai" },
                
                // Road characteristics
                LaneCount = 2,
                SpeedLimitKph = 50,
                BaseSpeedKph = 45,
                
                // Lower density on tight curves - feels crowded quickly
                DensityMultiplier = 0.5f,
                MaxVehiclesPerKm = 15,
                SpawnPriority = 0.7f,
                
                // World coordinate bounds
                WorldBounds = new WorldBounds
                {
                    MinX = -9392, MaxX = 7190,
                    MinZ = -4994, MaxZ = 16610
                },
                
                // Driver behavior - more cautious on tight roads
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.1f,     // Few aggressive drivers
                    NormalRatio = 0.6f,
                    TimidRatio = 0.3f,
                    TruckRatio = 0.05f,         // Almost no trucks on C1
                    DesiredSpeedVariance = 0.15f
                }
            },
            
            // ================================================================
            // WANGAN/BAYSHORE ROUTE (fast_lane15-16)
            // Wide 4-6 lane sections, fast traffic, long straights
            // World bounds: X[-11027, 2989] Z[-9241, 16572]
            // ================================================================
            new TrafficZone
            {
                Id = "wangan_bayshore",
                Name = "Wangan Bayshore Route",
                ZoneType = ShutoZoneType.Wangan,
                SplineNames = new[] { "fast_lane15.ai", "fast_lane16.ai" },
                
                LaneCount = 5,
                SpeedLimitKph = 80,
                BaseSpeedKph = 95,  // Drivers typically exceed limit on straights
                
                // Full density - wide road handles more traffic
                DensityMultiplier = 1.0f,
                MaxVehiclesPerKm = 45,
                SpawnPriority = 1.0f,
                
                WorldBounds = new WorldBounds
                {
                    MinX = -11027, MaxX = 2989,
                    MinZ = -9241, MaxZ = 16572
                },
                
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.25f,    // More aggressive on open road
                    NormalRatio = 0.45f,
                    TimidRatio = 0.2f,
                    TruckRatio = 0.1f,
                    DesiredSpeedVariance = 0.25f // More speed variance
                }
            },
            
            // ================================================================
            // ROUTE 9 - TRANSITION ROUTE
            // Connects C1/C2 to outer areas
            // World bounds: X[-11049, 2989] Z[-9224, 16264]
            // ================================================================
            new TrafficZone
            {
                Id = "route_9_transition",
                Name = "Route 9 (Transition)",
                ZoneType = ShutoZoneType.UrbanRoute,
                SplineNames = new[] { "fast_lane9.ai" },
                
                LaneCount = 3,
                SpeedLimitKph = 60,
                BaseSpeedKph = 55,
                
                DensityMultiplier = 0.7f,
                MaxVehiclesPerKm = 25,
                SpawnPriority = 0.8f,
                
                WorldBounds = new WorldBounds
                {
                    MinX = -11049, MaxX = 2989,
                    MinZ = -9224, MaxZ = 16264
                },
                
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.2f,
                    NormalRatio = 0.5f,
                    TimidRatio = 0.3f,
                    TruckRatio = 0.15f,
                    DesiredSpeedVariance = 0.2f
                }
            },
            
            // ================================================================
            // OUTER SECTIONS (fast_lane10-14)
            // Mixed highway, connecting routes
            // World bounds: X[-11045, 6676] Z[-10022, 16586]
            // ================================================================
            new TrafficZone
            {
                Id = "outer_sections",
                Name = "Outer Highway Sections",
                ZoneType = ShutoZoneType.C2Central,
                SplineNames = new[] { "fast_lane10.ai", "fast_lane11.ai", "fast_lane12.ai",
                                      "fast_lane13.ai", "fast_lane14.ai" },
                
                LaneCount = 3,
                SpeedLimitKph = 60,
                BaseSpeedKph = 60,
                
                DensityMultiplier = 0.8f,
                MaxVehiclesPerKm = 30,
                SpawnPriority = 0.85f,
                
                WorldBounds = new WorldBounds
                {
                    MinX = -11045, MaxX = 6676,
                    MinZ = -10022, MaxZ = 16586
                },
                
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.2f,
                    NormalRatio = 0.5f,
                    TimidRatio = 0.3f,
                    TruckRatio = 0.2f,
                    DesiredSpeedVariance = 0.2f
                }
            },
            
            // ================================================================
            // ROUTE 7 - SHORT CONNECTOR
            // Small section, typically less traffic
            // World bounds: X[3757, 6584] Z[-9166, -4791]
            // ================================================================
            new TrafficZone
            {
                Id = "route_7",
                Name = "Route 7 Connector",
                ZoneType = ShutoZoneType.UrbanRoute,
                SplineNames = new[] { "fast_lane7.ai" },
                
                LaneCount = 2,
                SpeedLimitKph = 50,
                BaseSpeedKph = 50,
                
                DensityMultiplier = 0.4f,
                MaxVehiclesPerKm = 12,
                SpawnPriority = 0.5f,
                
                WorldBounds = new WorldBounds
                {
                    MinX = 3757, MaxX = 6584,
                    MinZ = -9166, MaxZ = -4791
                },
                
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.15f,
                    NormalRatio = 0.55f,
                    TimidRatio = 0.3f,
                    TruckRatio = 0.1f,
                    DesiredSpeedVariance = 0.15f
                }
            },
            
            // ================================================================
            // EXTENDED ROUTES (fast_lane17-26)
            // Various smaller connectors and extensions
            // World bounds: X[-5378, 4162] Z[-10027, 1510]
            // ================================================================
            new TrafficZone
            {
                Id = "extended_routes",
                Name = "Extended Route Network",
                ZoneType = ShutoZoneType.UrbanRoute,
                SplineNames = new[] { "fast_lane17.ai", "fast_lane18.ai", "fast_lane19.ai",
                                      "fast_lane20.ai", "fast_lane21.ai", "fast_lane22.ai",
                                      "fast_lane23.ai", "fast_lane24.ai", "fast_lane25.ai",
                                      "fast_lane26.ai" },
                
                LaneCount = 2,
                SpeedLimitKph = 50,
                BaseSpeedKph = 45,
                
                DensityMultiplier = 0.4f,
                MaxVehiclesPerKm = 10,
                SpawnPriority = 0.4f,
                
                WorldBounds = new WorldBounds
                {
                    MinX = -5378, MaxX = 4162,
                    MinZ = -10027, MaxZ = 1510
                },
                
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.1f,
                    NormalRatio = 0.6f,
                    TimidRatio = 0.3f,
                    TruckRatio = 0.15f,
                    DesiredSpeedVariance = 0.15f
                }
            },
            
            // ================================================================
            // JUNCTION AREAS (lj splines - 78 junction splines total)
            // Merge/split points, naturally congested
            // ================================================================
            new TrafficZone
            {
                Id = "junction_areas",
                Name = "Junction/Merge Areas",
                ZoneType = ShutoZoneType.Junction,
                // Junction splines are fast_lanelj1.ai through fast_lanelj78.ai
                SplineNamePattern = "fast_lanelj*.ai",
                
                LaneCount = 3,
                SpeedLimitKph = 40,
                BaseSpeedKph = 30,
                IsCongestedArea = true,
                
                // Lower spawn here - traffic flows in from main routes
                DensityMultiplier = 0.6f,
                MaxVehiclesPerKm = 40,
                SpawnPriority = 0.3f,
                
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.1f,
                    NormalRatio = 0.5f,
                    TimidRatio = 0.4f,
                    TruckRatio = 0.15f,
                    DesiredSpeedVariance = 0.1f // Less variance at junctions
                }
            },
            
            // ================================================================
            // EXIT RAMPS (a, b, e splines)
            // Off-ramp traffic leaving the expressway
            // ================================================================
            new TrafficZone
            {
                Id = "exit_ramps",
                Name = "Exit Ramps",
                ZoneType = ShutoZoneType.OffRamp,
                SplineNamePattern = "fast_lanea*.ai|fast_laneb*.ai|fast_lanee*.ai",
                
                LaneCount = 1,
                SpeedLimitKph = 40,
                BaseSpeedKph = 35,
                
                // Very low spawn - vehicles exit via these
                DensityMultiplier = 0.2f,
                MaxVehiclesPerKm = 5,
                SpawnPriority = 0.1f,
                
                DefaultDriverProfile = new ZoneDriverProfile
                {
                    AggressiveRatio = 0.05f,
                    NormalRatio = 0.6f,
                    TimidRatio = 0.35f,
                    TruckRatio = 0.2f,
                    DesiredSpeedVariance = 0.1f
                }
            }
        };
    }
    
    /// <summary>
    /// Find zone by spline name
    /// </summary>
    public static TrafficZone? GetZoneForSpline(string splineName, List<TrafficZone> zones)
    {
        foreach (var zone in zones)
        {
            if (zone.SplineNames != null && zone.SplineNames.Contains(splineName))
                return zone;
            
            if (!string.IsNullOrEmpty(zone.SplineNamePattern))
            {
                // Simple pattern matching (supports * wildcard and | for OR)
                foreach (var pattern in zone.SplineNamePattern.Split('|'))
                {
                    var regex = "^" + pattern.Replace(".", "\\.").Replace("*", ".*") + "$";
                    if (System.Text.RegularExpressions.Regex.IsMatch(splineName, regex))
                        return zone;
                }
            }
        }
        return null;
    }
    
    /// <summary>
    /// Find zone by world coordinates
    /// </summary>
    public static TrafficZone? GetZoneForPosition(float worldX, float worldZ, List<TrafficZone> zones)
    {
        foreach (var zone in zones)
        {
            if (zone.WorldBounds != null && zone.WorldBounds.Contains(worldX, worldZ))
                return zone;
        }
        return null;
    }
}

/// <summary>
/// Extended TrafficZone with spline-based configuration
/// </summary>
public partial class TrafficZone
{
    /// <summary>Specific spline file names this zone covers</summary>
    public string[]? SplineNames { get; set; }
    
    /// <summary>Pattern for matching spline names (supports * wildcard)</summary>
    public string? SplineNamePattern { get; set; }
    
    /// <summary>World coordinate bounds for this zone</summary>
    public WorldBounds? WorldBounds { get; set; }
    
    /// <summary>
    /// Check if a world position is within this zone
    /// </summary>
    public bool ContainsWorldPosition(float x, float z)
    {
        return WorldBounds?.Contains(x, z) ?? false;
    }
}

/// <summary>
/// World coordinate bounds
/// </summary>
public class WorldBounds
{
    public float MinX { get; set; }
    public float MaxX { get; set; }
    public float MinZ { get; set; }
    public float MaxZ { get; set; }
    
    public bool Contains(float x, float z)
    {
        return x >= MinX && x <= MaxX && z >= MinZ && z <= MaxZ;
    }
    
    public float CenterX => (MinX + MaxX) / 2f;
    public float CenterZ => (MinZ + MaxZ) / 2f;
    public float Width => MaxX - MinX;
    public float Height => MaxZ - MinZ;
}

/// <summary>
/// Spline-to-Zone mapping helper for runtime lookups
/// </summary>
public class SplineZoneMapper
{
    private readonly Dictionary<string, TrafficZone> _splineToZone = new();
    private readonly List<TrafficZone> _zones;
    
    public SplineZoneMapper(List<TrafficZone> zones)
    {
        _zones = zones;
        BuildMapping();
    }
    
    private void BuildMapping()
    {
        foreach (var zone in _zones)
        {
            if (zone.SplineNames != null)
            {
                foreach (var spline in zone.SplineNames)
                {
                    _splineToZone[spline] = zone;
                }
            }
        }
    }
    
    public TrafficZone? GetZone(string splineName)
    {
        if (_splineToZone.TryGetValue(splineName, out var zone))
            return zone;
        
        // Try pattern matching
        return ShutoZonesV2.GetZoneForSpline(splineName, _zones);
    }
    
    public TrafficZone? GetZoneAtPosition(float worldX, float worldZ)
    {
        return ShutoZonesV2.GetZoneForPosition(worldX, worldZ, _zones);
    }
}
