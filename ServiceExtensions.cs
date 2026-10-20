namespace UpdateManager.ToolManagementCoreLibrary;
public static class ServiceExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection RegisterPostBuildServices()
        {
            services.AddSingleton<IToolsContext, FileToolsContext>()
                .AddSingleton<INugetPacker, NugetPacker>()
                .AddSingleton<PrivateToolDeploymentProcessor>()
                ; //for now, just one.
            return services;
        }
        public IServiceCollection RegisterToolDiscoveryServices()
        {
            services.AddTransient<IToolsContext, FileToolsContext>()
                .AddTransient<ToolDiscoveryService>();
            return services;
        }
        public IServiceCollection RegisterPublicToolUploadServices<TUploader>()
            where TUploader : class, INugetUploader
        {
            services.AddSingleton<IToolsContext, FileToolsContext>()
                .AddSingleton<IUploadedToolsStorage, FileUploadedToolsStorage>()
                .AddSingleton<INugetUploader, TUploader>()
                .AddSingleton<NuGetPublicToolUploadManager>();

            return services;
        }
    }
    
}