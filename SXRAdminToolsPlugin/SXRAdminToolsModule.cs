using AssettoServer.Server.Plugin;
using Autofac;

namespace SXRSXRAdminToolsPlugin;

/// <summary>
/// Autofac module for Admin Tools Plugin dependency injection
/// </summary>
public class SXRSXRAdminToolsModule : AssettoServerModule<SXRAdminToolsConfiguration>
{
    protected override void Load(ContainerBuilder builder)
    {
        // Main plugin service
        builder.RegisterType<SXRAdminToolsPlugin>()
            .AsSelf()
            .As<IAssettoServerAutostart>()
            .SingleInstance();
        
        // Ban service
        builder.RegisterType<SXRBanService>()
            .AsSelf()
            .SingleInstance();
        
        // Audit service
        builder.RegisterType<SXRAuditService>()
            .AsSelf()
            .SingleInstance();
        
        // HTTP API controller
        builder.RegisterType<SXRAdminToolsController>()
            .AsSelf();
    }
}
