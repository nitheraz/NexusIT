
using System.Diagnostics;
using NexusIT.App.Models;

namespace NexusIT.App.Services.Rdp;

public class RdpLauncherService
{
    public void Launch(RdpConnection connection)
    {
        if (string.IsNullOrWhiteSpace(connection.Host))
        {
            throw new InvalidOperationException(
                "A computer name or IP address is required.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "mstsc.exe",
            UseShellExecute = true
        };

        startInfo.ArgumentList.Add($"/v:{connection.Host.Trim()}");

        if (connection.UseAdminSession)
            startInfo.ArgumentList.Add("/admin");

        if (connection.FullScreen)
            startInfo.ArgumentList.Add("/f");

        if (connection.UseAllMonitors)
            startInfo.ArgumentList.Add("/multimon");

        Process.Start(startInfo);
    }
}
