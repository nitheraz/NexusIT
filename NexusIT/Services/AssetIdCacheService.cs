using System.IO;
using System.Text.Json;

namespace NexusIT.App.Services;

public static class AssetIdCacheService
{
    private static readonly string CacheDirectory =
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "NexusIT",
            "GAM7");

    private static readonly string CacheFilePath =
        Path.Combine(CacheDirectory, "asset-id-cache.json");

    private static readonly object SyncLock = new();

    public static Dictionary<string, string>? TryLoad(
        TimeSpan maxAge)
    {
        lock (SyncLock)
        {
            try
            {
                if (!File.Exists(CacheFilePath))
                    return null;

                var fileInfo = new FileInfo(CacheFilePath);

                if (DateTime.UtcNow - fileInfo.LastWriteTimeUtc > maxAge)
                    return null;

                string json = File.ReadAllText(CacheFilePath);

                var cachedMap =
                    JsonSerializer.Deserialize<Dictionary<string, string>>(json);

                if (cachedMap is null)
                    return null;

                return new Dictionary<string, string>(
                    cachedMap,
                    StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                // A missing or invalid cache should not prevent GAM use.
                return null;
            }
        }
    }

    public static void Save(
        Dictionary<string, string> map)
    {
        lock (SyncLock)
        {
            try
            {
                Directory.CreateDirectory(CacheDirectory);

                string json = JsonSerializer.Serialize(
                    map,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(CacheFilePath, json);
            }
            catch
            {
                // Cache failures should not interrupt device lookups.
            }
        }
    }

    public static void Clear()
    {
        lock (SyncLock)
        {
            try
            {
                if (File.Exists(CacheFilePath))
                    File.Delete(CacheFilePath);
            }
            catch
            {
                // Clearing the cache is best-effort.
            }
        }
    }
}