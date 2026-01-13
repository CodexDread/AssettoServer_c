using AssettoServer.Server.Plugin;
using Autofac;

namespace SPBattlePlugin;

/// <summary>
/// Autofac module for SP Battle Plugin dependency injection
/// </summary>
public class SPBattleModule : AssettoServerModule<SPBattleConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        // Main plugin service
        builder.RegisterType<SPBattlePlugin>()
            .AsSelf()
            .As<IAssettoServerAutostart>()
            .SingleInstance();
        
        // Per-car battle handlers (factory registered)
        builder.RegisterType<EntryCarBattle>().AsSelf();
        
        // Battle instances (factory registered)
        builder.RegisterType<Battle>().AsSelf();
        
        // Leaderboard service
        builder.RegisterType<LeaderboardService>()
            .AsSelf()
            .SingleInstance();
        
        // HTTP API controller
        builder.RegisterType<LeaderboardController>().AsSelf();
    }
}
