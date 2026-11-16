using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace WallMod.Helpers.History;

public class DirectoryHistoryHelper
{
    private const int MaxRecentDirectories = 15;



    public List<string> LoadRecentJson()
    {
        if (!File.Exists(AppStorageHelper.appRecentDirectoriesFile))
            return new List<string>();

        string json = File.ReadAllText(AppStorageHelper.appRecentDirectoriesFile);
        return JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
    }

    public void AddToRecent(string direcPath)
    {
        var recent = LoadRecentJson();

        // move to front instead of duplicating
        recent.Remove(direcPath);
        recent.Insert(0, direcPath);

        if (recent.Count > MaxRecentDirectories)
            recent.RemoveRange(MaxRecentDirectories, recent.Count - MaxRecentDirectories);

        File.WriteAllText(AppStorageHelper.appRecentDirectoriesFile, JsonSerializer.Serialize(recent));
    }
}

// chose record since it should be immutable once declared
public record DirectoryHistoryRecord(int ItemCount, string DirecPath)
{
    // path split in two so the folder name can be coloured separately in the popup view
    public string FolderName => Path.GetFileName(Path.TrimEndingDirectorySeparator(DirecPath));
    public string ParentPath => Path.TrimEndingDirectorySeparator(DirecPath)[..^FolderName.Length];
}