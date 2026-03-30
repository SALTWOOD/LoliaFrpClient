using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace LoliaFrpClient.Services;

public record ClientUpdateResult(
    bool HasUpdate, 
    string CurrentVersion, 
    string LatestVersion, 
    string ReleaseUrl, 
    string ReleaseNotes, 
    DateTime PublishedAt, 
    string? DownloadUrl
);

public static class ClientUpdateService
{
    private const string Owner = "SALTWOOD";
    private const string Repo = "LoliaFrpClient";

    public static string GetCurrentVersion()
    {
        if (Utils.IsPackaged())
        {
            var v = Package.Current.Id.Version;
            return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}";
        }
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
    }

    public static async Task<ClientUpdateResult> CheckForUpdateAsync()
    {
        var currentStr = GetCurrentVersion();
        
        try
        {
            var release = await GitHubReleaseService.GetLatestReleaseAsync(Owner, Repo);
            if (release == null) return CreateEmpty(currentStr);

            // Version.Parse handles "1.0.0", but needs to strip 'v' from GitHub tags
            var latestStr = release.TagName.TrimStart('v', 'V');
            var currentVersion = Version.Parse(currentStr.TrimStart('v', 'V'));
            var latestVersion = Version.Parse(latestStr);

            return new ClientUpdateResult(
                HasUpdate: latestVersion > currentVersion,
                CurrentVersion: $"v{currentStr}",
                LatestVersion: release.TagName,
                ReleaseUrl: "https://github.com/SALTWOOD/LoliaFrpClient/releases/latest", // Or release.HtmlUrl
                ReleaseNotes: release.Body,
                PublishedAt: DateTime.Now, // Use release.PublishedAt if added to record
                DownloadUrl: GitHubReleaseService.GetDownloadUrl(release, AssetType.Client)
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Update check failed: {ex.Message}");
            return CreateEmpty(currentStr);
        }
    }

    private static ClientUpdateResult CreateEmpty(string version) => 
        new(false, $"v{version}", string.Empty, string.Empty, string.Empty, DateTime.MinValue, null);
}