using AssettoServer.Server;
using AssettoServer.Server.Ai;
using AssettoServer.Server.Configuration;
using AssettoServer.Server.Plugin;
using AssettoServer.Shared.Services;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace RealisticTrafficPlugin;

/// <summary>
/// AssettoServer plugin for realistic traffic simulation.
/// Enhances the existing AI traffic with IDM car-following and MOBIL lane changes.
/// </summary>
public class RealisticTrafficPlugin : CriticalBackgroundService, IAssettoServerAutostart
{
    private readonly TrafficManager _trafficManager;
    private readonly TrafficConfiguration _config;
    private readonly EntryCarManager _entryCarManager;
    private readonly ACServerConfiguration _serverConfig;
    private readonly ILogger _logger;
    
    private readonly float _tickInterval;
    private DateTime _lastUpdate = DateTime.UtcNow;
    
    public RealisticTrafficPlugin(
        TrafficConfiguration config,
        EntryCarManager entryCarManager,
        ACServerConfiguration serverConfig,
        IHostApplicationLifetime lifetime) : base(lifetime)
    {
        _config = config;
        _entryCarManager = entryCarManager;
        _serverConfig = serverConfig;
        _logger = Log.ForContext<RealisticTrafficPlugin>();
        
        _trafficManager = new TrafficManager(config);
        _tickInterval = 1.0f / config.UpdateTickRate;
        
        _logger.Information("RealisticTrafficPlugin initialized with {TickRate}Hz tick rate", config.UpdateTickRate);
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.Information("RealisticTrafficPlugin starting...");
        
        // Wait for server to be ready
        await Task.Delay(5000, stoppingToken);
        
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var elapsed = (float)(now - _lastUpdate).TotalSeconds;
                
                if (elapsed >= _tickInterval)
                {
                    // Update player positions from entry cars
                    UpdatePlayerPositions();
                    
                    // Update traffic simulation
                    _trafficManager.Update(elapsed);
                    
                    // Sync traffic states to AI slots
                    SyncTrafficToAiSlots();
                    
                    _lastUpdate = now;
                    
                    if (_config.DebugLogging)
                    {
                        _logger.Debug("Traffic update: {VehicleCount} vehicles", _trafficManager.VehicleCount);
                    }
                }
                
                // Sleep until next tick
                var sleepTime = (int)((_tickInterval - elapsed) * 1000);
                if (sleepTime > 0)
                {
                    await Task.Delay(sleepTime, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error in traffic update loop");
                await Task.Delay(1000, stoppingToken);
            }
        }
        
        _logger.Information("RealisticTrafficPlugin stopped");
    }
    
    private void UpdatePlayerPositions()
    {
        foreach (var car in _entryCarManager.EntryCars)
        {
            if (car.Client == null || car.AiControlled) continue;
            
            // Get player's spline position
            // Note: You'll need to adapt this to your spline system
            var splinePos = car.Status.SplinePosition;
            var worldPos = new System.Numerics.Vector3(
                car.Status.Position.X,
                car.Status.Position.Y,
                car.Status.Position.Z);
            var forward = new System.Numerics.Vector3(
                car.Status.Velocity.X,
                car.Status.Velocity.Y,
                car.Status.Velocity.Z);
            
            _trafficManager.UpdatePlayerPosition(
                car.SessionId, 
                splinePos, 
                worldPos, 
                forward);
        }
        
        // Remove disconnected players
        var activePlayers = _entryCarManager.EntryCars
            .Where(c => c.Client != null && !c.AiControlled)
            .Select(c => c.SessionId)
            .ToHashSet();
        
        // Note: You may need to track and remove players that disconnect
    }
    
    private void SyncTrafficToAiSlots()
    {
        var states = _trafficManager.GetVehicleStates().ToList();
        var aiCars = _entryCarManager.EntryCars
            .Where(c => c.AiControlled)
            .ToList();
        
        // Match traffic states to AI slots
        for (int i = 0; i < Math.Min(states.Count, aiCars.Count); i++)
        {
            var state = states[i];
            var aiCar = aiCars[i];
            
            // Convert spline position + lane to world position
            // This requires integration with your spline system
            // Example pseudocode:
            // var worldPos = SplineToWorld(state.SplinePosition, state.Lane, state.LateralOffset);
            // aiCar.SetPosition(worldPos);
            // aiCar.SetSpeed(state.Speed);
            
            if (_config.LogSpawnEvents && _config.DebugLogging)
            {
                _logger.Verbose("AI {Slot}: Spline={Pos:F0} Lane={Lane} Speed={Speed:F1}",
                    i, state.SplinePosition, state.Lane, state.Speed * 3.6f);
            }
        }
    }
}

/// <summary>
/// Module for dependency injection registration
/// </summary>
public class RealisticTrafficModule : AssettoServerModule<TrafficConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<RealisticTrafficPlugin>().AsSelf().As<IHostedService>().SingleInstance();
    }
}
