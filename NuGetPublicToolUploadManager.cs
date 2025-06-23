namespace UpdateManager.ToolManagementCoreLibrary;
public class NuGetPublicToolUploadManager(IToolsContext toolsContext,
    IUploadedToolsStorage uploadContext,
    INugetUploader uploader
    )
{
    // The method that does all the work of checking, uploading, and tracking
    public async Task UploadToolsAsync(CancellationToken cancellationToken = default)
    {
        string feedUrl = bb1.Configuration!.GetStagingPackagePath();
        BasicList<UploadToolModel> list = await GetUploadedToolsAsync(feedUrl, cancellationToken);
        list = list.ToBasicList(); //try to make a copy here too.
        await UploadToolsAsync(list, cancellationToken);
        await CheckToolsAsync(list, feedUrl);
    }
    public async Task<bool> HasItemsToProcessAsync()
    {
        var list = await uploadContext.GetAllUploadedToolsAsync();
        return list.Count > 0;
    }
    private async Task UploadToolsAsync(BasicList<UploadToolModel> tools, CancellationToken cancellationToken)
    {
        await tools.ForConditionalItemsAsync(x => x.Uploaded == false, async item =>
        {
            bool rets;
            rets = await uploader.UploadNugetPackageAsync(item.NugetFilePath, cancellationToken);
            if (rets)
            {
                item.Uploaded = true;
                await uploadContext.UpdateUploadedToolAsync(item); //update this one since it was not uploaded
                Console.WriteLine("Your package was pushed");
            }
        });
    }
    private async Task CheckToolsAsync(BasicList<UploadToolModel> tools, string feedUrl)
    {
        await tools.ForConditionalItemsAsync(x => x.Uploaded, async item =>
        {
            Console.WriteLine($"Checking {item.PackageId} to see if its on public nuget");
            bool rets;
            rets = await NuGetPackageChecker.IsPublicPackageAvailableAsync(item.PackageId, item.Version);
            if (rets)
            {
                Console.WriteLine($"Package {item.PackageId} is finally on nuget.  Can now delete");
                await uploadContext.DeleteUploadedToolAsync(item.PackageId);
                await LocalNuGetFeedManager.DeletePackageFolderAsync(feedUrl, item.PackageId);
            }
        });
    }
    private async Task<BasicList<UploadToolModel>> GetUploadedToolsAsync(string feedUrl, CancellationToken cancellationToken)
    {
        var stagingTools = await LocalNuGetFeedManager.GetAllPackagesAsync(feedUrl, cancellationToken);
        var allPackages = await toolsContext.GetToolsAsync();
        var uploadedPackages = await uploadContext.GetAllUploadedToolsAsync();
        BasicList<UploadToolModel> output = [];
        //the moment of truth has to be the staging packages.
        foreach (var name in stagingTools)
        {
            //this means needs to add package.
            var ourPackage = allPackages.SingleOrDefault(x => x.GetPackageID().Equals(name, StringComparison.CurrentCultureIgnoreCase));
            var uploadedPackage = uploadedPackages.SingleOrDefault(x => x.PackageId.Equals(name, StringComparison.CurrentCultureIgnoreCase));
            //i am guessing if you are now temporarily ignoring it, still okay to process because it was the past.
            //same thing for development.
            if (uploadedPackage is null && ourPackage is not null)
            {
                string packageId = ourPackage.GetPackageID();
                uploadedPackage = new()
                {
                    PackageId = packageId,
                    Version = ourPackage.Version,
                    NugetFilePath = LocalNuGetFeedManager.GetNugetFile(feedUrl, packageId, ourPackage.Version)
                };
                output.Add(uploadedPackage);
            }
            else if (uploadedPackage is not null && ourPackage is not null)
            {
                if (uploadedPackage.Version != ourPackage.Version)
                {
                    //this means needs to use the new version regardless of status
                    uploadedPackage.Version = ourPackage.Version;
                    uploadedPackage.NugetFilePath = LocalNuGetFeedManager.GetNugetFile(feedUrl, uploadedPackage.PackageId, ourPackage.Version);
                    uploadedPackage.Uploaded = false; //we have new version now.
                }
                output.Add(uploadedPackage);
            }
        }
        await uploadContext.SaveUpdatedUploadedListAsync(output); //i think.
        return output;
    }
}