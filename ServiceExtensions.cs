namespace UpdateManager.ToolManagementCoreLibrary;
public static class ServiceExtensions
{
    public static IServiceCollection RegisterPostBuildServices(this IServiceCollection services)
    {
        services.AddSingleton<IToolsContext, FileToolsContext>()
            .AddSingleton<INugetPacker, NugetPacker>()
            .AddSingleton<PrivateToolDeploymentProcessor>()
            ; //for now, just one.
        return services;
    }
    public static IServiceCollection RegisterPackageDiscoveryServices(this IServiceCollection services)
    {
        services.AddTransient<IToolsContext, FileToolsContext>()
            .AddTransient<ToolDiscoveryService>();
        return services;
    }
    public static IServiceCollection RegisterPublicUploadServices(this IServiceCollection services)
    {
        services.AddSingleton<IToolsContext, FileToolsContext>()
            .AddSingleton<IUploadedToolsStorage, FileUploadedToolsStorage>()
            .AddSingleton<INugetUploader, PublicNugetUploader>()
            .AddSingleton<NuGetPublicToolUploadManager>();
        return services;
    }
}