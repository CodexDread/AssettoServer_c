using System;
using System.Collections.Generic;
using System.Drawing;
using System.Numerics;
using AssettoServer.Server.Ai.Splines;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Configuration.Extra;
using AssettoServer.Server.Weather;
using AssettoServer.Shared.Model;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Utils;
using JPBotelho;
using Serilog;
using SunCalcNet.Model;

namespace AssettoServer.Server.Ai;

public class AiState : IDisposable
{
    public CarStatus Status { get; } = new();
    public bool Initialized { get; private set; }

    public int CurrentSplinePointId
    {
        get => _currentSplinePointId;
        private set
        {
            _spline.SlowestAiStates.Enter(value, this);
            _spline.SlowestAiStates.Leave(_currentSplinePointId, this);
            _currentSplinePointId = value;
        }
    }

    private int _currentSplinePointId;
    
    public long SpawnProtectionEnds { get; set; }
    public float SafetyDistanceSquared { get; set; } = 20 * 20;
    public float Acceleration { get; set; }
    public float CurrentSpeed { get; private set; }
    public float TargetSpeed { get; private set; }
    public float InitialMaxSpeed { get; private set; }
    public float MaxSpeed { get; private set; }
    public Color Color { get; private set; }
    public byte SpawnCounter { get; private set; }
    public float ClosestAiObstacleDistance { get; private set; }
    public EntryCar EntryCar { get; }

    // === SXR TRAFFIC PERSONALITY SYSTEM ===
    /// <summary>
    /// Driver aggressiveness (0-1). 0 = passive/safe, 1 = aggressive/fast
    /// - 0: Drives 10 under limit, rarely changes lanes, very polite
    /// - 1: Drives 30 over limit, cuts through traffic, minimal politeness
    /// </summary>
    public float Aggressiveness { get; private set; }
    
    /// <summary>
    /// Current lateral offset from spline center (meters). Negative = left, positive = right.
    /// </summary>
    public float LateralOffset { get; private set; }
    
    /// <summary>
    /// Whether a lane change is currently in progress (including abort animation)
    /// </summary>
    public bool IsChangingLanes => _laneChangeActive;
    
    private bool _laneChangeActive;
    private int _laneChangeSourcePointId;
    private int _laneChangeTargetPointId;
    private float _laneChangeStartTime;
    private float _laneChangeDuration;
    private bool _laneChangeIsLeft;
    private float _lastLaneChangeTime;
    private float _lastProactiveLaneCheckTime;

    // Lane change abort: smooth return instead of teleport
    private bool _laneChangeAborting;
    private float _laneChangeAbortStartTime;
    private float _laneChangeAbortStartOffset; // Lateral offset when abort started

    // Chain-reaction prevention: track when a new obstacle appeared in front
    private float _newObstacleAppearedTime;
    private int _lastKnownLeaderSessionId = -1;

    // Proactive lane change planning: look early, execute at obstacle
    private bool _hasPlannedLaneChange;
    private bool _plannedLaneChangeIsLeft;
    private int _plannedTargetPointId;
    private float _plannedLaneChangeGapDistance; // Distance to gap we found
    // === END SXR TRAFFIC PERSONALITY ===

    private const float WalkingSpeed = 10 / 3.6f;

    private Vector3 _startTangent;
    private Vector3 _endTangent;

    private float _currentVecLength;
    private float _currentVecProgress;
    private long _lastTick;
    private bool _stoppedForObstacle;
    private long _stoppedForObstacleSince;
    private long _ignoreObstaclesUntil;
    private long _stoppedForCollisionUntil;
    private long _obstacleHonkStart;
    private long _obstacleHonkEnd;
    private CarStatusFlags _indicator = 0;
    private int _nextJunctionId;
    private bool _junctionPassed;
    private float _endIndicatorDistance;
    private float _minObstacleDistance;
    private double _randomTwilight;

    private readonly ACServerConfiguration _configuration;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly WeatherManager _weatherManager;
    private readonly AiSpline _spline;
    private readonly JunctionEvaluator _junctionEvaluator;

    private static readonly List<Color> CarColors =
    [
        Color.FromArgb(13, 17, 22),
        Color.FromArgb(19, 24, 31),
        Color.FromArgb(28, 29, 33),
        Color.FromArgb(12, 13, 24),
        Color.FromArgb(11, 20, 33),
        Color.FromArgb(151, 154, 151),
        Color.FromArgb(153, 157, 160),
        Color.FromArgb(194, 196, 198),
        Color.FromArgb(234, 234, 234),
        Color.FromArgb(255, 255, 255),
        Color.FromArgb(182, 17, 27),
        Color.FromArgb(218, 25, 24),
        Color.FromArgb(73, 17, 29),
        Color.FromArgb(35, 49, 85),
        Color.FromArgb(28, 53, 81),
        Color.FromArgb(37, 58, 167),
        Color.FromArgb(21, 92, 45),
        Color.FromArgb(18, 46, 43)
    ];

    public AiState(EntryCar entryCar, SessionManager sessionManager, WeatherManager weatherManager, ACServerConfiguration configuration, EntryCarManager entryCarManager, AiSpline spline)
    {
        EntryCar = entryCar;
        _sessionManager = sessionManager;
        _weatherManager = weatherManager;
        _configuration = configuration;
        _entryCarManager = entryCarManager;
        _spline = spline;
        _junctionEvaluator = new JunctionEvaluator(spline);

        _lastTick = _sessionManager.ServerTimeMilliseconds;
    }

    ~AiState()
    {
        Despawn();
    }
    
    public void Dispose()
    {
        Despawn();
        GC.SuppressFinalize(this);
    }

    public void Despawn()
    {
        Initialized = false;
        _laneChangeActive = false;
        _laneChangeAborting = false;
        _laneChangeAbortStartTime = 0;
        _laneChangeAbortStartOffset = 0;
        LateralOffset = 0;
        Aggressiveness = 0;
        _lastProactiveLaneCheckTime = 0;
        _newObstacleAppearedTime = 0;
        _lastKnownLeaderSessionId = -1;
        _hasPlannedLaneChange = false;
        _plannedLaneChangeIsLeft = false;
        _plannedTargetPointId = -1;
        _plannedLaneChangeGapDistance = 0;
        _spline.SlowestAiStates.Leave(CurrentSplinePointId, this);
    }

    private void SetRandomSpeed()
    {
        // === SXR: Aggressiveness-based speed ===
        // Generate aggressiveness on spawn (weighted distribution - more passive drivers than aggressive)
        // Use a biased distribution: most drivers are average, few are very aggressive
        float rawRandom = (float)Random.Shared.NextDouble();
        Aggressiveness = rawRandom * rawRandom; // Bias toward 0 (passive), fewer aggressive drivers
        
        // Base speed variation
        float baseMaxSpeed = _configuration.Extra.AiParams.MaxSpeedMs;
        float variation = baseMaxSpeed * _configuration.Extra.AiParams.MaxSpeedVariationPercent;
        
        // Get configurable speed offsets
        float passiveOffsetMs = LaneChangeConfig.PassiveSpeedOffsetKmh / 3.6f;
        float aggressiveOffsetMs = LaneChangeConfig.AggressiveSpeedOffsetKmh / 3.6f;
        
        // Aggressiveness speed modifier:
        // 0.0 aggression = passive offset (default -10 km/h under limit)
        // 0.5 aggression = at limit (average)
        // 1.0 aggression = aggressive offset (default +30 km/h over limit)
        float speedModifierMs = (float)MathUtils.Lerp(passiveOffsetMs, aggressiveOffsetMs, Aggressiveness);
        
        // Fast lane offset (if there's a lane to the left, we're in a slow lane)
        float fastLaneOffset = 0;
        if (_spline.Points[CurrentSplinePointId].LeftId >= 0)
        {
            fastLaneOffset = _configuration.Extra.AiParams.RightLaneOffsetMs;
        }
        
        // Calculate final speed with small random variation
        float randomVariation = (-variation / 2) + (float)Random.Shared.NextDouble() * variation;
        InitialMaxSpeed = baseMaxSpeed + fastLaneOffset + speedModifierMs + randomVariation * 0.3f;
        
        // Ensure minimum speed for safety
        InitialMaxSpeed = MathF.Max(InitialMaxSpeed, 8.33f); // Min 30 km/h
        
        CurrentSpeed = InitialMaxSpeed;
        TargetSpeed = InitialMaxSpeed;
        MaxSpeed = InitialMaxSpeed;
    }

    private void SetRandomColor()
    {
        Color = CarColors[Random.Shared.Next(CarColors.Count)];
    }

    public void Teleport(int pointId)
    {
        // Reset any active lane change
        _laneChangeActive = false;
        LateralOffset = 0;
        _indicator = 0;
        
        _junctionEvaluator.Clear();
        CurrentSplinePointId = pointId;
        if (!_junctionEvaluator.TryNext(CurrentSplinePointId, out var nextPointId))
            throw new InvalidOperationException($"Cannot get next spline point for {CurrentSplinePointId}");
        _currentVecLength = (_spline.Points[nextPointId].Position - _spline.Points[CurrentSplinePointId].Position).Length();
        _currentVecProgress = 0;
            
        CalculateTangents();
        
        SetRandomSpeed();
        SetRandomColor();

        var minDist = _configuration.Extra.AiParams.MinAiSafetyDistanceSquared;
        var maxDist = _configuration.Extra.AiParams.MaxAiSafetyDistanceSquared;
        if (_configuration.Extra.AiParams.LaneCountSpecificOverrides.TryGetValue(_spline.GetLanes(CurrentSplinePointId).Length, out var overrides))
        {
            minDist = overrides.MinAiSafetyDistanceSquared;
            maxDist = overrides.MaxAiSafetyDistanceSquared;
        }
        
        if (EntryCar.MinAiSafetyDistanceMetersSquared.HasValue)
            minDist = EntryCar.MinAiSafetyDistanceMetersSquared.Value;
        if (EntryCar.MaxAiSafetyDistanceMetersSquared.HasValue)
            maxDist = EntryCar.MaxAiSafetyDistanceMetersSquared.Value;

        SpawnProtectionEnds = _sessionManager.ServerTimeMilliseconds + Random.Shared.Next(EntryCar.AiMinSpawnProtectionTimeMilliseconds, EntryCar.AiMaxSpawnProtectionTimeMilliseconds);
        SafetyDistanceSquared = Random.Shared.Next((int)Math.Round(minDist * (1.0f / _configuration.Extra.AiParams.TrafficDensity)),
            (int)Math.Round(maxDist * (1.0f / _configuration.Extra.AiParams.TrafficDensity)));
        _stoppedForCollisionUntil = 0;
        _ignoreObstaclesUntil = 0;
        _obstacleHonkEnd = 0;
        _obstacleHonkStart = 0;
        _indicator = 0;
        _randomTwilight = Random.Shared.NextSingle(0, 12) * Math.PI / 180.0;
        _nextJunctionId = -1;
        _junctionPassed = false;
        _endIndicatorDistance = 0;
        _lastTick = _sessionManager.ServerTimeMilliseconds;
        _minObstacleDistance = Random.Shared.Next(8, 13);
        SpawnCounter++;
        Initialized = true;
        Update();
    }

    private void CalculateTangents()
    {
        if (!_junctionEvaluator.TryNext(CurrentSplinePointId, out var nextPointId))
            throw new InvalidOperationException("Cannot get next spline point");

        var points = _spline.Points;
        
        if (_junctionEvaluator.TryPrevious(CurrentSplinePointId, out var previousPointId))
        {
            _startTangent = (points[nextPointId].Position - points[previousPointId].Position) * 0.5f;
        }
        else
        {
            _startTangent = (points[nextPointId].Position - points[CurrentSplinePointId].Position) * 0.5f;
        }

        if (_junctionEvaluator.TryNext(CurrentSplinePointId, out var nextNextPointId, 2))
        {
            _endTangent = (points[nextNextPointId].Position - points[CurrentSplinePointId].Position) * 0.5f;
        }
        else
        {
            _endTangent = (points[nextPointId].Position - points[CurrentSplinePointId].Position) * 0.5f;
        }
    }

    private bool Move(float progress)
    {
        var points = _spline.Points;
        var junctions = _spline.Junctions;
        
        bool recalculateTangents = false;
        while (progress > _currentVecLength)
        {
            progress -= _currentVecLength;
                
            if (!_junctionEvaluator.TryNext(CurrentSplinePointId, out var nextPointId)
                || !_junctionEvaluator.TryNext(nextPointId, out var nextNextPointId))
            {
                return false;
            }

            CurrentSplinePointId = nextPointId;
            _currentVecLength = (points[nextNextPointId].Position - points[CurrentSplinePointId].Position).Length();
            recalculateTangents = true;

            if (_junctionPassed)
            {
                _endIndicatorDistance -= _currentVecLength;

                if (_endIndicatorDistance < 0)
                {
                    _indicator = 0;
                    _junctionPassed = false;
                    _endIndicatorDistance = 0;
                }
            }
                
            if (_nextJunctionId >= 0 && points[CurrentSplinePointId].JunctionEndId == _nextJunctionId)
            {
                _junctionPassed = true;
                _endIndicatorDistance = junctions[_nextJunctionId].IndicateDistancePost;
                _nextJunctionId = -1;
            }
        }

        if (recalculateTangents)
        {
            CalculateTangents();
        }

        _currentVecProgress = progress;

        return true;
    }

    public bool CanSpawn(int spawnPointId, AiState? previousAi, AiState? nextAi)
    {
        var ops = _spline.Operations;
        ref readonly var spawnPoint = ref ops.Points[spawnPointId];

        if (!IsAllowedLaneCount(spawnPointId))
            return false;
        if (!IsAllowedLane(in spawnPoint))
            return false;
        if (!IsKeepingSafetyDistances(in spawnPoint, previousAi, nextAi))
            return false;

        return EntryCar.CanSpawnAiState(spawnPoint.Position, this);
    }

    private bool IsKeepingSafetyDistances(in SplinePoint spawnPoint, AiState? previousAi, AiState? nextAi)
    {
        if (previousAi != null)
        {
            var distance = MathF.Max(0, Vector3.Distance(spawnPoint.Position, previousAi.Status.Position)
                           - previousAi.EntryCar.VehicleLengthPreMeters
                           - EntryCar.VehicleLengthPostMeters);

            var distanceSquared = distance * distance;
            if (distanceSquared < previousAi.SafetyDistanceSquared || distanceSquared < SafetyDistanceSquared)
                return false;
        }
        
        if (nextAi != null)
        {
            var distance = MathF.Max(0, Vector3.Distance(spawnPoint.Position, nextAi.Status.Position)
                                        - nextAi.EntryCar.VehicleLengthPostMeters
                                        - EntryCar.VehicleLengthPreMeters);

            var distanceSquared = distance * distance;
            if (distanceSquared < nextAi.SafetyDistanceSquared || distanceSquared < SafetyDistanceSquared)
                return false;
        }

        return true;
    }

    private bool IsAllowedLaneCount(int spawnPointId)
    {
        var laneCount = _spline.GetLanes(spawnPointId).Length;
        if (EntryCar.MinLaneCount.HasValue && laneCount < EntryCar.MinLaneCount.Value)
            return false;
        if (EntryCar.MaxLaneCount.HasValue && laneCount > EntryCar.MaxLaneCount.Value)
            return false;
        
        return true;
    }

    private bool IsAllowedLane(in SplinePoint spawnPoint)
    {
        var isAllowedLane = true;
        if (EntryCar.AiAllowedLanes != null)
        {
            isAllowedLane = (EntryCar.AiAllowedLanes.Contains(LaneSpawnBehavior.Middle) && spawnPoint.LeftId >= 0 && spawnPoint.RightId >= 0)
                            || (EntryCar.AiAllowedLanes.Contains(LaneSpawnBehavior.Left) && spawnPoint.LeftId < 0)
                            || (EntryCar.AiAllowedLanes.Contains(LaneSpawnBehavior.Right) && spawnPoint.RightId < 0);
        }

        return isAllowedLane;
    }

    private (AiState? ClosestAiState, float ClosestAiStateDistance, float MaxSpeed) SplineLookahead()
    {
        var points = _spline.Points;
        var junctions = _spline.Junctions;
        
        float maxBrakingDistance = PhysicsUtils.CalculateBrakingDistance(CurrentSpeed, EntryCar.AiDeceleration) * 2 + 20;
        AiState? closestAiState = null;
        float closestAiStateDistance = float.MaxValue;
        bool junctionFound = false;
        float distanceTravelled = 0;
        var pointId = CurrentSplinePointId;
        ref readonly var point = ref points[pointId]; 
        float maxSpeed = float.MaxValue;
        float currentSpeedSquared = CurrentSpeed * CurrentSpeed;
        while (distanceTravelled < maxBrakingDistance)
        {
            distanceTravelled += point.Length;
            pointId = _junctionEvaluator.Next(pointId);
            if (pointId < 0)
                break;

            point = ref points[pointId];

            if (!junctionFound && point.JunctionStartId >= 0 && distanceTravelled < junctions[point.JunctionStartId].IndicateDistancePre)
            {
                ref readonly var jct = ref junctions[point.JunctionStartId];
                
                var indicator = _junctionEvaluator.WillTakeJunction(point.JunctionStartId) ? jct.IndicateWhenTaken : jct.IndicateWhenNotTaken;
                if (indicator != 0)
                {
                    _indicator = indicator;
                    _nextJunctionId = point.JunctionStartId;
                    junctionFound = true;
                }
            }

            if (closestAiState == null)
            {
                var slowest = _spline.SlowestAiStates[pointId];

                if (slowest != null)
                {
                    closestAiState = slowest;
                    closestAiStateDistance = MathF.Max(0, Vector3.Distance(Status.Position, closestAiState.Status.Position)
                                                          - EntryCar.VehicleLengthPreMeters
                                                          - closestAiState.EntryCar.VehicleLengthPostMeters);
                }
            }

            float maxCorneringSpeedSquared = PhysicsUtils.CalculateMaxCorneringSpeedSquared(point.Radius, EntryCar.AiCorneringSpeedFactor);
            if (maxCorneringSpeedSquared < currentSpeedSquared)
            {
                float maxCorneringSpeed = MathF.Sqrt(maxCorneringSpeedSquared);
                float brakingDistance = PhysicsUtils.CalculateBrakingDistance(CurrentSpeed - maxCorneringSpeed,
                                            EntryCar.AiDeceleration * EntryCar.AiCorneringBrakeForceFactor)
                                        * EntryCar.AiCorneringBrakeDistanceFactor;

                if (brakingDistance > distanceTravelled)
                {
                    maxSpeed = Math.Min(maxCorneringSpeed, maxSpeed);
                }
            }
        }

        return (closestAiState, closestAiStateDistance, maxSpeed);
    }

    private bool ShouldIgnorePlayerObstacles()
    {
        if (_configuration.Extra.AiParams.IgnorePlayerObstacleSpheres != null)
        {
            foreach (var sphere in _configuration.Extra.AiParams.IgnorePlayerObstacleSpheres)
            {
                if (Vector3.DistanceSquared(Status.Position, sphere.Center) < sphere.RadiusMeters * sphere.RadiusMeters)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private (EntryCar? entryCar, float distance) FindClosestPlayerObstacle()
    {
        if (!ShouldIgnorePlayerObstacles())
        {
            EntryCar? closestCar = null;
            float minDistance = float.MaxValue;
            for (var i = 0; i < _entryCarManager.EntryCars.Length; i++)
            {
                var playerCar = _entryCarManager.EntryCars[i];
                if (playerCar.Client?.HasSentFirstUpdate == true)
                {
                    float distance = Vector3.DistanceSquared(playerCar.Status.Position, Status.Position);

                    if (distance < minDistance
                        && Math.Abs(playerCar.Status.Position.Y - Status.Position.Y) < 1.5
                        && GetAngleToCar(playerCar.Status) is > 166 and < 194)
                    {
                        minDistance = distance;
                        closestCar = playerCar;
                    }
                }
            }

            if (closestCar != null)
            {
                return (closestCar, MathF.Sqrt(minDistance));
            }
        }

        return (null, float.MaxValue);
    }

    private bool IsObstacle(EntryCar playerCar)
    {
        float aiRectWidth = 4; // Lane width
        float halfAiRectWidth = aiRectWidth / 2;
        float aiRectLength = 10; // length of rectangle infront of ai traffic
        float aiRectOffset = 1; // offset of the rectangle from ai position

        float obstacleRectWidth = 1; // width of obstacle car 
        float obstacleRectLength = 1; // length of obstacle car
        float halfObstacleRectWidth = obstacleRectWidth / 2;
        float halfObstanceRectLength = obstacleRectLength / 2;

        Vector3 forward = Vector3.Transform(-Vector3.UnitX, Matrix4x4.CreateRotationY(Status.Rotation.X));
        Matrix4x4 aiViewMatrix = Matrix4x4.CreateLookAt(Status.Position, Status.Position + forward, Vector3.UnitY);

        Matrix4x4 targetWorldViewMatrix = Matrix4x4.CreateRotationY(playerCar.Status.Rotation.X) * Matrix4x4.CreateTranslation(playerCar.Status.Position) * aiViewMatrix;

        Vector3 targetFrontLeft = Vector3.Transform(new Vector3(-halfObstanceRectLength, 0, halfObstacleRectWidth), targetWorldViewMatrix);
        Vector3 targetFrontRight = Vector3.Transform(new Vector3(-halfObstanceRectLength, 0, -halfObstacleRectWidth), targetWorldViewMatrix);
        Vector3 targetRearLeft = Vector3.Transform(new Vector3(halfObstanceRectLength, 0, halfObstacleRectWidth), targetWorldViewMatrix);
        Vector3 targetRearRight = Vector3.Transform(new Vector3(halfObstanceRectLength, 0, -halfObstacleRectWidth), targetWorldViewMatrix);

        static bool IsPointInside(Vector3 point, float width, float length, float offset)
            => MathF.Abs(point.X) >= width || (-point.Z >= offset && -point.Z <= offset + length);

        bool isObstacle = IsPointInside(targetFrontLeft, halfAiRectWidth, aiRectLength, aiRectOffset)
                          || IsPointInside(targetFrontRight, halfAiRectWidth, aiRectLength, aiRectOffset)
                          || IsPointInside(targetRearLeft, halfAiRectWidth, aiRectLength, aiRectOffset)
                          || IsPointInside(targetRearRight, halfAiRectWidth, aiRectLength, aiRectOffset);

        return isObstacle;
    }

    public void DetectObstacles()
    {
        if (!Initialized) return;
            
        if (_sessionManager.ServerTimeMilliseconds < _ignoreObstaclesUntil)
        {
            SetTargetSpeed(MaxSpeed);
            return;
        }

        if (_sessionManager.ServerTimeMilliseconds < _stoppedForCollisionUntil)
        {
            SetTargetSpeed(0);
            return;
        }
            
        float targetSpeed = InitialMaxSpeed;
        float maxSpeed = InitialMaxSpeed;
        bool hasObstacle = false;

        var splineLookahead = SplineLookahead();
        var playerObstacle = FindClosestPlayerObstacle();

        ClosestAiObstacleDistance = splineLookahead.ClosestAiState != null ? splineLookahead.ClosestAiStateDistance : -1;

        if (playerObstacle.distance < _minObstacleDistance || splineLookahead.ClosestAiStateDistance < _minObstacleDistance)
        {
            targetSpeed = 0;
            hasObstacle = true;
        }
        else if (playerObstacle.distance < splineLookahead.ClosestAiStateDistance && playerObstacle.entryCar != null)
        {
            float playerSpeed = playerObstacle.entryCar.Status.Velocity.Length();

            if (playerSpeed < 0.1f)
            {
                playerSpeed = 0;
            }

            if ((playerSpeed < CurrentSpeed || playerSpeed == 0)
                && playerObstacle.distance < PhysicsUtils.CalculateBrakingDistance(CurrentSpeed - playerSpeed, EntryCar.AiDeceleration) * 2 + 20)
            {
                targetSpeed = Math.Max(WalkingSpeed, playerSpeed);
                hasObstacle = true;
            }
        }
        else if (splineLookahead.ClosestAiState != null)
        {
            float closestTargetSpeed = Math.Min(splineLookahead.ClosestAiState.CurrentSpeed, splineLookahead.ClosestAiState.TargetSpeed);
            if ((closestTargetSpeed < CurrentSpeed || splineLookahead.ClosestAiState.CurrentSpeed == 0)
                && splineLookahead.ClosestAiStateDistance < PhysicsUtils.CalculateBrakingDistance(CurrentSpeed - closestTargetSpeed, EntryCar.AiDeceleration) * 2 + 20)
            {
                targetSpeed = Math.Max(WalkingSpeed, closestTargetSpeed);
                hasObstacle = true;
            }
        }

        targetSpeed = Math.Min(splineLookahead.MaxSpeed, targetSpeed);

        if (CurrentSpeed == 0 && !_stoppedForObstacle)
        {
            _stoppedForObstacle = true;
            _stoppedForObstacleSince = _sessionManager.ServerTimeMilliseconds;
            _obstacleHonkStart = _stoppedForObstacleSince + Random.Shared.Next(3000, 7000);
            _obstacleHonkEnd = _obstacleHonkStart + Random.Shared.Next(500, 1500);
            Log.Verbose("AI {SessionId} stopped for obstacle", EntryCar.SessionId);
        }
        else if (CurrentSpeed > 0 && _stoppedForObstacle)
        {
            _stoppedForObstacle = false;
            Log.Verbose("AI {SessionId} no longer stopped for obstacle", EntryCar.SessionId);
        }
        else if (_stoppedForObstacle && _sessionManager.ServerTimeMilliseconds - _stoppedForObstacleSince > _configuration.Extra.AiParams.IgnoreObstaclesAfterMilliseconds)
        {
            _ignoreObstaclesUntil = _sessionManager.ServerTimeMilliseconds + 10_000;
            Log.Verbose("AI {SessionId} ignoring obstacles until {IgnoreObstaclesUntil}", EntryCar.SessionId, _ignoreObstaclesUntil);
        }

        float deceleration = EntryCar.AiDeceleration;
        if (!hasObstacle)
        {
            deceleration *= EntryCar.AiCorneringBrakeForceFactor;
        }
        
        MaxSpeed = maxSpeed;
        SetTargetSpeed(targetSpeed, deceleration, EntryCar.AiAcceleration);
        
        // === SXR: Evaluate lane change ===
        if (!_laneChangeActive)
        {
            float currentTimeSec = _sessionManager.ServerTimeMilliseconds / 1000f;

            // === FIX #2: CHAIN-REACTION PREVENTION ===
            // Track if a new leader just appeared (someone changed lane in front of us)
            int currentLeaderSessionId = splineLookahead.ClosestAiState?.EntryCar.SessionId ?? -1;
            if (currentLeaderSessionId != _lastKnownLeaderSessionId && currentLeaderSessionId >= 0)
            {
                // New leader appeared - record the time
                _newObstacleAppearedTime = currentTimeSec;
                _lastKnownLeaderSessionId = currentLeaderSessionId;

                // Clear any planned lane change since situation changed
                _hasPlannedLaneChange = false;

                if (LaneChangeDebugLogging)
                {
                    Log.Debug("AI {SessionId} detected new leader {LeaderId}, waiting before lane change",
                        EntryCar.SessionId, currentLeaderSessionId);
                }
            }
            else if (currentLeaderSessionId < 0)
            {
                _lastKnownLeaderSessionId = -1;
            }

            // Chain-reaction cooldown: don't lane change immediately when a new obstacle appears
            // This gives time to assess the situation and prevents panic lane changes
            float chainReactionCooldown = (float)MathUtils.Lerp(3.0f, 1.5f, Aggressiveness);
            bool inChainReactionCooldown = (currentTimeSec - _newObstacleAppearedTime) < chainReactionCooldown;

            // Reactive lane change: when blocked by slower traffic
            if (hasObstacle && splineLookahead.ClosestAiState != null && !inChainReactionCooldown)
            {
                // === FIX #3: EXECUTE PLANNED LANE CHANGE ===
                // If we had a planned lane change, now is the time to execute it
                if (_hasPlannedLaneChange)
                {
                    var point = _spline.Points[CurrentSplinePointId];
                    int targetPointId = _plannedLaneChangeIsLeft ? point.LeftId : point.RightId;

                    // Verify the gap is still available
                    if (targetPointId >= 0 && EvaluateLaneChange(targetPointId, splineLookahead.ClosestAiState, splineLookahead.ClosestAiStateDistance, _plannedLaneChangeIsLeft))
                    {
                        StartLaneChange(targetPointId, _plannedLaneChangeIsLeft);
                        _hasPlannedLaneChange = false;

                        if (LaneChangeDebugLogging)
                        {
                            Log.Debug("AI {SessionId} executing planned lane change at obstacle",
                                EntryCar.SessionId);
                        }
                    }
                    else
                    {
                        // Gap no longer available, try regular lane change
                        _hasPlannedLaneChange = false;
                        TryMobilLaneChange(splineLookahead.ClosestAiState, splineLookahead.ClosestAiStateDistance);
                    }
                }
                else
                {
                    TryMobilLaneChange(splineLookahead.ClosestAiState, splineLookahead.ClosestAiStateDistance);
                }
            }
            // Proactive lane change PLANNING for aggressive drivers: look ahead but don't execute yet
            else if (Aggressiveness > 0.5f && splineLookahead.ClosestAiState != null && !_hasPlannedLaneChange)
            {
                // More aggressive = checks more frequently and at greater distance
                float proactiveCheckInterval = (float)MathUtils.Lerp(3f, 0.5f, Aggressiveness);
                float proactiveLookahead = (float)MathUtils.Lerp(50f, 150f, Aggressiveness);

                if (currentTimeSec - _lastProactiveLaneCheckTime > proactiveCheckInterval)
                {
                    _lastProactiveLaneCheckTime = currentTimeSec;

                    // Check if there's slower traffic ahead within proactive lookahead
                    if (splineLookahead.ClosestAiStateDistance < proactiveLookahead)
                    {
                        float leaderSpeed = Math.Min(splineLookahead.ClosestAiState.CurrentSpeed, splineLookahead.ClosestAiState.TargetSpeed);

                        // If we're significantly faster than the leader, find a gap but DON'T execute yet
                        if (CurrentSpeed > leaderSpeed + 5f) // 5 m/s = 18 km/h faster
                        {
                            // Plan the lane change instead of executing immediately
                            TryPlanProactiveLaneChange(splineLookahead.ClosestAiState, splineLookahead.ClosestAiStateDistance);
                        }
                    }
                }
            }
        }
    }

    public void StopForCollision()
    {
        if (!ShouldIgnorePlayerObstacles())
        {
            _stoppedForCollisionUntil = _sessionManager.ServerTimeMilliseconds + Random.Shared.Next(EntryCar.AiMinCollisionStopTimeMilliseconds, EntryCar.AiMaxCollisionStopTimeMilliseconds);
        }
    }

    /// <returns>0 is the rear <br/> Angle is counterclockwise</returns>
    public float GetAngleToCar(CarStatus car)
    {
        float challengedAngle = (float) (Math.Atan2(Status.Position.X - car.Position.X, Status.Position.Z - car.Position.Z) * 180 / Math.PI);
        if (challengedAngle < 0)
            challengedAngle += 360;
        float challengedRot = Status.GetRotationAngle();

        challengedAngle += challengedRot;
        challengedAngle %= 360;

        return challengedAngle;
    }

    private void SetTargetSpeed(float speed, float deceleration, float acceleration)
    {
        TargetSpeed = speed;
        if (speed < CurrentSpeed)
        {
            Acceleration = -deceleration;
        }
        else if (speed > CurrentSpeed)
        {
            Acceleration = acceleration;
        }
        else
        {
            Acceleration = 0;
        }
    }

    private void SetTargetSpeed(float speed)
    {
        SetTargetSpeed(speed, EntryCar.AiDeceleration, EntryCar.AiAcceleration);
    }

    public void Update()
    {
        if (!Initialized)
            return;

        var ops = _spline.Operations;

        long currentTime = _sessionManager.ServerTimeMilliseconds;
        long dt = currentTime - _lastTick;
        _lastTick = currentTime;

        if (Acceleration != 0)
        {
            CurrentSpeed += Acceleration * (dt / 1000.0f);
                
            if ((Acceleration < 0 && CurrentSpeed < TargetSpeed) || (Acceleration > 0 && CurrentSpeed > TargetSpeed))
            {
                CurrentSpeed = TargetSpeed;
                Acceleration = 0;
            }
        }

        float moveMeters = (dt / 1000.0f) * CurrentSpeed;
        if (!Move(_currentVecProgress + moveMeters) || !_junctionEvaluator.TryNext(CurrentSplinePointId, out var nextPoint))
        {
            Log.Debug("Car {SessionId} reached spline end, despawning", EntryCar.SessionId);
            Despawn();
            return;
        }

        CatmullRom.CatmullRomPoint smoothPos = CatmullRom.Evaluate(ops.Points[CurrentSplinePointId].Position, 
            ops.Points[nextPoint].Position, 
            _startTangent, 
            _endTangent, 
            _currentVecProgress / _currentVecLength);
            
        Vector3 rotation = new Vector3
        {
            X = MathF.Atan2(smoothPos.Tangent.Z, smoothPos.Tangent.X) - MathF.PI / 2,
            Y = (MathF.Atan2(new Vector2(smoothPos.Tangent.Z, smoothPos.Tangent.X).Length(), smoothPos.Tangent.Y) - MathF.PI / 2) * -1f,
            Z = ops.GetCamber(CurrentSplinePointId, _currentVecProgress / _currentVecLength)
        };

        // === SXR: Calculate position with lane change offset ===
        Vector3 position = smoothPos.Position with { Y = smoothPos.Position.Y + EntryCar.AiSplineHeightOffsetMeters };
        
        if (_laneChangeActive)
        {
            float currentTimeSec = _sessionManager.ServerTimeMilliseconds / 1000f;
            bool wasActive = _laneChangeActive;
            UpdateLaneChange(currentTimeSec);
            
            // If lane change just completed, recalculate position from NEW spline
            if (wasActive && !_laneChangeActive)
            {
                // Recalculate smoothPos from new spline point
                if (_junctionEvaluator.TryNext(CurrentSplinePointId, out var newNextPoint))
                {
                    smoothPos = CatmullRom.Evaluate(ops.Points[CurrentSplinePointId].Position, 
                        ops.Points[newNextPoint].Position, 
                        _startTangent, 
                        _endTangent, 
                        _currentVecProgress / _currentVecLength);
                    
                    position = smoothPos.Position with { Y = smoothPos.Position.Y + EntryCar.AiSplineHeightOffsetMeters };
                    
                    rotation = new Vector3
                    {
                        X = MathF.Atan2(smoothPos.Tangent.Z, smoothPos.Tangent.X) - MathF.PI / 2,
                        Y = (MathF.Atan2(new Vector2(smoothPos.Tangent.Z, smoothPos.Tangent.X).Length(), smoothPos.Tangent.Y) - MathF.PI / 2) * -1f,
                        Z = ops.GetCamber(CurrentSplinePointId, _currentVecProgress / _currentVecLength)
                    };
                }
            }
            else if (LateralOffset != 0f)
            {
                // Calculate right vector (perpendicular to forward direction in XZ plane)
                Vector3 right = Vector3.Cross(smoothPos.Tangent, Vector3.UnitY);
                if (right.LengthSquared() > 0.001f)
                {
                    right = Vector3.Normalize(right);
                    position += right * LateralOffset;
                }

                // === ADD STEERING ROTATION DURING LANE CHANGE ===
                float steeringYaw = CalculateLaneChangeSteeringYaw();
                rotation.X += steeringYaw;
            }
        }
        // === END SXR ===

        float tyreAngularSpeed = GetTyreAngularSpeed(CurrentSpeed, EntryCar.TyreDiameterMeters);
        byte encodedTyreAngularSpeed =  (byte) (Math.Clamp(MathF.Round(MathF.Log10(tyreAngularSpeed + 1.0f) * 20.0f) * Math.Sign(tyreAngularSpeed), -100.0f, 154.0f) + 100.0f);

        Status.Timestamp = _sessionManager.ServerTimeMilliseconds;
        Status.Position = position;
        Status.Rotation = rotation;
        Status.Velocity = smoothPos.Tangent * CurrentSpeed;
        Status.SteerAngle = 127;
        Status.WheelAngle = 127;
        Status.TyreAngularSpeed[0] = encodedTyreAngularSpeed;
        Status.TyreAngularSpeed[1] = encodedTyreAngularSpeed;
        Status.TyreAngularSpeed[2] = encodedTyreAngularSpeed;
        Status.TyreAngularSpeed[3] = encodedTyreAngularSpeed;
        Status.EngineRpm = (ushort)MathUtils.Lerp(EntryCar.AiIdleEngineRpm, EntryCar.AiMaxEngineRpm, CurrentSpeed / _configuration.Extra.AiParams.MaxSpeedMs);
        Status.StatusFlag = GetLights(_configuration.Extra.AiParams.EnableDaytimeLights, _weatherManager.CurrentSunPosition, _randomTwilight)
                            | (_sessionManager.ServerTimeMilliseconds < _stoppedForCollisionUntil || CurrentSpeed < 20 / 3.6f ? CarStatusFlags.HazardsOn : 0)
                            | (CurrentSpeed == 0 || Acceleration < 0 ? CarStatusFlags.BrakeLightsOn : 0)
                            | (_stoppedForObstacle && _sessionManager.ServerTimeMilliseconds > _obstacleHonkStart && _sessionManager.ServerTimeMilliseconds < _obstacleHonkEnd ? CarStatusFlags.Horn : 0)
                            | GetWiperSpeed(_weatherManager.CurrentWeather.RainIntensity)
                            | _indicator;
        Status.Gear = 2;
    }
        
    private static float GetTyreAngularSpeed(float speed, float wheelDiameter)
    {
        return speed / (MathF.PI * wheelDiameter) * 6;
    }

    private static CarStatusFlags GetWiperSpeed(float rainIntensity)
    {
        return rainIntensity switch
        {
            < 0.05f => 0,
            < 0.25f => CarStatusFlags.WiperLevel1,
            < 0.5f => CarStatusFlags.WiperLevel2,
            _ => CarStatusFlags.WiperLevel3
        };
    }
    
    private static CarStatusFlags GetLights(bool daytimeLights, SunPosition? sunPosition, double twilight)
    {
        const CarStatusFlags lightFlags = CarStatusFlags.LightsOn | CarStatusFlags.HighBeamsOff;
        if (daytimeLights || sunPosition == null) return lightFlags;

        return sunPosition.Value.Altitude < twilight ? lightFlags : 0;
    }
    
    // ============================================================================
    // SXR LANE CHANGE SYSTEM
    // ============================================================================
    
    // Helper to get lane change params
    private LaneChangeParams LaneChangeConfig => _configuration.Extra.LaneChangeParams;
    private float LaneChangeCooldown => LaneChangeConfig.LaneChangeCooldownSeconds;
    private float LaneWidth => LaneChangeConfig.LaneWidthMeters;
    private bool LaneChangesEnabled => LaneChangeConfig.EnableLaneChanges;
    private bool LaneChangeDebugLogging => LaneChangeConfig.DebugLogging;
    private bool ProactiveLaneChangesEnabled => LaneChangeConfig.EnableProactiveLaneChanges;
    
    /// <summary>
    /// Start a smooth lane change to an adjacent lane
    /// </summary>
    public void StartLaneChange(int targetPointId, bool isLeft)
    {
        if (_laneChangeActive) return;
        if (targetPointId < 0) return;
        if (!LaneChangesEnabled) return;
        
        float currentTimeSec = _sessionManager.ServerTimeMilliseconds / 1000f;
        
        _laneChangeActive = true;
        _laneChangeSourcePointId = CurrentSplinePointId;
        _laneChangeTargetPointId = targetPointId;
        _laneChangeStartTime = currentTimeSec;
        
        // Aggressive drivers change lanes faster
        float baseDuration = CalculateLaneChangeDuration(CurrentSpeed);
        float aggressionSpeedMultiplier = (float)MathUtils.Lerp(1.2f, 0.7f, Aggressiveness);
        _laneChangeDuration = baseDuration * aggressionSpeedMultiplier;
        
        _laneChangeIsLeft = isLeft;
        _lastLaneChangeTime = currentTimeSec;
        LateralOffset = 0;
        
        // Set turn indicator
        _indicator = isLeft ? CarStatusFlags.IndicateLeft : CarStatusFlags.IndicateRight;
        
        if (LaneChangeDebugLogging)
        {
            Log.Debug("AI {SessionId} [Aggr:{Aggression:F2}] starting {Direction} lane change (duration: {Duration:F1}s)", 
                EntryCar.SessionId, Aggressiveness, isLeft ? "left" : "right", _laneChangeDuration);
        }
    }
    
    /// <summary>
    /// Update lane change progress - called from Update()
    /// </summary>
    private void UpdateLaneChange(float currentTimeSec)
    {
        if (!_laneChangeActive) return;

        // === HANDLE ABORT ANIMATION ===
        if (_laneChangeAborting)
        {
            UpdateLaneChangeAbort(currentTimeSec);
            return;
        }

        // Safety check - abort if adjacent lane no longer exists at current position
        var currentPoint = _spline.Points[CurrentSplinePointId];
        int adjacentLane = _laneChangeIsLeft ? currentPoint.LeftId : currentPoint.RightId;
        if (adjacentLane < 0)
        {
            // Lane ended - start smooth abort
            StartLaneChangeAbort(currentTimeSec, "target lane ended");
            return;
        }

        // === COLLISION DETECTION DURING MERGE ===
        // Check if something appeared in our target lane during the merge
        if (HasCollisionInTargetLane(adjacentLane))
        {
            StartLaneChangeAbort(currentTimeSec, "collision detected in target lane");
            return;
        }

        float elapsed = currentTimeSec - _laneChangeStartTime;
        float progress = Math.Clamp(elapsed / _laneChangeDuration, 0f, 1f);

        // Quintic polynomial for smooth S-curve: 10t³ - 15t⁴ + 6t⁵
        float t = progress;
        float t2 = t * t;
        float t3 = t2 * t;
        float t4 = t3 * t;
        float t5 = t4 * t;
        float polynomial = 10f * t3 - 15f * t4 + 6f * t5;

        // Calculate lateral offset (negative for left, positive for right)
        LateralOffset = LaneWidth * polynomial * (_laneChangeIsLeft ? -1f : 1f);

        // Lane change complete
        if (progress >= 1f)
        {
            FinalizeLaneChange();
        }
    }

    /// <summary>
    /// Check if there's a collision risk in the target lane during an active lane change.
    /// This checks both AI traffic and players.
    /// </summary>
    private bool HasCollisionInTargetLane(int targetLanePointId)
    {
        // Calculate how far we've progressed into the lane change
        float currentTimeSec = _sessionManager.ServerTimeMilliseconds / 1000f;
        float elapsed = currentTimeSec - _laneChangeStartTime;
        float progress = Math.Clamp(elapsed / _laneChangeDuration, 0f, 1f);

        // Only check after we've started moving (past 10% progress)
        // and before we're almost done (before 90% progress)
        if (progress < 0.1f || progress > 0.9f)
            return false;

        // The further into the lane change, the tighter the collision margin
        // At 50% progress, we're in the middle of two lanes - highest risk
        float progressFactor = 1f - MathF.Abs(progress - 0.5f) * 2f; // 0 at edges, 1 at middle
        float collisionMargin = 8f + (12f * progressFactor); // 8-20m depending on progress

        // Check for AI traffic in target lane at our position
        var points = _spline.Points;
        int pointId = targetLanePointId;

        // Check a small range around our current position
        float checkDistance = 25f;
        float distanceTravelled = 0;

        // Check ahead
        while (distanceTravelled < checkDistance && pointId >= 0)
        {
            var slowest = _spline.SlowestAiStates[pointId];
            if (slowest != null && slowest != this && slowest.Initialized)
            {
                float worldDist = Vector3.Distance(Status.Position, slowest.Status.Position);
                if (worldDist < collisionMargin)
                {
                    if (LaneChangeDebugLogging)
                    {
                        Log.Debug("AI {SessionId} lane change collision risk: AI {OtherId} at {Dist:F1}m (progress {Progress:P0})",
                            EntryCar.SessionId, slowest.EntryCar.SessionId, worldDist, progress);
                    }
                    return true;
                }
            }

            ref readonly var point = ref points[pointId];
            if (point.NextId < 0) break;
            distanceTravelled += point.Length;
            pointId = point.NextId;
        }

        // Check behind
        pointId = targetLanePointId;
        distanceTravelled = 0;
        while (distanceTravelled < checkDistance && pointId >= 0)
        {
            ref readonly var point = ref points[pointId];
            if (point.PreviousId < 0) break;
            distanceTravelled += point.Length;
            pointId = point.PreviousId;

            var slowest = _spline.SlowestAiStates[pointId];
            if (slowest != null && slowest != this && slowest.Initialized)
            {
                float worldDist = Vector3.Distance(Status.Position, slowest.Status.Position);
                if (worldDist < collisionMargin)
                {
                    if (LaneChangeDebugLogging)
                    {
                        Log.Debug("AI {SessionId} lane change collision risk: AI {OtherId} behind at {Dist:F1}m",
                            EntryCar.SessionId, slowest.EntryCar.SessionId, worldDist);
                    }
                    return true;
                }
            }
        }

        // Check for PLAYERS in the target lane (they're not tracked in SlowestAiStates)
        for (var i = 0; i < _entryCarManager.EntryCars.Length; i++)
        {
            var playerCar = _entryCarManager.EntryCars[i];
            if (playerCar.Client?.HasSentFirstUpdate != true) continue;
            if (playerCar.AiControlled) continue; // Skip AI, already checked above

            float worldDist = Vector3.Distance(Status.Position, playerCar.Status.Position);

            // Players can be moving fast, so we need a larger margin
            float playerCollisionMargin = collisionMargin + playerCar.Status.Velocity.Length() * 0.5f;

            if (worldDist < playerCollisionMargin)
            {
                // Check if player is roughly in our target lane direction
                // (not behind us in our current lane)
                float heightDiff = MathF.Abs(playerCar.Status.Position.Y - Status.Position.Y);
                if (heightDiff < 3f) // Same level
                {
                    if (LaneChangeDebugLogging)
                    {
                        Log.Debug("AI {SessionId} lane change collision risk: PLAYER at {Dist:F1}m, speed {Speed:F0}km/h",
                            EntryCar.SessionId, worldDist, playerCar.Status.Velocity.Length() * 3.6f);
                    }
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Start aborting the lane change - smooth return to original lane
    /// </summary>
    private void StartLaneChangeAbort(float currentTimeSec, string reason)
    {
        _laneChangeAborting = true;
        _laneChangeAbortStartTime = currentTimeSec;
        _laneChangeAbortStartOffset = LateralOffset;

        // Turn on hazards briefly to indicate abort
        _indicator = CarStatusFlags.HazardsOn;

        if (LaneChangeDebugLogging)
        {
            Log.Debug("AI {SessionId} aborting lane change: {Reason} (offset: {Offset:F2}m)",
                EntryCar.SessionId, reason, LateralOffset);
        }
    }

    /// <summary>
    /// Update the abort animation - smoothly return to original lane
    /// </summary>
    private void UpdateLaneChangeAbort(float currentTimeSec)
    {
        // Abort takes about 1-2 seconds depending on how far we got
        float abortDuration = MathF.Abs(_laneChangeAbortStartOffset) / LaneWidth * 2f;
        abortDuration = Math.Clamp(abortDuration, 0.5f, 2f);

        float elapsed = currentTimeSec - _laneChangeAbortStartTime;
        float progress = Math.Clamp(elapsed / abortDuration, 0f, 1f);

        // Smooth ease-out for abort
        float easeOut = 1f - (1f - progress) * (1f - progress);

        // Interpolate back to zero offset
        LateralOffset = _laneChangeAbortStartOffset * (1f - easeOut);

        // Abort complete
        if (progress >= 1f)
        {
            _laneChangeActive = false;
            _laneChangeAborting = false;
            LateralOffset = 0;
            _indicator = 0;

            if (LaneChangeDebugLogging)
            {
                Log.Debug("AI {SessionId} lane change abort complete", EntryCar.SessionId);
            }
        }
    }
    
    /// <summary>
    /// Called when lane change completes to switch spline point
    /// </summary>
    private void FinalizeLaneChange()
    {
        // Get the adjacent lane point from our CURRENT position, not the stale starting position
        var currentPoint = _spline.Points[CurrentSplinePointId];
        int targetPointId = _laneChangeIsLeft ? currentPoint.LeftId : currentPoint.RightId;
        
        // Reset state
        _laneChangeActive = false;
        LateralOffset = 0;
        _indicator = 0;
        
        if (targetPointId < 0)
        {
            // No adjacent lane at current position - lane ended during change
            if (LaneChangeDebugLogging)
            {
                Log.Warning("AI {SessionId} lane change failed - no adjacent lane at current position", EntryCar.SessionId);
            }
            return;
        }
        
        int sourcePointId = CurrentSplinePointId;
        
        // Update spline tracking
        _spline.SlowestAiStates.Leave(sourcePointId, this);
        _spline.SlowestAiStates.Enter(targetPointId, this);
        
        // Switch to new spline point
        _currentSplinePointId = targetPointId;
        
        // Recalculate tangents for new lane
        _junctionEvaluator.Clear();
        if (_junctionEvaluator.TryNext(CurrentSplinePointId, out var nextPointId))
        {
            _currentVecLength = (_spline.Points[nextPointId].Position - _spline.Points[CurrentSplinePointId].Position).Length();
            // Preserve progress ratio within the segment
            _currentVecProgress = Math.Min(_currentVecProgress, _currentVecLength * 0.9f);
            CalculateTangents();
        }
        
        if (LaneChangeDebugLogging)
        {
            Log.Debug("AI {SessionId} completed lane change to point {PointId}", 
                EntryCar.SessionId, targetPointId);
        }
    }
    
    /// <summary>
    /// Calculate steering yaw offset during lane change for natural-looking movement.
    /// Uses derivative of the quintic polynomial to determine steering intensity.
    /// </summary>
    private float CalculateLaneChangeSteeringYaw()
    {
        float currentTimeSec = _sessionManager.ServerTimeMilliseconds / 1000f;

        // Handle abort case - steer back toward original lane
        if (_laneChangeAborting)
        {
            float abortDuration = MathF.Abs(_laneChangeAbortStartOffset) / LaneWidth * 2f;
            abortDuration = Math.Clamp(abortDuration, 0.5f, 2f);
            float elapsed = currentTimeSec - _laneChangeAbortStartTime;
            float progress = Math.Clamp(elapsed / abortDuration, 0f, 1f);

            // Steer back toward original lane (opposite direction)
            // Intensity decreases as we approach original position
            float abortSteerIntensity = (1f - progress) * 0.5f;

            // Max steering angle during abort (less aggressive than forward change)
            float maxAbortSteerAngle = 0.06f; // ~3.4 degrees

            // Direction is opposite to the original lane change direction
            float abortDirection = _laneChangeIsLeft ? 1f : -1f; // Steer right if was going left

            return abortDirection * maxAbortSteerAngle * abortSteerIntensity;
        }

        // Normal lane change steering
        float normalElapsed = currentTimeSec - _laneChangeStartTime;
        float normalProgress = Math.Clamp(normalElapsed / _laneChangeDuration, 0f, 1f);

        // Derivative of quintic polynomial: 30t² - 60t³ + 30t⁴
        // This gives us the rate of lateral movement, which corresponds to steering intensity
        float t = normalProgress;
        float t2 = t * t;
        float t3 = t2 * t;
        float t4 = t3 * t;
        float derivative = 30f * t2 - 60f * t3 + 30f * t4;

        // Normalize derivative (max value is 1.875 at t≈0.33)
        float steerIntensity = derivative / 1.875f;

        // Max steering angle in radians (~5-8 degrees depending on speed)
        // Faster = less steering angle needed, slower = more pronounced steering
        float speedFactor = Math.Clamp(CurrentSpeed / 30f, 0.5f, 1.5f); // 30 m/s = 108 km/h baseline
        float maxSteerAngle = 0.12f / speedFactor; // ~7 degrees at baseline, less at high speed

        // Direction: left lane change = negative yaw (turn left), right = positive
        float direction = _laneChangeIsLeft ? -1f : 1f;

        return direction * maxSteerAngle * steerIntensity;
    }

    /// <summary>
    /// Calculate lane change duration based on speed
    /// </summary>
    private float CalculateLaneChangeDuration(float speedMs)
    {
        float baseDuration = LaneChangeConfig.BaseLaneChangeDurationSeconds;
        float minDuration = LaneChangeConfig.MinLaneChangeDurationSeconds;
        float maxDuration = LaneChangeConfig.MaxLaneChangeDurationSeconds;
        
        // ~baseDuration at 100 km/h, scales up with speed
        float baseSpeed = 27.8f;   // 100 km/h
        float speedFactor = MathF.Max(1f, speedMs / baseSpeed);
        float duration = baseDuration * (1f + 0.5f * MathF.Log(speedFactor));
        return Math.Clamp(duration, minDuration, maxDuration);
    }
    
    /// <summary>
    /// Try to change lanes using MOBIL algorithm (reactive - when blocked)
    /// </summary>
    private void TryMobilLaneChange(AiState currentLeader, float currentLeaderDistance)
    {
        if (!LaneChangesEnabled) return;
        
        float minSpeed = LaneChangeConfig.MinSpeedForLaneChange;
        float maxLeaderDist = LaneChangeConfig.MaxLeaderDistanceForLaneChange;
        
        // Aggressiveness affects cooldown: passive = longer wait, aggressive = shorter
        float baseCooldown = LaneChangeCooldown;
        float effectiveCooldown = (float)MathUtils.Lerp(baseCooldown * 2f, baseCooldown * 0.3f, Aggressiveness);
        
        // Check cooldown
        float currentTimeSec = _sessionManager.ServerTimeMilliseconds / 1000f;
        if (currentTimeSec - _lastLaneChangeTime < effectiveCooldown)
            return;
        
        // Don't change lanes at low speed or if gap is large
        // Aggressive drivers will try lane changes at smaller gaps
        float effectiveMaxLeaderDist = (float)MathUtils.Lerp(maxLeaderDist * 0.5f, maxLeaderDist * 1.5f, Aggressiveness);
        if (CurrentSpeed < minSpeed || currentLeaderDistance > effectiveMaxLeaderDist)
            return;
        
        var point = _spline.Points[CurrentSplinePointId];
        
        // Try left lane first (passing lane in Japan)
        if (point.LeftId >= 0 && EvaluateLaneChange(point.LeftId, currentLeader, currentLeaderDistance, true))
        {
            StartLaneChange(point.LeftId, true);
            return;
        }
        
        // Try right lane
        if (point.RightId >= 0 && EvaluateLaneChange(point.RightId, currentLeader, currentLeaderDistance, false))
        {
            StartLaneChange(point.RightId, false);
        }
    }
    
    /// <summary>
    /// Plan a proactive lane change - finds a gap but doesn't execute until obstacle is met.
    /// This is FIX #3: Look ahead early, execute at obstacle.
    /// </summary>
    private void TryPlanProactiveLaneChange(AiState aheadVehicle, float aheadDistance)
    {
        if (!LaneChangesEnabled) return;
        if (!ProactiveLaneChangesEnabled) return;
        if (Aggressiveness < 0.4f) return; // Only medium-aggressive+ drivers do this

        float currentTimeSec = _sessionManager.ServerTimeMilliseconds / 1000f;

        // Shorter cooldown for proactive checks
        float proactiveCooldown = (float)MathUtils.Lerp(8f, 2f, Aggressiveness);
        if (currentTimeSec - _lastLaneChangeTime < proactiveCooldown)
            return;

        var point = _spline.Points[CurrentSplinePointId];

        // For proactive changes, prefer the passing lane (left in Japan) more strongly
        // Aggressive drivers want to be in the fast lane
        if (point.LeftId >= 0)
        {
            var (leftLeader, leftLeaderDist) = FindLeaderInLane(point.LeftId, 250f);

            // Check if left lane is clear or has faster traffic
            bool leftIsBetter = leftLeader == null ||
                (leftLeaderDist > aheadDistance * 1.5f) ||
                (leftLeader.CurrentSpeed > aheadVehicle.CurrentSpeed + 3f);

            if (leftIsBetter && EvaluateProactiveLaneChange(point.LeftId, true))
            {
                // PLAN the lane change instead of executing
                _hasPlannedLaneChange = true;
                _plannedLaneChangeIsLeft = true;
                _plannedTargetPointId = point.LeftId;
                _plannedLaneChangeGapDistance = leftLeaderDist;

                if (LaneChangeDebugLogging)
                {
                    Log.Debug("AI {SessionId} [Aggr:{Aggression:F2}] PLANNED left lane change, gap at {GapDist:F0}m, obstacle at {ObstDist:F0}m",
                        EntryCar.SessionId, Aggressiveness, leftLeaderDist, aheadDistance);
                }
                return;
            }
        }

        // Try right lane if left isn't available or viable
        if (point.RightId >= 0)
        {
            var (rightLeader, rightLeaderDist) = FindLeaderInLane(point.RightId, 250f);

            bool rightIsBetter = rightLeader == null ||
                (rightLeaderDist > aheadDistance * 1.3f) ||
                (rightLeader.CurrentSpeed > aheadVehicle.CurrentSpeed + 2f);

            if (rightIsBetter && EvaluateProactiveLaneChange(point.RightId, false))
            {
                // PLAN the lane change instead of executing
                _hasPlannedLaneChange = true;
                _plannedLaneChangeIsLeft = false;
                _plannedTargetPointId = point.RightId;
                _plannedLaneChangeGapDistance = rightLeaderDist;

                if (LaneChangeDebugLogging)
                {
                    Log.Debug("AI {SessionId} [Aggr:{Aggression:F2}] PLANNED right lane change, gap at {GapDist:F0}m, obstacle at {ObstDist:F0}m",
                        EntryCar.SessionId, Aggressiveness, rightLeaderDist, aheadDistance);
                }
            }
        }
    }
    
    /// <summary>
    /// Evaluate proactive lane change with less strict requirements for aggressive drivers.
    /// Returns true if a gap is available - but this doesn't mean we execute immediately!
    /// </summary>
    private bool EvaluateProactiveLaneChange(int targetLanePointId, bool isLeft)
    {
        var ops = _spline.Operations;

        if (!ops.IsSameDirection(CurrentSplinePointId, targetLanePointId))
            return false;

        // === FIX #1: CHECK FOR ADJACENT VEHICLES ===
        float adjacentSafetyMargin = (float)MathUtils.Lerp(18f, 10f, Aggressiveness);
        if (HasAdjacentVehicle(targetLanePointId, adjacentSafetyMargin))
            return false;

        // Aggressive drivers accept tighter gaps
        float safeGapMultiplier = (float)MathUtils.Lerp(1.2f, 0.6f, Aggressiveness);
        float baseSafeGap = CurrentSpeed * 1.5f; // ~1.5 seconds at current speed
        float minSafeGap = baseSafeGap * safeGapMultiplier;

        float lookahead = (float)MathUtils.Lerp(150f, 250f, Aggressiveness);
        float lookbehind = (float)MathUtils.Lerp(80f, 40f, Aggressiveness); // Aggressive = checks less behind

        // Find vehicles in target lane
        var (targetLeader, targetLeaderDist) = FindLeaderInLane(targetLanePointId, lookahead);
        var (targetFollower, targetFollowerDist) = FindFollowerInLane(targetLanePointId, lookbehind);

        // Safety check - even aggressive drivers won't cut off someone too close
        if (targetFollower != null && targetFollowerDist > 0 && targetFollowerDist < minSafeGap * 0.5f)
        {
            return false;
        }

        // Check follower won't need to brake too hard
        if (targetFollower != null && targetFollowerDist > 0)
        {
            float safeDecel = (float)MathUtils.Lerp(3f, 5f, Aggressiveness); // Aggressive accepts harder braking from followers
            float followerAccelAfter = CalculateIdmAcceleration(
                targetFollower.CurrentSpeed,
                targetFollower.InitialMaxSpeed,
                targetFollowerDist,
                CurrentSpeed);

            if (followerAccelAfter < -safeDecel)
                return false;
        }

        // For proactive changes, we just need the lane to be better, not necessarily blocked
        return targetLeader == null || targetLeaderDist > minSafeGap;
    }
    
    /// <summary>
    /// Evaluate if a lane change is beneficial and safe using MOBIL, scaled by aggressiveness
    /// </summary>
    private bool EvaluateLaneChange(int targetLanePointId, AiState currentLeader, float currentLeaderDistance, bool isLeft)
    {
        var ops = _spline.Operations;

        // Check same direction
        if (!ops.IsSameDirection(CurrentSplinePointId, targetLanePointId))
            return false;

        // === FIX #1: CHECK FOR ADJACENT VEHICLES ===
        // Prevent changing lanes directly into another car
        float adjacentSafetyMargin = (float)MathUtils.Lerp(20f, 12f, Aggressiveness);
        if (HasAdjacentVehicle(targetLanePointId, adjacentSafetyMargin))
            return false;

        // === AGGRESSIVENESS-SCALED PARAMETERS ===
        // Passive (0): Very polite, high safety margins, high threshold
        // Aggressive (1): Low politeness, accepts tighter gaps, low threshold

        float basePoliteness = LaneChangeConfig.MobilPoliteness;
        float baseSafeDecel = LaneChangeConfig.MobilSafeDeceleration;
        float baseThreshold = LaneChangeConfig.MobilThreshold;
        float keepSlowLaneBias = LaneChangeConfig.MobilKeepSlowLaneBias;

        // Scale parameters by aggressiveness
        float politeness = (float)MathUtils.Lerp(basePoliteness + 0.3f, basePoliteness * 0.2f, Aggressiveness);
        float safeDecel = (float)MathUtils.Lerp(baseSafeDecel * 0.7f, baseSafeDecel * 1.3f, Aggressiveness);
        float threshold = (float)MathUtils.Lerp(baseThreshold + 0.2f, baseThreshold * 0.3f, Aggressiveness);

        // Aggressive drivers have less bias to stay in slow lane - they want the fast lane
        float effectiveBias = (float)MathUtils.Lerp(keepSlowLaneBias * 1.5f, keepSlowLaneBias * 0.3f, Aggressiveness);

        // Aggressive drivers look further ahead and less behind
        float lookahead = (float)MathUtils.Lerp(150f, 300f, Aggressiveness);
        float lookbehind = (float)MathUtils.Lerp(120f, 60f, Aggressiveness);

        // Find leader and follower in target lane
        var (targetLeader, targetLeaderDist) = FindLeaderInLane(targetLanePointId, lookahead);
        var (targetFollower, targetFollowerDist) = FindFollowerInLane(targetLanePointId, lookbehind);

        // === SAFETY CRITERION ===
        // New follower must not need to brake too hard
        if (targetFollower != null && targetFollowerDist > 0)
        {
            // Also check absolute minimum gap (even aggressive drivers won't cut someone off at 5m)
            float absoluteMinGap = (float)MathUtils.Lerp(25f, 12f, Aggressiveness);
            if (targetFollowerDist < absoluteMinGap)
                return false;

            float followerAccelAfter = CalculateIdmAcceleration(
                targetFollower.CurrentSpeed,
                targetFollower.InitialMaxSpeed,
                targetFollowerDist,
                CurrentSpeed);

            if (followerAccelAfter < -safeDecel)
                return false;
        }

        // === INCENTIVE CRITERION ===

        // My current acceleration
        float accCurrent = CalculateIdmAcceleration(CurrentSpeed, InitialMaxSpeed, currentLeaderDistance, currentLeader.CurrentSpeed);

        // My acceleration in target lane
        float accTarget = targetLeader != null && targetLeaderDist > 0
            ? CalculateIdmAcceleration(CurrentSpeed, InitialMaxSpeed, targetLeaderDist, targetLeader.CurrentSpeed)
            : CalculateIdmFreeRoad(CurrentSpeed, InitialMaxSpeed);

        // Follower's disadvantage
        float followerDisadvantage = 0f;
        if (targetFollower != null && targetFollowerDist > 0)
        {
            float followerAccelBefore = CalculateIdmFreeRoad(targetFollower.CurrentSpeed, targetFollower.InitialMaxSpeed);
            float followerAccelAfter = CalculateIdmAcceleration(
                targetFollower.CurrentSpeed,
                targetFollower.InitialMaxSpeed,
                targetFollowerDist,
                CurrentSpeed);
            followerDisadvantage = followerAccelBefore - followerAccelAfter;
        }

        // My advantage
        float myAdvantage = accTarget - accCurrent;

        // Keep-left bias for Japan (left-hand traffic)
        float bias = isLeft ? -effectiveBias : effectiveBias;

        // MOBIL criterion
        float incentive = myAdvantage - politeness * followerDisadvantage - bias;

        if (LaneChangeDebugLogging && incentive > threshold * 0.5f)
        {
            Log.Debug("AI {SessionId} [Aggr:{Aggression:F2}] lane change eval: incentive={Incentive:F2}, threshold={Threshold:F2}, politeness={Politeness:F2}",
                EntryCar.SessionId, Aggressiveness, incentive, threshold, politeness);
        }

        return incentive > threshold;
    }
    
    /// <summary>
    /// Check if there's a vehicle ADJACENT to us (same position, different lane).
    /// This prevents changing lanes directly into another car.
    /// </summary>
    private bool HasAdjacentVehicle(int targetLanePointId, float safetyMargin)
    {
        if (targetLanePointId < 0) return true; // No lane = blocked

        var ops = _spline.Operations;
        if (!ops.IsSameDirection(CurrentSplinePointId, targetLanePointId))
            return true; // Wrong direction = blocked

        var points = _spline.Points;

        // Check a range of points around our current position
        // We need to check both ahead and behind because cars occupy space
        float checkDistance = safetyMargin + 10f; // Extra buffer for car length

        // Check ahead in target lane
        int pointId = targetLanePointId;
        float distanceTravelled = 0;
        while (distanceTravelled < checkDistance)
        {
            var slowest = _spline.SlowestAiStates[pointId];
            if (slowest != null && slowest != this && slowest.Initialized && !slowest.IsChangingLanes)
            {
                // Check actual world distance to be sure
                float worldDist = Vector3.Distance(Status.Position, slowest.Status.Position);
                if (worldDist < safetyMargin)
                {
                    if (LaneChangeDebugLogging)
                    {
                        Log.Debug("AI {SessionId} blocked by adjacent vehicle {OtherId} at distance {Dist:F1}m",
                            EntryCar.SessionId, slowest.EntryCar.SessionId, worldDist);
                    }
                    return true;
                }
            }

            ref readonly var point = ref points[pointId];
            if (point.NextId < 0) break;
            distanceTravelled += point.Length;
            pointId = point.NextId;
        }

        // Check behind in target lane
        pointId = targetLanePointId;
        distanceTravelled = 0;
        while (distanceTravelled < checkDistance)
        {
            ref readonly var point = ref points[pointId];
            if (point.PreviousId < 0) break;

            distanceTravelled += point.Length;
            pointId = point.PreviousId;

            var slowest = _spline.SlowestAiStates[pointId];
            if (slowest != null && slowest != this && slowest.Initialized && !slowest.IsChangingLanes)
            {
                float worldDist = Vector3.Distance(Status.Position, slowest.Status.Position);
                if (worldDist < safetyMargin)
                {
                    if (LaneChangeDebugLogging)
                    {
                        Log.Debug("AI {SessionId} blocked by adjacent vehicle {OtherId} behind at distance {Dist:F1}m",
                            EntryCar.SessionId, slowest.EntryCar.SessionId, worldDist);
                    }
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Find leader vehicle in an adjacent lane
    /// </summary>
    private (AiState? Leader, float Distance) FindLeaderInLane(int lanePointId, float maxDistance = 200f)
    {
        if (lanePointId < 0) return (null, -1);
        
        var ops = _spline.Operations;
        if (!ops.IsSameDirection(CurrentSplinePointId, lanePointId))
            return (null, -1);
        
        var points = _spline.Points;
        float distanceTravelled = 0;
        int pointId = lanePointId;
        
        while (distanceTravelled < maxDistance)
        {
            ref readonly var point = ref points[pointId];
            if (point.NextId < 0) break;
            
            distanceTravelled += point.Length;
            pointId = point.NextId;
            
            var slowest = _spline.SlowestAiStates[pointId];
            if (slowest != null && slowest.Initialized)
            {
                return (slowest, distanceTravelled);
            }
        }
        
        return (null, -1);
    }
    
    /// <summary>
    /// Find follower vehicle in an adjacent lane
    /// </summary>
    private (AiState? Follower, float Distance) FindFollowerInLane(int lanePointId, float maxDistance = 100f)
    {
        if (lanePointId < 0) return (null, -1);
        
        var ops = _spline.Operations;
        if (!ops.IsSameDirection(CurrentSplinePointId, lanePointId))
            return (null, -1);
        
        var points = _spline.Points;
        float distanceTravelled = 0;
        int pointId = lanePointId;
        
        while (distanceTravelled < maxDistance)
        {
            ref readonly var point = ref points[pointId];
            if (point.PreviousId < 0) break;
            
            distanceTravelled += point.Length;
            pointId = point.PreviousId;
            
            var slowest = _spline.SlowestAiStates[pointId];
            if (slowest != null && slowest.Initialized)
            {
                return (slowest, distanceTravelled);
            }
        }
        
        return (null, -1);
    }
    
    /// <summary>
    /// IDM acceleration calculation
    /// </summary>
    private static float CalculateIdmAcceleration(float mySpeed, float desiredSpeed, float gap, float leaderSpeed)
    {
        const float maxAccel = 2.5f;
        const float comfortDecel = 4.0f;
        const float minGap = 2.0f;
        const float timeHeadway = 1.2f;
        const float delta = 4.0f;
        
        float approachingRate = mySpeed - leaderSpeed;
        float sqrtAB = MathF.Sqrt(maxAccel * comfortDecel);
        float sStar = minGap + mySpeed * timeHeadway + (mySpeed * approachingRate) / (2 * sqrtAB);
        sStar = MathF.Max(minGap, sStar);
        
        float freeRoadTerm = desiredSpeed > 0.1f ? MathF.Pow(mySpeed / desiredSpeed, delta) : 0f;
        float interactionTerm = gap > 0.1f ? MathF.Pow(sStar / gap, 2) : 1.0f;
        
        return maxAccel * (1f - freeRoadTerm - interactionTerm);
    }
    
    /// <summary>
    /// IDM free road acceleration
    /// </summary>
    private static float CalculateIdmFreeRoad(float mySpeed, float desiredSpeed)
    {
        const float maxAccel = 2.5f;
        const float delta = 4.0f;
        float freeRoadTerm = desiredSpeed > 0.1f ? MathF.Pow(mySpeed / desiredSpeed, delta) : 0f;
        return maxAccel * (1f - freeRoadTerm);
    }
}
