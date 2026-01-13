using AssettoServer.Server.Plugin;
using Autofac;

namespace AdminToolsPlugin;

/// <summary>
/// Autofac module for Admin Tools Plugin dependency injection
/// </summary>
public class AdminToolsModule : AssettoServerModule<AdminToolsConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        // Main plugin service
        builder.RegisterType<AdminToolsPlugin>()
            .AsSelf()
            .As<IAssettoServerAutostart>()
            .SingleInstance();
        
        // Ban service
        builder.RegisterType<BanService>()
            .AsSelf()
            .SingleInstance();
        
        // Audit service
        builder.RegisterType<AuditService>()
            .AsSelf()
            .SingleInstance();
        
        // HTTP API controller
        builder.RegisterType<AdminToolsController>()
            .AsSelf();
    }
}
