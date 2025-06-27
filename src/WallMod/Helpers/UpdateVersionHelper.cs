using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using WallMod.State;

namespace WallMod.Helpers;

public class UpdateVersionHelper
{
    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    private string githubApi = "https://api.github.com/repos/DefrimBinakaj/WallMod/releases/latest";


    public async Task<(bool, Version, string?)> GetGithubVersionAndInstallLink()
    {
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("WallMod/1");

        string json = await httpClient.GetStringAsync(githubApi).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);

        string tag = (doc.RootElement.GetProperty("tag_name").GetString() ?? "").TrimStart('v', 'V');
        var latestVersionNumber = Version.Parse(tag);

        string versionSuffix = OperatingSystem.IsWindows() ? "Windows_x64.exe"
                     : OperatingSystem.IsLinux() ? "Linux_x64"
                     : "";

        string? downloadURL = doc.RootElement.GetProperty("assets").EnumerateArray().FirstOrDefault(a => (a.GetProperty("name").GetString() ?? "")
                      .Contains(versionSuffix, StringComparison.OrdinalIgnoreCase)).GetProperty("browser_download_url").GetString();

        string currentVersion = uniVM.AppNameVersion;
        bool isNewestVersion;
        // compare actual two version numbers to see if curr version is latest ( [1..] used to get rid of 'v' )
        if (latestVersionNumber.ToString() == currentVersion[1..])
        {
            isNewestVersion = true;
        }
        else
        {
            isNewestVersion = false;
        }

        Debug.WriteLine(isNewestVersion + " - " + latestVersionNumber + " - " + downloadURL);
        return (isNewestVersion, latestVersionNumber, downloadURL);
    }



    public async Task ExecuteAppUpdate(Version appVersion, string appURL)
    {
        if (OperatingSystem.IsWindows())
        {
            // CODE TO UPDATE WINDOWS HERE
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/DefrimBinakaj/WallMod/releases/latest",
                UseShellExecute = true
            });

        }
        else if (OperatingSystem.IsLinux())
        {
            // CODE TO UPDATE LINUX HERE
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/DefrimBinakaj/WallMod/releases/latest",
                UseShellExecute = true
            });

        }
    }


}
