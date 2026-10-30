
namespace NexusIT.App.Models;

public class RdpConnection
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 3389;

    public string Group { get; set; } = "General";

    public string Username { get; set; } = string.Empty;

    public bool UseAdminSession { get; set; }

    public bool FullScreen { get; set; }

    public bool UseAllMonitors { get; set; }

    public int Width { get; set; } = 1920;

    public int Height { get; set; } = 1080;

    public string DisplayAddress =>
        Port == 3389 ? Host : $"{Host}:{Port}";
}
