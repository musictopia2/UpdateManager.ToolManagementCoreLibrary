namespace UpdateManager.ToolManagementCoreLibrary;
public class PrivateToolDeploymentProcessor(IToolsContext context, INugetPacker packer, IToolDiscoveryHandler handler)
{
    // The main post-build method, specifically for private feed deployment
    public async Task ProcessPostBuildToPrivateFeedAsync(PostBuildArguments arguments)
    {
        try
        {
            var configuration = bb1.Configuration ?? throw new CustomBasicException("Configuration is not initialized.");
            string netVersion = configuration.GetNetVersion();
            string prefixName = bb1.Configuration!.GetPackagePrefixFromConfig();
            BasicList<NuGetToolModel> tools = await context.GetToolsAsync();
            NuGetToolModel? tool = tools.SingleOrDefault(x => x.PackageName == arguments.ProjectName);
            bool rets;
            if (tool is not null)
            {
                if (tool.IsExcluded)
                {
                    return; //this means can return because you are ignoring.
                }
                
                string directory = tool.GetRepositoryDirectory();
                rets = await GitBranchManager.IsOnDefaultBranchAsync(directory);
                if (rets == false)
                {
                    Console.WriteLine("You are not on default branch.  Therefore, will not update the packages");
                    return;
                }
                await UpdatePackageVersionAsync(tool);
            }
            else
            {
                tool = new NuGetToolModel
                {
                    PackageName = arguments.ProjectName,
                };
                handler.CustomizePackageModel(tool);
                tool.PackageName = arguments.ProjectName;
                tool.CsProjPath = Path.Combine(arguments.ProjectDirectory, arguments.ProjectFile);
                string directory = tool.GetRepositoryDirectory();
                rets = await GitBranchManager.IsOnDefaultBranchAsync(directory);
                if (rets == false)
                {
                    Console.WriteLine("You are not on default branch.  Therefore, will not create or update the packages.");
                    return;
                }
                tool.NugetPackagePath = Path.Combine(arguments.ProjectDirectory, "bin", "Release");
                CsProjEditor editor = new(tool.CsProjPath);
                EnumFeedType? feedType = editor.GetFeedType() ?? throw new CustomBasicException("No feed type found in the csproj file");
                tool.FeedType = feedType.Value;
                // Determine the target framework (NetStandard or NetRuntime)
                if (tool.FeedType == EnumFeedType.Public)
                {
                    tool.Version = $"{netVersion}.0.1";
                }
                else
                {
                    tool.Version = "1.0.1";
                }
                if (tool.FeedType == EnumFeedType.Public)
                {
                    if (handler.NeedsPrefix(tool, editor))
                    {
                        tool.PrefixForPackageName = prefixName;
                    }
                }
                await context.AddToolAsync(tool);
            }
            await CreateAndUploadNuGetPackageAsync(tool);
            if (tool.Development == false)
            {
                string developmentFeed = configuration.GetDevelopmentPackagePath();
                await LocalNuGetFeedManager.DeletePackageFolderAsync(developmentFeed, tool.PackageName); //if its not there, just ignore.
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during post-build processing to private feed: {ex.Message}");
            Environment.Exit(1); //so will error out.
        }
    }
    private async Task CreateAndUploadNuGetPackageAsync(NuGetToolModel tool)
    {
        bool created = await packer.CreateNugetPackageAsync(tool, true);
        if (!created)
        {
            throw new CustomBasicException("Failed to create nuget package.");
        }
        if (!Directory.Exists(tool.NugetPackagePath))
        {
            throw new CustomBasicException($"NuGet package path does not exist: {tool.NugetPackagePath}");
        }
        var files = ff1.FileList(tool.NugetPackagePath);
        files.RemoveAllOnly(x => !x.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase));
        if (files.Count != 1)
        {
            throw new CustomBasicException($"Error: Expected 1 .nupkg file, but found {files.Count}.");
        }
        string nugetFile = ff1.FullFile(files.Single());
        bool uploaded = await LocalNuGetFeedUploader.UploadPrivateNugetPackageAsync(GetFeedToUse(tool), tool.NugetPackagePath, nugetFile);
        if (!uploaded)
        {
            throw new CustomBasicException("Failed to publish nuget package to private feed");
        }
        await NuGetToolManager.InstallToolAsync(tool.GetPackageID(), tool.Version);
    }
    private async Task UpdatePackageVersionAsync(NuGetToolModel package)
    {
        string version = package.Version.IncrementMinorVersion();
        await NuGetToolManager.UninstallToolAsync(package.GetPackageID());
        await context.UpdateToolVersionAsync(package.PackageName, version);
    }
    private static string GetFeedToUse(NuGetToolModel package)
    {
        string stagingPath = bb1.Configuration!.GetStagingPackagePath();
        string developmentPath = bb1.Configuration!.GetDevelopmentPackagePath();
        string localPath = bb1.Configuration!.GetPrivatePackagePath();
        if (package.Development)
        {
            return developmentPath;
        }
        if (package.FeedType == EnumFeedType.Local)
        {
            return localPath;
        }
        return stagingPath;
    }
}