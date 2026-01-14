using System.Numerics;

namespace SXRRealisticTrafficPlugin.Models;

/// <summary>
/// Generates smooth lane change trajectories using quintic polynomials.
/// Ensures zero velocity and acceleration at start/end for natural movement.
/// </summary>
public static class LaneChangeTrajectory
{
    /// <summary>
    /// Calculate lateral offset during lane change using quintic polynomial.
    /// y(t) = lane_width × [10(t/T)³ - 15(t/T)⁴ + 6(t/T)⁵]
    /// </summary>
    /// <param name="progress">Progress through lane change (0.0 to 1.0)</param>
    /// <param name="laneWidth">Width of lane in meters (typically 3.5m)</param>
    /// <returns>Lateral offset from starting lane center</returns>
    public static float QuinticLateralOffset(float progress, float laneWidth)
    {
        // Quintic polynomial: y = 10t³ - 15t⁴ + 6t⁵
        // This produces an S-curve with zero velocity and acceleration at boundaries
        float t = Math.Clamp(progress, 0f, 1f);
        float t2 = t * t;
        float t3 = t2 * t;
        float t4 = t3 * t;
        float t5 = t4 * t;
        
        float polynomial = 10f * t3 - 15f * t4 + 6f * t5;
        return laneWidth * polynomial;
    }
    
    /// <summary>
    /// Calculate lateral velocity during lane change.
    /// Derivative of quintic: dy/dt = lane_width × [30t² - 60t³ + 30t⁴] / T
    /// </summary>
    public static float QuinticLateralVelocity(float progress, float laneWidth, float duration)
    {
        float t = Math.Clamp(progress, 0f, 1f);
        float t2 = t * t;
        float t3 = t2 * t;
        float t4 = t3 * t;
        
        float derivative = 30f * t2 - 60f * t3 + 30f * t4;
        return (laneWidth * derivative) / duration;
    }
    
    /// <summary>
    /// Calculate lane change duration based on speed.
    /// Longer durations at higher speeds for safety and comfort.
    /// </summary>
    /// <param name="speedMs">Current speed in m/s</param>
    /// <returns>Duration in seconds</returns>
    public static float CalculateDuration(float speedMs)
    {
        // Scale duration with speed:
        // 3-4 seconds at 100 km/h (27.8 m/s)
        // 5-6 seconds at 300 km/h (83.3 m/s)
        
        float baseSpeed = 27.8f;   // 100 km/h reference
        float baseDuration = 3.5f;
        float speedFactor = MathF.Max(1f, speedMs / baseSpeed);
        
        // Logarithmic scaling to prevent excessive duration at very high speeds
        float duration = baseDuration * (1f + 0.5f * MathF.Log(speedFactor));
        
        return Math.Clamp(duration, 2.5f, 7.0f);
    }
    
    /// <summary>
    /// Calculate forward distance traveled during lane change.
    /// Needed for Bézier curve calculations.
    /// </summary>
    public static float CalculateForwardDistance(float speedMs, float duration)
    {
        return speedMs * duration;
    }
    
    /// <summary>
    /// Alternative: Cubic Bézier curve for lane change.
    /// Offers more intuitive control point placement.
    /// </summary>
    public static Vector2 BezierLaneChangePosition(
        float progress, 
        float forwardDistance, 
        float laneWidth)
    {
        // Control points for smooth S-curve
        var P0 = new Vector2(0, 0);
        var P1 = new Vector2(forwardDistance * 0.35f, 0);
        var P2 = new Vector2(forwardDistance * 0.65f, laneWidth);
        var P3 = new Vector2(forwardDistance, laneWidth);
        
        float t = Math.Clamp(progress, 0f, 1f);
        float u = 1 - t;
        float u2 = u * u;
        float u3 = u2 * u;
        float t2 = t * t;
        float t3 = t2 * t;
        
        // Cubic Bézier formula: B(t) = (1-t)³P₀ + 3(1-t)²tP₁ + 3(1-t)t²P₂ + t³P₃
        return u3 * P0 + 3 * u2 * t * P1 + 3 * u * t2 * P2 + t3 * P3;
    }
    
    /// <summary>
    /// Calculate maximum lateral acceleration during lane change.
    /// Used to validate comfort limits (should stay under 1.5 m/s²)
    /// </summary>
    public static float MaxLateralAcceleration(float laneWidth, float duration)
    {
        // Peak acceleration occurs at t=0.5 for quintic polynomial
        // d²y/dt² = lane_width × [60t - 180t² + 120t³] / T²
        // At t=0.5: = lane_width × [30 - 45 + 15] / T² = 0
        // Peak is actually at t ≈ 0.21 and t ≈ 0.79
        
        // Simplified approximation for peak lateral acceleration:
        // a_lat_max ≈ 5.77 × lane_width / T²
        float peakAccel = 5.77f * laneWidth / (duration * duration);
        return peakAccel;
    }
    
    /// <summary>
    /// Validate that a lane change at given speed won't exceed comfort limits.
    /// </summary>
    public static bool IsComfortableLaneChange(
        float speedMs, 
        float laneWidth = 3.5f, 
        float maxComfortAccel = 1.5f)
    {
        float duration = CalculateDuration(speedMs);
        float peakAccel = MaxLateralAcceleration(laneWidth, duration);
        return peakAccel <= maxComfortAccel;
    }
    
    /// <summary>
    /// Calculate minimum safe gap for lane change at given speed.
    /// Based on Time-To-Collision (TTC) threshold.
    /// </summary>
    public static float MinSafeGap(
        float egoSpeed, 
        float targetSpeed, 
        float ttcThreshold = 2.0f)
    {
        float closingRate = egoSpeed - targetSpeed;
        
        if (closingRate <= 0)
        {
            // Not approaching - minimum physical gap only
            return 20f; // meters
        }
        
        // Gap = closing_rate × TTC + safety_buffer
        float safetyBuffer = 15f;
        return closingRate * ttcThreshold + safetyBuffer;
    }
}

/// <summary>
/// Manages the active lane change state for a vehicle
/// </summary>
public class LaneChangeState
{
    public bool IsActive { get; private set; }
    public int StartLane { get; private set; }
    public int TargetLane { get; private set; }
    public float StartTime { get; private set; }
    public float Duration { get; private set; }
    public float Progress { get; private set; }
    public float LaneWidth { get; private set; }
    
    /// <summary>
    /// Current lateral offset from the starting lane center
    /// </summary>
    public float LateralOffset { get; private set; }
    
    /// <summary>
    /// Current lateral velocity
    /// </summary>
    public float LateralVelocity { get; private set; }
    
    /// <summary>
    /// Direction of lane change (-1 = left, +1 = right)
    /// </summary>
    public int Direction => TargetLane > StartLane ? 1 : -1;
    
    public void StartLaneChange(
        int fromLane, 
        int toLane, 
        float currentTime, 
        float speedMs,
        float laneWidth = 3.5f)
    {
        IsActive = true;
        StartLane = fromLane;
        TargetLane = toLane;
        StartTime = currentTime;
        Duration = LaneChangeTrajectory.CalculateDuration(speedMs);
        Progress = 0f;
        LaneWidth = laneWidth;
        LateralOffset = 0f;
        LateralVelocity = 0f;
    }
    
    /// <summary>
    /// Update lane change progress based on elapsed time
    /// </summary>
    public void Update(float currentTime)
    {
        if (!IsActive) return;
        
        float elapsed = currentTime - StartTime;
        Progress = Math.Clamp(elapsed / Duration, 0f, 1f);
        
        float totalOffset = LaneWidth * Direction;
        LateralOffset = LaneChangeTrajectory.QuinticLateralOffset(Progress, totalOffset);
        LateralVelocity = LaneChangeTrajectory.QuinticLateralVelocity(Progress, totalOffset, Duration);
        
        // Complete the lane change
        if (Progress >= 1f)
        {
            Complete();
        }
    }
    
    /// <summary>
    /// Abort lane change (e.g., if unsafe situation detected)
    /// </summary>
    public void Abort()
    {
        IsActive = false;
        Progress = 0f;
        LateralOffset = 0f;
        LateralVelocity = 0f;
    }
    
    private void Complete()
    {
        IsActive = false;
        Progress = 1f;
    }
}
