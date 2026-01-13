using System.Collections.Concurrent;
using System.Numerics;
using RealisticTrafficPlugin.Models;
using RealisticTrafficPlugin.Spatial;
using RealisticTrafficPlugin.Zones;

namespace RealisticTrafficPlugin;

/// <summary>
/// Main traffic simulation manager.
/// Coordinates IDM car-following, MOBIL lane changes, spawning, and updates.
/// </summary>
public class TrafficManager
{
    private readonly TrafficConfiguration _config;
    private readonly SplineGrid _splineGrid;
    private readonly List<TrafficZone> _zones;
    private readonly ConcurrentDictionary<int, TrafficVehicleState> _vehicles = new();
    private readonly Random _rng = new();
    
    private int _nextVehicleId = 1;
    private float _currentTime = 0f;
    private int _currentHour = 12; // Default to noon
    
    // Player tracking for spawn/despawn decisions
    private readonly ConcurrentDictionary<int, PlayerState> _players = new();
    
    public TrafficManager(TrafficConfiguration config)
    {
        _config = config;
        _splineGrid = new SplineGrid(config.SpatialCellSize, config.MaxLaneCount);
        _zones = ShutoZones.GetDefaultZones();
    }
    
    /// <summary>
    /// Main update loop - call at 50Hz (20ms intervals)
    /// </summary>
    public void Update(float deltaTime)
    {
        _currentTime += deltaTime;
        
        // Update all vehicles
        foreach (var kvp in _vehicles)
        {
            UpdateVehicle(kvp.Value, deltaTime);
        }
        
        // Rebuild spatial grid after position updates
        RebuildSpatialGrid();
        
        // Update leader/follower references
        UpdateVehicleRelationships();
        
        // Spawn/despawn traffic around players
        ManageTrafficPopulation();
    }
    
    /// <summary>
    /// Update a single vehicle's state
    /// </summary>
    private void UpdateVehicle(TrafficVehicleState state, float deltaTime)
    {
        var vehicle = state.Vehicle;
        
        // === LANE CHANGE LOGIC ===
        if (state.LaneChangeState.IsActive)
        {
            // Continue active lane change
            state.LaneChangeState.Update(_currentTime);
            
            if (!state.LaneChangeState.IsActive)
            {
                // Lane change completed
                vehicle.CurrentLane = state.LaneChangeState.TargetLane;
                vehicle.LastLaneChangeTime = _currentTime;
            }
        }
        else if (CanConsiderLaneChange(vehicle))
        {
            // Evaluate potential lane change using MOBIL
            var decision = MobilLaneChange.EvaluateLaneChange(
                vehicle,
                vehicle.Leader,
                GetLeaderInLane(vehicle, vehicle.CurrentLane - 1),
                GetFollowerInLane(vehicle, vehicle.CurrentLane - 1),
                GetLeaderInLane(vehicle, vehicle.CurrentLane + 1),
                GetFollowerInLane(vehicle, vehicle.CurrentLane + 1),
                vehicle.MobilParams,
                isLeftHandTraffic: true);
            
            if (decision != null)
            {
                int targetLane = decision.Direction == LaneChangeDirection.Left 
                    ? vehicle.CurrentLane - 1 
                    : vehicle.CurrentLane + 1;
                
                // Validate lane exists
                var zone = GetZoneForPosition(vehicle.SplinePosition);
                if (targetLane >= 0 && targetLane < (zone?.LaneCount ?? _config.MaxLaneCount))
                {
                    state.LaneChangeState.StartLaneChange(
                        vehicle.CurrentLane,
                        targetLane,
                        _currentTime,
                        vehicle.Speed,
                        _config.LaneWidth);
                }
            }
        }
        
        // === IDM ACCELERATION ===
        float acceleration;
        if (vehicle.Leader != null)
        {
            float gap = vehicle.Leader.SplinePosition - vehicle.SplinePosition - vehicle.Leader.Length;
            float approachingRate = vehicle.Speed - vehicle.Leader.Speed;
            
            acceleration = IntelligentDriverModel.CalculateAcceleration(
                vehicle.Speed,
                vehicle.DriverParams.DesiredSpeed,
                gap,
                approachingRate,
                vehicle.DriverParams);
        }
        else
        {
            acceleration = IntelligentDriverModel.CalculateFreeRoadAcceleration(
                vehicle.Speed,
                vehicle.DriverParams.DesiredSpeed,
                vehicle.DriverParams);
        }
        
        // === UPDATE KINEMATICS ===
        vehicle.Speed = MathF.Max(0, vehicle.Speed + acceleration * deltaTime);
        vehicle.SplinePosition += vehicle.Speed * deltaTime;
        
        // Handle spline wraparound if needed
        if (vehicle.SplinePosition > _config.SplineLength)
        {
            vehicle.SplinePosition -= _config.SplineLength;
        }
        
        state.LastAcceleration = acceleration;
    }
    
    private bool CanConsiderLaneChange(TrafficVehicle vehicle)
    {
        // Cooldown since last lane change
        if (_currentTime - vehicle.LastLaneChangeTime < vehicle.MobilParams.LaneChangeCooldown)
            return false;
        
        // Not already changing lanes
        if (vehicle.IsChangingLanes)
            return false;
        
        return true;
    }
    
    /// <summary>
    /// Spawn traffic around players based on zone density
    /// </summary>
    private void ManageTrafficPopulation()
    {
        foreach (var playerKvp in _players)
        {
            var player = playerKvp.Value;
            
            // Calculate spawn region
            float spawnAhead = _config.SpawnDistanceAhead;
            float spawnBehind = _config.SpawnDistanceBehind;
            float despawnDistance = _config.DespawnDistance;
            
            float spawnStart = player.SplinePosition - spawnBehind;
            float spawnEnd = player.SplinePosition + spawnAhead;
            
            // Get zone for player position
            var zone = GetZoneForPosition(player.SplinePosition);
            if (zone == null) continue;
            
            // Calculate target density
            float timeDensityMod = TimeOfDayTraffic.GetDensityMultiplier(_currentHour);
            float zoneDensity = zone.DensityMultiplier;
            float targetDensityPerKm = zone.MaxVehiclesPerKm * zoneDensity * timeDensityMod;
            
            // Count current vehicles in spawn region
            float rangeKm = (spawnEnd - spawnStart) / 1000f;
            int targetCount = (int)(targetDensityPerKm * rangeKm);
            int currentCount = _splineGrid.CountVehiclesInRange(spawnStart, spawnEnd);
            
            // Spawn if below target
            if (currentCount < targetCount)
            {
                int toSpawn = Math.Min(targetCount - currentCount, _config.MaxSpawnsPerTick);
                for (int i = 0; i < toSpawn; i++)
                {
                    TrySpawnVehicle(player, zone);
                }
            }
            
            // Despawn vehicles too far from any player
            DespawnDistantVehicles(despawnDistance);
        }
    }
    
    private void TrySpawnVehicle(PlayerState player, TrafficZone zone)
    {
        // Find spawn position outside player's view
        float spawnDist = _config.SpawnDistanceAhead + _rng.NextSingle() * 500f;
        float spawnPos = player.SplinePosition + spawnDist;
        
        // Check frustum culling (basic version - spawn behind or far ahead)
        if (!IsOutsideViewFrustum(player, spawnPos))
        {
            // Try behind instead
            spawnPos = player.SplinePosition - _config.SpawnDistanceBehind - _rng.NextSingle() * 300f;
        }
        
        if (spawnPos < 0) spawnPos += _config.SplineLength;
        if (spawnPos > _config.SplineLength) spawnPos -= _config.SplineLength;
        
        // Check for minimum gap from existing vehicles
        var nearby = _splineGrid.GetVehiclesInRange(spawnPos - 50, spawnPos + 50);
        if (nearby.Count > 0) return; // Too crowded
        
        // Create vehicle
        var personality = zone.DefaultDriverProfile.GetRandomPersonality(_rng);
        bool isTruck = zone.DefaultDriverProfile.ShouldSpawnTruck(_rng);
        
        var vehicle = new TrafficVehicle
        {
            Id = _nextVehicleId++,
            SplinePosition = spawnPos,
            CurrentLane = _rng.Next(0, zone.LaneCount),
            Speed = zone.GetRandomizedSpeedMs(_rng),
            Length = isTruck ? 12f : 4.5f,
            DriverParams = isTruck 
                ? DriverParameters.CreateTruck(personality)
                : DriverParameters.CreateCar(personality),
            MobilParams = personality switch
            {
                DriverPersonality.Timid => MobilParameters.Timid,
                DriverPersonality.Aggressive => MobilParameters.Aggressive,
                DriverPersonality.VeryAggressive => MobilParameters.Aggressive,
                _ => MobilParameters.Default
            }
        };
        
        var state = new TrafficVehicleState
        {
            Vehicle = vehicle,
            LaneChangeState = new LaneChangeState(),
            IsTruck = isTruck,
            Personality = personality,
            SpawnZone = zone.Id
        };
        
        _vehicles[vehicle.Id] = state;
        _splineGrid.Add(vehicle);
    }
    
    private bool IsOutsideViewFrustum(PlayerState player, float spawnPos)
    {
        float distance = spawnPos - player.SplinePosition;
        if (distance < 0) distance += _config.SplineLength;
        
        // Simple check: spawn at least 500m ahead
        return distance > 500f || distance < -200f;
    }
    
    private void DespawnDistantVehicles(float despawnDistance)
    {
        var toDespawn = new List<int>();
        
        foreach (var kvp in _vehicles)
        {
            var vehicle = kvp.Value.Vehicle;
            
            bool farFromAllPlayers = true;
            foreach (var playerKvp in _players)
            {
                float dist = MathF.Abs(vehicle.SplinePosition - playerKvp.Value.SplinePosition);
                if (dist < despawnDistance)
                {
                    farFromAllPlayers = false;
                    break;
                }
            }
            
            if (farFromAllPlayers)
            {
                toDespawn.Add(kvp.Key);
            }
        }
        
        foreach (var id in toDespawn)
        {
            if (_vehicles.TryRemove(id, out var state))
            {
                _splineGrid.Remove(state.Vehicle);
            }
        }
    }
    
    private void RebuildSpatialGrid()
    {
        _splineGrid.Rebuild(_vehicles.Values.Select(s => s.Vehicle));
    }
    
    private void UpdateVehicleRelationships()
    {
        foreach (var kvp in _vehicles)
        {
            var vehicle = kvp.Value.Vehicle;
            vehicle.Leader = _splineGrid.GetLeader(vehicle);
            vehicle.Follower = _splineGrid.GetFollower(vehicle);
        }
    }
    
    private TrafficVehicle? GetLeaderInLane(TrafficVehicle vehicle, int lane)
    {
        if (lane < 0 || lane >= _config.MaxLaneCount) return null;
        return _splineGrid.GetLeaderInLane(vehicle, lane);
    }
    
    private TrafficVehicle? GetFollowerInLane(TrafficVehicle vehicle, int lane)
    {
        if (lane < 0 || lane >= _config.MaxLaneCount) return null;
        return _splineGrid.GetFollowerInLane(vehicle, lane);
    }
    
    private TrafficZone? GetZoneForPosition(float splinePosition)
    {
        return _zones.FirstOrDefault(z => z.ContainsPosition(splinePosition));
    }
    
    // === PUBLIC API ===
    
    /// <summary>
    /// Update player position (call from main server)
    /// </summary>
    public void UpdatePlayerPosition(int playerId, float splinePosition, Vector3 worldPosition, Vector3 forward)
    {
        _players.AddOrUpdate(playerId,
            _ => new PlayerState { PlayerId = playerId, SplinePosition = splinePosition, WorldPosition = worldPosition, Forward = forward },
            (_, state) => { state.SplinePosition = splinePosition; state.WorldPosition = worldPosition; state.Forward = forward; return state; });
    }
    
    /// <summary>
    /// Remove player tracking
    /// </summary>
    public void RemovePlayer(int playerId)
    {
        _players.TryRemove(playerId, out _);
    }
    
    /// <summary>
    /// Set current hour for time-of-day density
    /// </summary>
    public void SetTimeOfDay(int hour)
    {
        _currentHour = Math.Clamp(hour, 0, 23);
    }
    
    /// <summary>
    /// Get all vehicle states for network sync
    /// </summary>
    public IEnumerable<VehicleNetworkState> GetVehicleStates()
    {
        foreach (var kvp in _vehicles)
        {
            var state = kvp.Value;
            var vehicle = state.Vehicle;
            
            yield return new VehicleNetworkState
            {
                Id = vehicle.Id,
                SplinePosition = vehicle.SplinePosition,
                Lane = vehicle.CurrentLane,
                LateralOffset = state.LaneChangeState.IsActive ? state.LaneChangeState.LateralOffset : 0f,
                Speed = vehicle.Speed,
                IsTruck = state.IsTruck
            };
        }
    }
    
    /// <summary>
    /// Get vehicle count
    /// </summary>
    public int VehicleCount => _vehicles.Count;
}

/// <summary>
/// Internal state tracking for each traffic vehicle
/// </summary>
public class TrafficVehicleState
{
    public TrafficVehicle Vehicle { get; set; } = new();
    public LaneChangeState LaneChangeState { get; set; } = new();
    public float LastAcceleration { get; set; }
    public bool IsTruck { get; set; }
    public DriverPersonality Personality { get; set; }
    public string SpawnZone { get; set; } = "";
}

/// <summary>
/// Player state for spawn/despawn calculations
/// </summary>
public class PlayerState
{
    public int PlayerId { get; set; }
    public float SplinePosition { get; set; }
    public Vector3 WorldPosition { get; set; }
    public Vector3 Forward { get; set; }
}

/// <summary>
/// Network-ready vehicle state for client sync
/// </summary>
public struct VehicleNetworkState
{
    public int Id;
    public float SplinePosition;
    public int Lane;
    public float LateralOffset;
    public float Speed;
    public bool IsTruck;
}
