using System.IO;

namespace NexusIT.App.Services;

public static class GamLocator
{
    private static readonly object SyncLock = new();

    private static string? _selectedGamPath;

    public static string? SelectedGamPath
    {
        get
        {
            lock (SyncLock)
            {
                return _selectedGamPath;
            }
        }
    }

    public static Task<string?> LocateGam()
    {
        string bundledPath = Path.Combine(
            AppContext.BaseDirectory,
            "GAM7",
            "gam.exe");

        string? result = File.Exists(bundledPath)
            ? bundledPath
            : null;

        lock (SyncLock)
        {
            _selectedGamPath = result;
        }

        return Task.FromResult<string?>(result);
    }
}