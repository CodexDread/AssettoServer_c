namespace SXRRealisticTrafficPlugin.Models;

/// <summary>
/// MOBIL (Minimizing Overall Braking Induced by Lane changes) algorithm.
/// Based on Kesting, Treiber, and Helbing (2007) - Transportation Research Record
/// </summary>
public class MobilLaneChange
{
    /// <summary>
    /// Evaluate whether a lane change should be performed.
    /// Returns the best lane change direction, or null if staying in current lane is optimal.
    /// </summary>
    public static LaneChangeDecision? EvaluateLaneChange(
        TrafficVehicle ego,
        TrafficVehicle? currentLeader,
        TrafficVehicle? leftLeader,
        TrafficVehicle? leftFollower,
        TrafficVehicle? rightLeader,
        TrafficVehicle? rightFollower,
        MobilParameters mobilParams,
        bool isLeftHandTraffic = true)  // Japan uses left-hand traffic
    {
        // Calculate current acceleration with existing leader
        float accCurrent = CalculateAccelerationToLeader(ego, currentLeader);
        
        LaneChangeDecision? bestDecision = null;
        float bestIncentive = mobilParams.AccelerationThreshold;
        
        // Evaluate left lane change
        if (leftLeader != null || leftFollower != null || CanChangeLaneLeft(ego))
        {
            var leftDecision = EvaluateSingleLaneChange(
                ego, currentLeader, accCurrent,
                leftLeader, leftFollower,
                LaneChangeDirection.Left,
                mobilParams,
                isLeftHandTraffic);
            
            if (leftDecision != null && leftDecision.Incentive > bestIncentive)
            {
                bestDecision = leftDecision;
                bestIncentive = leftDecision.Incentive;
            }
        }
        
        // Evaluate right lane change
        if (rightLeader != null || rightFollower != null || CanChangeLaneRight(ego))
        {
            var rightDecision = EvaluateSingleLaneChange(
                ego, currentLeader, accCurrent,
                rightLeader, rightFollower,
                LaneChangeDirection.Right,
                mobilParams,
                isLeftHandTraffic);
            
            if (rightDecision != null && rightDecision.Incentive > bestIncentive)
            {
                bestDecision = rightDecision;
                bestIncentive = rightDecision.Incentive;
            }
        }
        
        return bestDecision;
    }
    
    private static LaneChangeDecision? EvaluateSingleLaneChange(
        TrafficVehicle ego,
        TrafficVehicle? currentLeader,
        float accCurrent,
        TrafficVehicle? newLeader,
        TrafficVehicle? newFollower,
        LaneChangeDirection direction,
        MobilParameters p,
        bool isLeftHandTraffic)
    {
        // === SAFETY CRITERION ===
        // The new follower must not need to brake harder than b_safe
        if (newFollower != null)
        {
            float newFollowerAccAfter = CalculateAccelerationToLeader(newFollower, ego);
            if (newFollowerAccAfter < -p.SafeDeceleration)
            {
                return null; // Unsafe - new follower would need emergency braking
            }
        }
        
        // === INCENTIVE CRITERION ===
        // Personal benefit must outweigh collective cost
        
        // My acceleration after lane change
        float accNew = CalculateAccelerationToLeader(ego, newLeader);
        
        // New follower's acceleration change
        float followerDisadvantage = 0f;
        if (newFollower != null)
        {
            float accFollowerBefore = CalculateAccelerationToLeader(newFollower, newFollower.Leader);
            float accFollowerAfter = CalculateAccelerationToLeader(newFollower, ego);
            followerDisadvantage = accFollowerBefore - accFollowerAfter;
        }
        
        // My personal advantage
        float myAdvantage = accNew - accCurrent;
        
        // Keep-left bias for left-hand traffic (Japan)
        // Encourage returning to left lane after overtaking
        float bias = 0f;
        if (isLeftHandTraffic)
        {
            // In left-hand traffic: left lanes are for cruising, right for passing
            // Add positive bias for moving left (returning to cruising lane)
            // Add negative bias for moving right (only overtake if really needed)
            bias = direction == LaneChangeDirection.Left ? -p.KeepRightBias : p.KeepRightBias;
        }
        else
        {
            // In right-hand traffic: right lanes for cruising, left for passing
            bias = direction == LaneChangeDirection.Right ? -p.KeepRightBias : p.KeepRightBias;
        }
        
        // MOBIL incentive criterion:
        // advantage > politeness * disadvantage + threshold + bias
        float incentive = myAdvantage - p.Politeness * followerDisadvantage - bias;
        
        if (incentive > p.AccelerationThreshold)
        {
            return new LaneChangeDecision
            {
                Direction = direction,
                Incentive = incentive,
                ExpectedAcceleration = accNew,
                NewFollowerAcceleration = newFollower != null 
                    ? CalculateAccelerationToLeader(newFollower, ego) 
                    : 0f
            };
        }
        
        return null;
    }
    
    /// <summary>
    /// Calculate IDM acceleration toward a leader vehicle
    /// </summary>
    private static float CalculateAccelerationToLeader(TrafficVehicle ego, TrafficVehicle? leader)
    {
        if (leader == null)
        {
            // No leader - free road acceleration
            return IntelligentDriverModel.CalculateFreeRoadAcceleration(
                ego.Speed,
                ego.DriverParams.DesiredSpeed,
                ego.DriverParams);
        }
        
        float gap = leader.SplinePosition - ego.SplinePosition - leader.Length;
        float approachingRate = ego.Speed - leader.Speed;
        
        return IntelligentDriverModel.CalculateAcceleration(
            ego.Speed,
            ego.DriverParams.DesiredSpeed,
            gap,
            approachingRate,
            ego.DriverParams);
    }
    
    private static bool CanChangeLaneLeft(TrafficVehicle ego) => ego.CurrentLane > 0;
    private static bool CanChangeLaneRight(TrafficVehicle ego) => true; // Depends on road geometry
}

/// <summary>
/// Parameters for MOBIL lane change behavior
/// </summary>
public class MobilParameters
{
    /// <summary>
    /// Politeness factor (0 = selfish, 0.5 = cooperative).
    /// Realistic highway driving: 0.2-0.3
    /// </summary>
    public float Politeness { get; set; } = 0.25f;
    
    /// <summary>
    /// Maximum safe deceleration for new follower (m/s²).
    /// If lane change would cause follower to brake harder than this, it's blocked.
    /// Typically 4.0 m/s²
    /// </summary>
    public float SafeDeceleration { get; set; } = 4.0f;
    
    /// <summary>
    /// Minimum acceleration advantage to justify lane change (m/s²).
    /// Prevents marginal/frequent lane changes. Typically 0.1-0.2 m/s²
    /// </summary>
    public float AccelerationThreshold { get; set; } = 0.15f;
    
    /// <summary>
    /// Bias for returning to cruising lane (m/s²).
    /// Positive value discourages staying in passing lane.
    /// For Japan (left-hand traffic): encourages returning to left lane.
    /// Typically 0.3 m/s²
    /// </summary>
    public float KeepRightBias { get; set; } = 0.3f;
    
    /// <summary>
    /// Minimum time between lane change attempts (seconds)
    /// Prevents rapid lane hopping
    /// </summary>
    public float LaneChangeCooldown { get; set; } = 3.0f;
    
    /// <summary>
    /// Additional gap margin when lane changing near player vehicles (seconds)
    /// Gives player more predictable AI behavior
    /// </summary>
    public float PlayerReactionMargin { get; set; } = 1.5f;
    
    public static MobilParameters Default => new();
    
    public static MobilParameters Aggressive => new()
    {
        Politeness = 0.1f,
        SafeDeceleration = 5.0f,
        AccelerationThreshold = 0.1f,
        KeepRightBias = 0.2f,
        LaneChangeCooldown = 2.0f
    };
    
    public static MobilParameters Timid => new()
    {
        Politeness = 0.4f,
        SafeDeceleration = 3.0f,
        AccelerationThreshold = 0.25f,
        KeepRightBias = 0.4f,
        LaneChangeCooldown = 5.0f
    };
}

public class LaneChangeDecision
{
    public LaneChangeDirection Direction { get; set; }
    public float Incentive { get; set; }
    public float ExpectedAcceleration { get; set; }
    public float NewFollowerAcceleration { get; set; }
}

public enum LaneChangeDirection
{
    Left,
    Right
}

/// <summary>
/// Represents a traffic vehicle with all state needed for IDM/MOBIL calculations
/// </summary>
public class TrafficVehicle
{
    public int Id { get; set; }
    public float SplinePosition { get; set; }  // Position along the spline
    public int CurrentLane { get; set; }
    public float Speed { get; set; }
    public float Length { get; set; } = 4.5f;  // Vehicle length in meters
    
    public DriverParameters DriverParams { get; set; } = new();
    public MobilParameters MobilParams { get; set; } = new();
    
    // References to nearby vehicles (updated by spatial system)
    public TrafficVehicle? Leader { get; set; }
    public TrafficVehicle? Follower { get; set; }
    
    // Lane change state
    public bool IsChangingLanes { get; set; }
    public float LaneChangeProgress { get; set; }
    public int TargetLane { get; set; }
    public float LastLaneChangeTime { get; set; }
}
