using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using AssettoServer.Server;
using AssettoServer.Shared.Network.Packets.Shared;
using Serilog;

namespace SXRSXRSPBattlePlugin;

/// <summary>
/// Represents an active SP Battle between two players.
/// Implements TXR-style spirit point drain based on follow distance.
/// </summary>
public class Battle
{
    // Participants
    public EntryCar Challenger { get; }
    public EntryCar Challenged { get; }
    public EntryCar? Leader { get; private set; }
    public EntryCar? Follower { get; private set; }
    
    // SP State
    public float ChallengerSP { get; private set; }
    public float ChallengedSP { get; private set; }
    public float ChallengerMaxSP { get; }
    public float ChallengedMaxSP { get; }
    public float ChallengerDrainRate { get; private set; }
    public float ChallengedDrainRate { get; private set; }
    
    // Battle State
    public bool HasStarted { get; private set; }
    public bool IsFinished { get; private set; }
    public bool LineUpRequired { get; }
    public EntryCar? Winner { get; private set; }
    public BattleEndReason EndReason { get; private set; }
    public long StartTime { get; private set; }
    public float CurrentDistance { get; private set; }
    
    // Events for UI updates
    public event Action<Battle>? OnBattleUpdate;
    public event Action<Battle>? OnBattleStarted;
    public event Action<Battle>? OnBattleEnded;
    
    // Internal state
    private long _lastOvertakeTime;
    private long _lastUpdateTime;
    private Vector3 _lastLeaderPosition;
    private readonly string _challengerName;
    private readonly string _challengedName;
    private readonly SXRSPBattleConfiguration _config;
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly SXRSPBattlePlugin _plugin;
    
    public delegate Battle Factory(EntryCar challenger, EntryCar challenged, 
        int challengerDriverLevel, int challengedDriverLevel, bool lineUpRequired = true);
    
    public Battle(
        EntryCar challenger, 
        EntryCar challenged,
        int challengerDriverLevel,
        int challengedDriverLevel,
        SXRSPBattleConfiguration config,
        SessionManager sessionManager, 
        EntryCarManager entryCarManager, 
        SXRSPBattlePlugin plugin, 
        bool lineUpRequired = true)
    {
        Challenger = challenger;
        Challenged = challenged;
        _config = config;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _plugin = plugin;
        LineUpRequired = lineUpRequired;
        
        _challengerName = Challenger.Client?.Name ?? "Unknown";
        _challengedName = Challenged.Client?.Name ?? "Unknown";
        
        // Calculate max SP with driver level bonus
        int clampedChallengerLevel = Math.Clamp(challengerDriverLevel, 0, _config.MaxDriverLevel);
        int clampedChallengedLevel = Math.Clamp(challengedDriverLevel, 0, _config.MaxDriverLevel);
        
        ChallengerMaxSP = _config.TotalSP + (clampedChallengerLevel * _config.DriverLevelBonusSPPerLevel);
        ChallengedMaxSP = _config.TotalSP + (clampedChallengedLevel * _config.DriverLevelBonusSPPerLevel);
        
        ChallengerSP = ChallengerMaxSP;
        ChallengedSP = ChallengedMaxSP;
    }
    
    public Task StartAsync()
    {
        if (!HasStarted && !IsFinished)
        {
            HasStarted = true;
            _ = Task.Run(BattleLoopAsync);
        }
        return Task.CompletedTask;
    }
    
    private async Task BattleLoopAsync()
    {
        try
        {
            // Validate participants
            if (Challenger.Client == null || Challenged.Client == null)
            {
                SendMessage("Opponent has disconnected.");
                EndBattle(null, BattleEndReason.Disconnected);
                return;
            }
            
            // Line-up phase
            if (LineUpRequired && !AreLinedUp())
            {
                SendMessage("Line up side-by-side within 15 seconds.");
                
                var lineUpTimeout = Task.Delay((int)(_config.LineUpTimeoutSeconds * 1000));
                var lineUpChecker = Task.Run(async () =>
                {
                    while (!lineUpTimeout.IsCompleted && !AreLinedUp())
                        await Task.Delay(100);
                });
                
                var completedTask = await Task.WhenAny(lineUpTimeout, lineUpChecker);
                if (completedTask == lineUpTimeout || !AreLinedUp())
                {
                    SendMessage("Failed to line up. Battle cancelled.");
                    EndBattle(null, BattleEndReason.LineUpFailed);
                    return;
                }
            }
            
            // Countdown phase
            for (int i = _config.CountdownSeconds; i > 0; i--)
            {
                if (!AreLinedUp() && LineUpRequired)
                {
                    SendMessage("Went out of line. Battle cancelled.");
                    EndBattle(null, BattleEndReason.LineUpFailed);
                    return;
                }
                
                await SendCountdownAsync(i);
                await Task.Delay(1000);
            }
            
            // GO!
            await SendCountdownAsync(0);
            StartTime = _sessionManager.ServerTimeMilliseconds;
            _lastUpdateTime = StartTime;
            _lastOvertakeTime = StartTime;
            
            OnBattleStarted?.Invoke(this);
            
            // Main battle loop
            const int updateIntervalMs = 50; // 20Hz update rate
            while (!IsFinished)
            {
                // Check disconnections
                if (Challenger.Client == null)
                {
                    EndBattle(Challenged, BattleEndReason.Disconnected);
                    return;
                }
                if (Challenged.Client == null)
                {
                    EndBattle(Challenger, BattleEndReason.Disconnected);
                    return;
                }
                
                long currentTime = _sessionManager.ServerTimeMilliseconds;
                float deltaTime = (currentTime - _lastUpdateTime) / 1000f;
                _lastUpdateTime = currentTime;
                
                // Update leader/follower
                UpdateLeader();
                
                // Calculate distance
                CurrentDistance = Vector3.Distance(Challenger.Status.Position, Challenged.Status.Position);
                
                // Check for teleport/reset (sudden large position change)
                if (Leader != null)
                {
                    float leaderMoved = Vector3.Distance(_lastLeaderPosition, Leader.Status.Position);
                    if (leaderMoved > 200) // Teleport detected
                    {
                        EndBattle(Follower, BattleEndReason.Reset);
                        return;
                    }
                    _lastLeaderPosition = Leader.Status.Position;
                }
                
                // Apply SP drain based on distance
                ApplySPDrain(deltaTime);
                
                // Apply lead bonus
                if (Leader != null && Follower != null)
                {
                    if (Leader == Challenger)
                        ChallengerSP = Math.Min(ChallengerMaxSP, ChallengerSP + _config.LeadBonusPerSecond * deltaTime);
                    else
                        ChallengedSP = Math.Min(ChallengedMaxSP, ChallengedSP + _config.LeadBonusPerSecond * deltaTime);
                }
                
                // Check win conditions
                if (ChallengerSP <= 0)
                {
                    ChallengerSP = 0;
                    EndBattle(Challenged, BattleEndReason.SPDepleted);
                    return;
                }
                if (ChallengedSP <= 0)
                {
                    ChallengedSP = 0;
                    EndBattle(Challenger, BattleEndReason.SPDepleted);
                    return;
                }
                
                // Check separation distance
                if (CurrentDistance > _config.BattleSeparationDistance)
                {
                    EndBattle(Leader, BattleEndReason.Separation);
                    return;
                }
                
                // Check no-overtake timeout
                if (currentTime - _lastOvertakeTime > _config.NoOvertakeTimeoutSeconds * 1000)
                {
                    EndBattle(Leader, BattleEndReason.Timeout);
                    return;
                }
                
                // Check max duration
                if (_config.MaxBattleDurationSeconds > 0 && 
                    currentTime - StartTime > _config.MaxBattleDurationSeconds * 1000)
                {
                    // Winner is whoever has more SP
                    var winner = ChallengerSP > ChallengedSP ? Challenger : 
                                 ChallengedSP > ChallengerSP ? Challenged : Leader;
                    EndBattle(winner, BattleEndReason.TimeLimit);
                    return;
                }
                
                // Fire update event for UI
                OnBattleUpdate?.Invoke(this);
                
                await Task.Delay(updateIntervalMs);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error in SP Battle loop");
            EndBattle(null, BattleEndReason.Error);
        }
    }
    
    [MemberNotNull(nameof(Leader))]
    private void UpdateLeader()
    {
        bool isFirstUpdate = Leader == null;
        
        if (isFirstUpdate)
        {
            _lastOvertakeTime = _sessionManager.ServerTimeMilliseconds;
            Leader = Challenger;
            Follower = Challenged;
            _lastLeaderPosition = Leader.Status.Position;
        }
        
        // Calculate relative positions using velocity-based facing
        float challengerAngle = CalculateRelativeAngle(Challenger, Challenged);
        float challengedAngle = CalculateRelativeAngle(Challenged, Challenger);
        
        float challengerSpeed = Challenger.Status.Velocity.LengthSquared();
        float challengedSpeed = Challenged.Status.Velocity.LengthSquared();
        
        float distanceSquared = Vector3.DistanceSquared(Challenger.Status.Position, Challenged.Status.Position);
        
        EntryCar oldLeader = Leader!;
        
        // Overtake detection: opponent is behind you (angle 90-270) and you're faster
        const float minSpeedForOvertake = 25f; // ~18 km/h squared
        
        if (challengerAngle > 90 && challengerAngle < 270 && 
            Leader != Challenger && 
            challengerSpeed > challengedSpeed && 
            challengerSpeed > minSpeedForOvertake &&
            distanceSquared < 2500) // Within 50m
        {
            Leader = Challenger;
            Follower = Challenged;
        }
        else if (challengedAngle > 90 && challengedAngle < 270 && 
                 Leader != Challenged && 
                 challengedSpeed > challengerSpeed && 
                 challengedSpeed > minSpeedForOvertake &&
                 distanceSquared < 2500)
        {
            Leader = Challenged;
            Follower = Challenger;
        }
        
        if (oldLeader != Leader && !isFirstUpdate)
        {
            // Overtake occurred!
            SendMessage($"{Leader.Client?.Name} overtakes!");
            _lastOvertakeTime = _sessionManager.ServerTimeMilliseconds;
            _lastLeaderPosition = Leader.Status.Position;
            
            // Apply overtake SP bonus to new leader
            if (Leader == Challenger)
                ChallengerSP = Math.Min(ChallengerMaxSP, ChallengerSP + _config.OvertakeSPBonus);
            else
                ChallengedSP = Math.Min(ChallengedMaxSP, ChallengedSP + _config.OvertakeSPBonus);
        }
    }
    
    private float CalculateRelativeAngle(EntryCar from, EntryCar to)
    {
        float angle = (float)(Math.Atan2(
            to.Status.Position.X - from.Status.Position.X,
            to.Status.Position.Z - from.Status.Position.Z) * 180 / Math.PI);
        
        if (angle < 0) angle += 360;
        
        float rotation = from.Status.GetRotationAngle();
        angle = (angle + rotation) % 360;
        
        return angle;
    }
    
    private void ApplySPDrain(float deltaTime)
    {
        if (Leader == null || Follower == null) return;
        
        // Get drain rate based on distance
        float drainRate = GetDrainRateForDistance(CurrentDistance);
        
        // Apply drain to follower
        if (Follower == Challenger)
        {
            ChallengerDrainRate = drainRate;
            ChallengedDrainRate = 0;
            ChallengerSP -= drainRate * deltaTime;
        }
        else
        {
            ChallengedDrainRate = drainRate;
            ChallengerDrainRate = 0;
            ChallengedSP -= drainRate * deltaTime;
        }
    }
    
    private float GetDrainRateForDistance(float distance)
    {
        var thresholds = _config.FollowDistanceThresholds;
        var rates = _config.DrainRatesPerSecond;
        
        // Find which bracket we're in
        for (int i = 0; i < thresholds.Length; i++)
        {
            if (distance < thresholds[i])
            {
                return rates[i];
            }
        }
        
        // Beyond last threshold - max drain
        return rates[^1];
    }
    
    /// <summary>
    /// Apply collision penalty to a car
    /// </summary>
    public void ApplyCollisionPenalty(EntryCar car, bool isWallCollision = false)
    {
        float penalty = isWallCollision ? _config.WallCollisionSPPenalty : _config.CollisionSPPenalty;
        
        if (car == Challenger)
        {
            ChallengerSP = Math.Max(0, ChallengerSP - penalty);
            Log.Debug("Collision penalty {Penalty} applied to challenger, SP now {SP}", penalty, ChallengerSP);
        }
        else if (car == Challenged)
        {
            ChallengedSP = Math.Max(0, ChallengedSP - penalty);
            Log.Debug("Collision penalty {Penalty} applied to challenged, SP now {SP}", penalty, ChallengedSP);
        }
    }
    
    private void EndBattle(EntryCar? winner, BattleEndReason reason)
    {
        if (IsFinished) return;
        
        IsFinished = true;
        Winner = winner;
        EndReason = reason;
        
        // Clear battle references
        _plugin.GetBattle(Challenger).CurrentBattle = null;
        _plugin.GetBattle(Challenged).CurrentBattle = null;
        
        // Announce result
        string resultMessage = FormatResultMessage();
        if (_config.BroadcastResults)
        {
            _entryCarManager.BroadcastChat(resultMessage);
        }
        else
        {
            SendMessage(resultMessage);
        }
        
        Log.Information("SP Battle ended: {Result}", resultMessage);
        
        OnBattleEnded?.Invoke(this);
    }
    
    private string FormatResultMessage()
    {
        if (Winner == null)
        {
            return EndReason switch
            {
                BattleEndReason.Disconnected => $"Battle between {_challengerName} and {_challengedName} cancelled (disconnect)",
                BattleEndReason.LineUpFailed => $"Battle between {_challengerName} and {_challengedName} cancelled (lineup failed)",
                BattleEndReason.Reset => $"Battle between {_challengerName} and {_challengedName} cancelled (reset detected)",
                BattleEndReason.Error => $"Battle between {_challengerName} and {_challengedName} cancelled (error)",
                _ => $"Battle between {_challengerName} and {_challengedName} ended in a draw"
            };
        }
        
        string winnerName = Winner == Challenger ? _challengerName : _challengedName;
        string loserName = Winner == Challenger ? _challengedName : _challengerName;
        
        return EndReason switch
        {
            BattleEndReason.SPDepleted => $"{winnerName} defeated {loserName}! (SP depleted)",
            BattleEndReason.Separation => $"{winnerName} defeated {loserName}! (left behind)",
            BattleEndReason.Timeout => $"{winnerName} defeated {loserName}! (timeout)",
            BattleEndReason.TimeLimit => $"{winnerName} defeated {loserName}! (time limit - higher SP)",
            _ => $"{winnerName} defeated {loserName}!"
        };
    }
    
    private bool AreLinedUp()
    {
        float distance = Vector3.Distance(Challenger.Status.Position, Challenged.Status.Position);
        
        if (!LineUpRequired)
            return distance <= _config.ChallengeMaxDistance;
        
        if (distance > _config.LineUpDistance)
            return false;
        
        // Check they're facing the same direction
        float challengerRot = Challenger.Status.GetRotationAngle();
        float challengedRot = Challenged.Status.GetRotationAngle();
        
        float angleDiff = (challengerRot - challengedRot + 180 + 360) % 360 - 180;
        return Math.Abs(angleDiff) <= 15; // Within 15 degrees
    }
    
    private void SendMessage(string message)
    {
        Challenger.Client?.SendChatMessage(message);
        Challenged.Client?.SendChatMessage(message);
    }
    
    private async Task SendCountdownAsync(int count)
    {
        string message = count > 0 ? count.ToString() : "GO!";
        
        // Sync timing based on ping difference
        bool isChallengerHighPing = Challenger.Ping > Challenged.Ping;
        EntryCar highPing = isChallengerHighPing ? Challenger : Challenged;
        EntryCar lowPing = isChallengerHighPing ? Challenged : Challenger;
        
        highPing.Client?.SendChatMessage(message);
        int pingDiff = Math.Abs(Challenger.Ping - Challenged.Ping);
        if (pingDiff > 5)
        {
            await Task.Delay(pingDiff);
        }
        lowPing.Client?.SendChatMessage(message);
        
        // Send countdown event to UI
        _plugin.SendBattleCountdown(this, count);
    }
    
    /// <summary>
    /// Get SP percentage for a participant (0-1)
    /// </summary>
    public float GetSPPercentage(EntryCar car)
    {
        if (car == Challenger)
            return ChallengerSP / ChallengerMaxSP;
        if (car == Challenged)
            return ChallengedSP / ChallengedMaxSP;
        return 0;
    }
}

public enum BattleEndReason
{
    None,
    SPDepleted,
    Separation,
    Timeout,
    TimeLimit,
    Disconnected,
    LineUpFailed,
    Reset,
    Error
}
