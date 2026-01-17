using AssettoServer.Server.Plugin;
using Autofac;

namespace SXRAdminToolsPlugin;

/// <summary>
/// Autofac module for Admin Tools Plugin dependency injection
/// </summary>
public class SXRAdminToolsModule : AssettoServerModule<SXRAdminToolsConfiguration>
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
        
        // Command module
        builder.RegisterType<SXRAdminCommandModule>()
            .AsSelf();
    }
}
