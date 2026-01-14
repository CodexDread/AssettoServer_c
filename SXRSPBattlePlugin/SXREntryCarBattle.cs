using System.Numerics;
using AssettoServer.Network.Tcp;
using AssettoServer.Server;
using AssettoServer.Shared.Network.Packets.Incoming;
using AssettoServer.Shared.Network.Packets.Outgoing;
using AssettoServer.Shared.Network.Packets.Shared;

namespace SXRSXRSPBattlePlugin;

/// <summary>
/// Per-car battle state and event handler.
/// Detects headlight flashes for TXR-style challenges.
/// </summary>
public class SXREntryCarBattle
{
    private readonly SessionManager _sessionManager;
    private readonly EntryCarManager _entryCarManager;
    private readonly SXRSPBattlePlugin _plugin;
    private readonly SXRSPBattleConfiguration _config;
    private readonly EntryCar _entryCar;
    private readonly Battle.Factory _battleFactory;
    
    /// <summary>
    /// Current headlight flash count (resets after timeout)
    /// </summary>
    public int LightFlashCount { get; private set; }
    
    /// <summary>
    /// Currently active battle (null if not in battle)
    /// </summary>
    internal Battle? CurrentBattle { get; set; }
    
    /// <summary>
    /// Driver Level for SP bonus calculation
    /// Will be integrated with external DL system later
    /// </summary>
    public int DriverLevel { get; set; } = 0;
    
    /// <summary>
    /// Last time a light was flashed
    /// </summary>
    private long _lastLightFlashTime;
    
    /// <summary>
    /// Last time this car initiated a challenge
    /// </summary>
    private long _lastChallengeTime;
    
    /// <summary>
    /// Last collision time (for debouncing)
    /// </summary>
    private long _lastCollisionTime;
    
    public SXREntryCarBattle(
        EntryCar entryCar, 
        SessionManager sessionManager, 
        EntryCarManager entryCarManager, 
        SXRSPBattlePlugin plugin,
        SXRSPBattleConfiguration config,
        Battle.Factory battleFactory)
    {
        _entryCar = entryCar;
        _sessionManager = sessionManager;
        _entryCarManager = entryCarManager;
        _plugin = plugin;
        _config = config;
        _battleFactory = battleFactory;
        
        // Subscribe to car events
        _entryCar.PositionUpdateReceived += OnPositionUpdateReceived;
        _entryCar.ResetInvoked += OnResetInvoked;
        _entryCar.CollisionReceived += OnCollisionReceived;
    }
    
    private void OnResetInvoked(EntryCar sender, EventArgs args)
    {
        // Cancel current battle if player resets
        if (CurrentBattle != null && CurrentBattle.HasStarted)
        {
            // The battle loop will detect the reset via position change
        }
        CurrentBattle = null;
    }
    
    private void OnCollisionReceived(EntryCar sender, CollisionEventArgs args)
    {
        if (CurrentBattle == null || !CurrentBattle.HasStarted) return;
        
        long currentTime = _sessionManager.ServerTimeMilliseconds;
        
        // Debounce collisions (500ms)
        if (currentTime - _lastCollisionTime < 500) return;
        _lastCollisionTime = currentTime;
        
        // Check if collision is with battle opponent or wall
        if (args.TargetCar != null)
        {
            // Car collision
            var opponent = CurrentBattle.Challenger == _entryCar ? CurrentBattle.Challenged : CurrentBattle.Challenger;
            if (args.TargetCar == opponent)
            {
                // Collision with opponent - both take penalty
                CurrentBattle.ApplyCollisionPenalty(_entryCar, false);
                CurrentBattle.ApplyCollisionPenalty(opponent, false);
            }
        }
        else
        {
            // Wall collision
            CurrentBattle.ApplyCollisionPenalty(_entryCar, true);
        }
    }
    
    private void OnPositionUpdateReceived(EntryCar sender, in PositionUpdateIn positionUpdate)
    {
        long currentTime = _sessionManager.ServerTimeMilliseconds;
        
        // Detect light flash (headlights toggled on OR high beams toggled)
        bool lightsToggled = 
            ((_entryCar.Status.StatusFlag & CarStatusFlags.LightsOn) == 0 && 
             (positionUpdate.StatusFlag & CarStatusFlags.LightsOn) != 0) ||
            ((_entryCar.Status.StatusFlag & CarStatusFlags.HighBeamsOff) == 0 && 
             (positionUpdate.StatusFlag & CarStatusFlags.HighBeamsOff) != 0);
        
        if (lightsToggled)
        {
            _lastLightFlashTime = currentTime;
            LightFlashCount++;
        }
        
        // Accept challenge via hazard lights
        if (CurrentBattle != null && 
            !CurrentBattle.HasStarted &&
            CurrentBattle.Challenged == sender &&
            (_entryCar.Status.StatusFlag & CarStatusFlags.HazardsOn) == 0 &&
            (positionUpdate.StatusFlag & CarStatusFlags.HazardsOn) != 0 &&
            !CurrentBattle.LineUpRequired)
        {
            _ = CurrentBattle.StartAsync();
        }
        
        // Reset flash count after 3 second timeout
        if (currentTime - _lastLightFlashTime > 3000 && LightFlashCount > 0)
        {
            LightFlashCount = 0;
        }
        
        // Triple flash = challenge nearby car
        if (LightFlashCount >= 3)
        {
            LightFlashCount = 0;
            
            // Enforce cooldown
            if (currentTime - _lastChallengeTime > _config.ChallengeCooldownSeconds * 1000)
            {
                Task.Run(ChallengeNearbyCar);
                _lastChallengeTime = currentTime;
            }
        }
    }
    
    /// <summary>
    /// Challenge a specific car
    /// </summary>
    internal void ChallengeCar(EntryCar target, bool lineUpRequired = true)
    {
        void Reply(string message) => _entryCar.Client?.SendChatMessage(message);
        
        // Validate current state
        if (CurrentBattle != null)
        {
            if (CurrentBattle.HasStarted)
                Reply("You are currently in a battle.");
            else
                Reply("You have a pending challenge.");
            return;
        }
        
        if (target == _entryCar)
        {
            Reply("You cannot challenge yourself.");
            return;
        }
        
        // Check target's state
        var targetBattle = _plugin.GetBattle(target);
        if (targetBattle.CurrentBattle != null)
        {
            if (targetBattle.CurrentBattle.HasStarted)
                Reply("This driver is currently in a battle.");
            else
                Reply("This driver has a pending challenge.");
            return;
        }
        
        // Check distance
        float distance = Vector3.Distance(_entryCar.Status.Position, target.Status.Position);
        if (distance > _config.ChallengeMaxDistance)
        {
            Reply($"Target is too far away ({distance:F0}m). Get within {_config.ChallengeMaxDistance}m.");
            return;
        }
        
        // Create the battle
        var battle = _battleFactory(
            _entryCar, 
            target, 
            DriverLevel, 
            targetBattle.DriverLevel, 
            lineUpRequired);
        
        CurrentBattle = battle;
        targetBattle.CurrentBattle = battle;
        
        // Notify participants
        float challengerMaxSP = battle.ChallengerMaxSP;
        float challengedMaxSP = battle.ChallengedMaxSP;
        
        _entryCar.Client?.SendChatMessage(
            $"You challenged {target.Client?.Name} to an SP Battle! (Your SP: {challengerMaxSP:F0})");
        
        if (lineUpRequired)
        {
            target.Client?.SendChatMessage(
                $"{_entryCar.Client?.Name} challenges you to an SP Battle! (Your SP: {challengedMaxSP:F0})\n" +
                $"Type /accept within {_config.ChallengeTimeoutSeconds}s to accept.");
        }
        else
        {
            target.Client?.SendChatMessage(
                $"{_entryCar.Client?.Name} challenges you! (Your SP: {challengedMaxSP:F0})\n" +
                $"Flash hazards or type /accept within {_config.ChallengeTimeoutSeconds}s.");
        }
        
        // Send challenge event to UI
        _plugin.SendChallengeEvent(_entryCar, target);
        
        // Timeout task
        _ = Task.Delay((int)(_config.ChallengeTimeoutSeconds * 1000)).ContinueWith(_ =>
        {
            if (battle == CurrentBattle && !battle.HasStarted)
            {
                CurrentBattle = null;
                targetBattle.CurrentBattle = null;
                
                _entryCar.Client?.SendChatMessage("Challenge timed out.");
                target.Client?.SendChatMessage("Challenge timed out.");
            }
        });
    }
    
    /// <summary>
    /// Find and challenge the nearest eligible car
    /// </summary>
    private void ChallengeNearbyCar()
    {
        EntryCar? bestMatch = null;
        float bestDistanceSq = _config.ChallengeMaxDistance * _config.ChallengeMaxDistance;
        
        foreach (var car in _entryCarManager.EntryCars)
        {
            if (car.Client == null || car == _entryCar) continue;
            
            // Check if car is in front of us (angle check)
            float angle = CalculateRelativeAngle(car);
            
            // Target should be roughly ahead of us (within 60 degrees of forward)
            // Or beside us (90 degrees)
            if (angle > 60 && angle < 300 && !(angle > 250 || angle < 110))
                continue;
            
            float distanceSq = Vector3.DistanceSquared(car.Status.Position, _entryCar.Status.Position);
            if (distanceSq < bestDistanceSq)
            {
                bestDistanceSq = distanceSq;
                bestMatch = car;
            }
        }
        
        if (bestMatch != null)
        {
            ChallengeCar(bestMatch, lineUpRequired: false);
        }
    }
    
    private float CalculateRelativeAngle(EntryCar target)
    {
        float angle = (float)(Math.Atan2(
            target.Status.Position.X - _entryCar.Status.Position.X,
            target.Status.Position.Z - _entryCar.Status.Position.Z) * 180 / Math.PI);
        
        if (angle < 0) angle += 360;
        
        float rotation = _entryCar.Status.GetRotationAngle();
        angle = (angle + rotation) % 360;
        
        return angle;
    }
    
    /// <summary>
    /// Accept a pending challenge
    /// </summary>
    internal async Task AcceptChallengeAsync()
    {
        if (CurrentBattle == null)
        {
            _entryCar.Client?.SendChatMessage("You don't have a pending challenge.");
            return;
        }
        
        if (CurrentBattle.HasStarted)
        {
            _entryCar.Client?.SendChatMessage("Battle has already started.");
            return;
        }
        
        if (CurrentBattle.Challenger == _entryCar)
        {
            _entryCar.Client?.SendChatMessage("You can't accept your own challenge.");
            return;
        }
        
        await CurrentBattle.StartAsync();
    }
    
    /// <summary>
    /// Set driver level (called by external DL system)
    /// </summary>
    public void SetDriverLevel(int level)
    {
        DriverLevel = Math.Clamp(level, 0, _config.MaxDriverLevel);
    }
}
