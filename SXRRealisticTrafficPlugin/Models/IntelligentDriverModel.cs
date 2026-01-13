using System.Numerics;

namespace RealisticTrafficPlugin.Models;

/// <summary>
/// Intelligent Driver Model (IDM) for realistic car-following behavior.
/// Based on Treiber, Hennecke, and Helbing (2000) - "Congested Traffic States"
/// </summary>
public class IntelligentDriverModel
{
    /// <summary>
    /// Calculate acceleration based on IDM formula.
    /// acceleration = a × [1 - (v/v₀)^δ - (s*/s)²]
    /// </summary>
    public static float CalculateAcceleration(
        float currentSpeed,          // Current vehicle speed (m/s)
        float desiredSpeed,          // Desired/target speed (m/s)
        float gapToLeader,           // Bumper-to-bumper distance (m)
        float approachingRate,       // Speed difference (positive when closing) (m/s)
        DriverParameters driverParams)
    {
        // Free road acceleration term
        float freeRoadTerm = 1.0f - MathF.Pow(currentSpeed / desiredSpeed, driverParams.AccelerationExponent);
        
        // Desired dynamic gap (s*)
        float desiredGap = CalculateDesiredGap(
            currentSpeed, 
            approachingRate, 
            driverParams);
        
        // Interaction term (braking due to leader)
        float interactionTerm = gapToLeader > 0.1f 
            ? MathF.Pow(desiredGap / gapToLeader, 2) 
            : 1.0f; // Prevent division by zero
        
        // IDM acceleration
        float acceleration = driverParams.MaxAcceleration * (freeRoadTerm - interactionTerm);
        
        // Clamp to physical limits
        return Math.Clamp(acceleration, -driverParams.MaxDeceleration, driverParams.MaxAcceleration);
    }
    
    /// <summary>
    /// Calculate the desired gap s* = s₀ + v×T + (v×Δv)/(2×√(a×b))
    /// </summary>
    private static float CalculateDesiredGap(
        float speed,
        float approachingRate,
        DriverParameters p)
    {
        float dynamicPart = speed * p.TimeHeadway;
        float interactionPart = (speed * approachingRate) / (2 * MathF.Sqrt(p.MaxAcceleration * p.ComfortDeceleration));
        
        return p.MinimumGap + Math.Max(0, dynamicPart + interactionPart);
    }
    
    /// <summary>
    /// Calculate acceleration when there's no leader (free road)
    /// </summary>
    public static float CalculateFreeRoadAcceleration(
        float currentSpeed,
        float desiredSpeed,
        DriverParameters driverParams)
    {
        float freeRoadTerm = 1.0f - MathF.Pow(currentSpeed / desiredSpeed, driverParams.AccelerationExponent);
        return driverParams.MaxAcceleration * freeRoadTerm;
    }
}

/// <summary>
/// Parameters that define a driver's behavior characteristics
/// </summary>
public class DriverParameters
{
    /// <summary>Desired velocity on open road (m/s). Highway: ~36 m/s (130 km/h), Trucks: ~22 m/s (80 km/h)</summary>
    public float DesiredSpeed { get; set; } = 36.0f;
    
    /// <summary>Maximum acceleration (m/s²). Cars: 1.0-2.5, Trucks: 0.5-1.0</summary>
    public float MaxAcceleration { get; set; } = 1.5f;
    
    /// <summary>Comfortable deceleration (m/s²). Typically 2.0-3.0</summary>
    public float ComfortDeceleration { get; set; } = 2.5f;
    
    /// <summary>Maximum emergency deceleration (m/s²)</summary>
    public float MaxDeceleration { get; set; } = 6.0f;
    
    /// <summary>Desired time headway (s). Aggressive: 0.8-1.0, Normal: 1.2-1.5, Timid: 1.8-2.0</summary>
    public float TimeHeadway { get; set; } = 1.2f;
    
    /// <summary>Minimum gap at standstill (m). Typically 2.0</summary>
    public float MinimumGap { get; set; } = 2.0f;
    
    /// <summary>Acceleration exponent (δ). Typically 4</summary>
    public float AccelerationExponent { get; set; } = 4.0f;
    
    /// <summary>
    /// Create default parameters for a car driver
    /// </summary>
    public static DriverParameters CreateCar(DriverPersonality personality = DriverPersonality.Normal)
    {
        var baseParams = new DriverParameters
        {
            DesiredSpeed = 36.0f,      // 130 km/h
            MaxAcceleration = 2.0f,
            ComfortDeceleration = 2.5f,
            MaxDeceleration = 6.0f,
            TimeHeadway = 1.2f,
            MinimumGap = 2.0f,
            AccelerationExponent = 4.0f
        };
        
        return ApplyPersonality(baseParams, personality);
    }
    
    /// <summary>
    /// Create default parameters for a truck driver
    /// </summary>
    public static DriverParameters CreateTruck(DriverPersonality personality = DriverPersonality.Normal)
    {
        var baseParams = new DriverParameters
        {
            DesiredSpeed = 22.2f,      // 80 km/h (truck limit in Japan)
            MaxAcceleration = 0.6f,
            ComfortDeceleration = 2.0f,
            MaxDeceleration = 4.0f,
            TimeHeadway = 1.7f,
            MinimumGap = 3.0f,
            AccelerationExponent = 4.0f
        };
        
        return ApplyPersonality(baseParams, personality);
    }
    
    /// <summary>
    /// Apply personality modifier to parameters
    /// </summary>
    private static DriverParameters ApplyPersonality(DriverParameters p, DriverPersonality personality)
    {
        float factor = personality switch
        {
            DriverPersonality.Timid => 0.8f,
            DriverPersonality.Normal => 1.0f,
            DriverPersonality.Aggressive => 1.2f,
            DriverPersonality.VeryAggressive => 1.4f,
            _ => 1.0f
        };
        
        // Aggressive drivers: faster desired speed, quicker acceleration, shorter following distance
        p.DesiredSpeed *= factor;
        p.MaxAcceleration *= factor;
        p.TimeHeadway /= factor;
        
        return p;
    }
}

public enum DriverPersonality
{
    Timid,          // Factor 0.8 - slower, more following distance
    Normal,         // Factor 1.0 - baseline
    Aggressive,     // Factor 1.2 - faster, closer following
    VeryAggressive  // Factor 1.4 - racing style
}
