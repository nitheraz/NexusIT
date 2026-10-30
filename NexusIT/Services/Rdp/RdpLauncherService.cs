
using System.Diagnostics;
using System.IO;
using System.Text;
using NexusIT.App.Models;

namespace NexusIT.App.Services.Rdp;

public class RdpLauncherService
{
    public void Launch(RdpConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.Host))
            throw new InvalidOperationException(
                "A computer name or IP address is required.");

        if (connection.Port is < 1 or > 65535)
            throw new InvalidOperationException(
                "The RDP port must be between 1 and 65535.");

        if (connection.UseAllMonitors && connection.FullScreen == false)
        {
            // Multiple monitors can still be used without fullscreen.
            // No additional validation is required.
        }

        string directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "NexusIT",
            "RDP",
            "Temporary");

        Directory.CreateDirectory(directory);

        string profilePath = Path.Combine(
            directory,
            $"{Guid.NewGuid():N}.rdp");

        var settings = new List<string>
        {
            $"full address:s:{connection.Host}:{connection.Port}",
            $"screen mode id:i:{(connection.FullScreen ? 2 : 1)}",
            $"desktopwidth:i:{connection.Width}",
            $"desktopheight:i:{connection.Height}",
            $"use multimon:i:{(connection.UseAllMonitors ? 1 : 0)}",
            $"administrative session:i:{(connection.UseAdminSession ? 1 : 0)}",
            "prompt for credentials:i:1",
            "authentication level:i:2",
            "enablecredsspsupport:i:1",
            "redirectclipboard:i:1",
            "redirectprinters:i:0"
        };

        if (!string.IsNullOrWhiteSpace(connection.Username))
        {
            settings.Add($"username:s:{connection.Username}");
        }

        File.WriteAllText(
            profilePath,
            string.Join(Environment.NewLine, settings),
            new UTF8Encoding(false));

        var startInfo = new ProcessStartInfo
        {
            FileName = "mstsc.exe",
            UseShellExecute = true
        };

        startInfo.ArgumentList.Add(profilePath);

        Process.Start(startInfo);
    }
}
