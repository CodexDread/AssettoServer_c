using System.Collections.Concurrent;
using System.Numerics;
using RealisticTrafficPlugin.Models;

namespace RealisticTrafficPlugin.Spatial;

/// <summary>
/// Spatial grid for efficient O(1) average-case vehicle lookups.
/// Uses a 2D grid with configurable cell size.
/// </summary>
public class SpatialGrid
{
    private readonly ConcurrentDictionary<long, List<TrafficVehicle>> _cells = new();
    private readonly int _cellSize;
    private readonly object _updateLock = new();
    
    public SpatialGrid(int cellSizeMeters = 200)
    {
        _cellSize = cellSizeMeters;
    }
    
    /// <summary>
    /// Compute cell key from world position
    /// </summary>
    private long GetCellKey(float x, float z)
    {
        int cellX = (int)MathF.Floor(x / _cellSize);
        int cellZ = (int)MathF.Floor(z / _cellSize);
        return ((long)cellX << 32) | (uint)cellZ;
    }
    
    /// <summary>
    /// Add a vehicle to the grid
    /// </summary>
    public void Add(TrafficVehicle vehicle, Vector3 position)
    {
        var key = GetCellKey(position.X, position.Z);
        _cells.AddOrUpdate(
            key,
            _ => new List<TrafficVehicle> { vehicle },
            (_, list) => { lock (list) { list.Add(vehicle); } return list; });
    }
    
    /// <summary>
    /// Remove a vehicle from the grid
    /// </summary>
    public void Remove(TrafficVehicle vehicle, Vector3 position)
    {
        var key = GetCellKey(position.X, position.Z);
        if (_cells.TryGetValue(key, out var list))
        {
            lock (list) { list.Remove(vehicle); }
        }
    }
    
    /// <summary>
    /// Update vehicle position (remove from old cell, add to new)
    /// </summary>
    public void Update(TrafficVehicle vehicle, Vector3 oldPosition, Vector3 newPosition)
    {
        var oldKey = GetCellKey(oldPosition.X, oldPosition.Z);
        var newKey = GetCellKey(newPosition.X, newPosition.Z);
        
        if (oldKey != newKey)
        {
            Remove(vehicle, oldPosition);
            Add(vehicle, newPosition);
        }
    }
    
    /// <summary>
    /// Get all vehicles within radius of a position
    /// </summary>
    public List<TrafficVehicle> GetNearbyVehicles(Vector3 position, float radius)
    {
        var results = new List<TrafficVehicle>();
        int cellRadius = (int)MathF.Ceiling(radius / _cellSize);
        float radiusSq = radius * radius;
        
        int baseCellX = (int)MathF.Floor(position.X / _cellSize);
        int baseCellZ = (int)MathF.Floor(position.Z / _cellSize);
        
        for (int dx = -cellRadius; dx <= cellRadius; dx++)
        {
            for (int dz = -cellRadius; dz <= cellRadius; dz++)
            {
                long key = ((long)(baseCellX + dx) << 32) | (uint)(baseCellZ + dz);
                if (_cells.TryGetValue(key, out var cell))
                {
                    lock (cell)
                    {
                        results.AddRange(cell);
                    }
                }
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Clear all vehicles from the grid
    /// </summary>
    public void Clear()
    {
        _cells.Clear();
    }
    
    /// <summary>
    /// Get total vehicle count
    /// </summary>
    public int Count => _cells.Values.Sum(list => { lock (list) { return list.Count; } });
}

/// <summary>
/// 1D spline-based grid for track-aware spatial queries.
/// More efficient for linear tracks like highways.
/// </summary>
public class SplineGrid
{
    private readonly ConcurrentDictionary<int, Dictionary<int, List<TrafficVehicle>>> _cells = new();
    private readonly float _cellLength;
    private readonly int _laneCount;
    
    public SplineGrid(float cellLengthMeters = 200f, int laneCount = 5)
    {
        _cellLength = cellLengthMeters;
        _laneCount = laneCount;
    }
    
    private int GetSplineCell(float splinePosition) => (int)MathF.Floor(splinePosition / _cellLength);
    
    /// <summary>
    /// Add vehicle to spline grid
    /// </summary>
    public void Add(TrafficVehicle vehicle)
    {
        int cell = GetSplineCell(vehicle.SplinePosition);
        int lane = Math.Clamp(vehicle.CurrentLane, 0, _laneCount - 1);
        
        var laneDict = _cells.GetOrAdd(cell, _ => new Dictionary<int, List<TrafficVehicle>>());
        lock (laneDict)
        {
            if (!laneDict.TryGetValue(lane, out var list))
            {
                list = new List<TrafficVehicle>();
                laneDict[lane] = list;
            }
            list.Add(vehicle);
        }
    }
    
    /// <summary>
    /// Remove vehicle from spline grid
    /// </summary>
    public void Remove(TrafficVehicle vehicle)
    {
        int cell = GetSplineCell(vehicle.SplinePosition);
        int lane = Math.Clamp(vehicle.CurrentLane, 0, _laneCount - 1);
        
        if (_cells.TryGetValue(cell, out var laneDict))
        {
            lock (laneDict)
            {
                if (laneDict.TryGetValue(lane, out var list))
                {
                    list.Remove(vehicle);
                }
            }
        }
    }
    
    /// <summary>
    /// Get the leader (vehicle ahead) in the same lane
    /// </summary>
    public TrafficVehicle? GetLeader(TrafficVehicle vehicle, float maxDistance = 500f)
    {
        int startCell = GetSplineCell(vehicle.SplinePosition);
        int endCell = GetSplineCell(vehicle.SplinePosition + maxDistance);
        int lane = vehicle.CurrentLane;
        
        TrafficVehicle? closest = null;
        float closestDist = float.MaxValue;
        
        for (int cell = startCell; cell <= endCell; cell++)
        {
            if (_cells.TryGetValue(cell, out var laneDict))
            {
                lock (laneDict)
                {
                    if (laneDict.TryGetValue(lane, out var list))
                    {
                        foreach (var other in list)
                        {
                            if (other.Id == vehicle.Id) continue;
                            
                            float dist = other.SplinePosition - vehicle.SplinePosition;
                            if (dist > 0 && dist < closestDist)
                            {
                                closest = other;
                                closestDist = dist;
                            }
                        }
                    }
                }
            }
        }
        
        return closest;
    }
    
    /// <summary>
    /// Get the follower (vehicle behind) in the same lane
    /// </summary>
    public TrafficVehicle? GetFollower(TrafficVehicle vehicle, float maxDistance = 500f)
    {
        int startCell = GetSplineCell(vehicle.SplinePosition - maxDistance);
        int endCell = GetSplineCell(vehicle.SplinePosition);
        int lane = vehicle.CurrentLane;
        
        TrafficVehicle? closest = null;
        float closestDist = float.MaxValue;
        
        for (int cell = startCell; cell <= endCell; cell++)
        {
            if (_cells.TryGetValue(cell, out var laneDict))
            {
                lock (laneDict)
                {
                    if (laneDict.TryGetValue(lane, out var list))
                    {
                        foreach (var other in list)
                        {
                            if (other.Id == vehicle.Id) continue;
                            
                            float dist = vehicle.SplinePosition - other.SplinePosition;
                            if (dist > 0 && dist < closestDist)
                            {
                                closest = other;
                                closestDist = dist;
                            }
                        }
                    }
                }
            }
        }
        
        return closest;
    }
    
    /// <summary>
    /// Get leader in adjacent lane (for lane change evaluation)
    /// </summary>
    public TrafficVehicle? GetLeaderInLane(TrafficVehicle vehicle, int targetLane, float maxDistance = 500f)
    {
        int startCell = GetSplineCell(vehicle.SplinePosition);
        int endCell = GetSplineCell(vehicle.SplinePosition + maxDistance);
        
        TrafficVehicle? closest = null;
        float closestDist = float.MaxValue;
        
        for (int cell = startCell; cell <= endCell; cell++)
        {
            if (_cells.TryGetValue(cell, out var laneDict))
            {
                lock (laneDict)
                {
                    if (laneDict.TryGetValue(targetLane, out var list))
                    {
                        foreach (var other in list)
                        {
                            float dist = other.SplinePosition - vehicle.SplinePosition;
                            if (dist > 0 && dist < closestDist)
                            {
                                closest = other;
                                closestDist = dist;
                            }
                        }
                    }
                }
            }
        }
        
        return closest;
    }
    
    /// <summary>
    /// Get follower in adjacent lane (for lane change evaluation)
    /// </summary>
    public TrafficVehicle? GetFollowerInLane(TrafficVehicle vehicle, int targetLane, float maxDistance = 500f)
    {
        int startCell = GetSplineCell(vehicle.SplinePosition - maxDistance);
        int endCell = GetSplineCell(vehicle.SplinePosition);
        
        TrafficVehicle? closest = null;
        float closestDist = float.MaxValue;
        
        for (int cell = startCell; cell <= endCell; cell++)
        {
            if (_cells.TryGetValue(cell, out var laneDict))
            {
                lock (laneDict)
                {
                    if (laneDict.TryGetValue(targetLane, out var list))
                    {
                        foreach (var other in list)
                        {
                            float dist = vehicle.SplinePosition - other.SplinePosition;
                            if (dist > 0 && dist < closestDist)
                            {
                                closest = other;
                                closestDist = dist;
                            }
                        }
                    }
                }
            }
        }
        
        return closest;
    }
    
    /// <summary>
    /// Get all vehicles in a spline range
    /// </summary>
    public List<TrafficVehicle> GetVehiclesInRange(float startPos, float endPos)
    {
        var results = new List<TrafficVehicle>();
        int startCell = GetSplineCell(startPos);
        int endCell = GetSplineCell(endPos);
        
        for (int cell = startCell; cell <= endCell; cell++)
        {
            if (_cells.TryGetValue(cell, out var laneDict))
            {
                lock (laneDict)
                {
                    foreach (var list in laneDict.Values)
                    {
                        foreach (var vehicle in list)
                        {
                            if (vehicle.SplinePosition >= startPos && vehicle.SplinePosition <= endPos)
                            {
                                results.Add(vehicle);
                            }
                        }
                    }
                }
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Count vehicles in a spline range
    /// </summary>
    public int CountVehiclesInRange(float startPos, float endPos)
    {
        return GetVehiclesInRange(startPos, endPos).Count;
    }
    
    /// <summary>
    /// Clear all vehicles
    /// </summary>
    public void Clear()
    {
        _cells.Clear();
    }
    
    /// <summary>
    /// Rebuild grid from vehicle list
    /// </summary>
    public void Rebuild(IEnumerable<TrafficVehicle> vehicles)
    {
        Clear();
        foreach (var vehicle in vehicles)
        {
            Add(vehicle);
        }
    }
}
