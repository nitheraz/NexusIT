
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NexusIT.App.Services;

public sealed class WebToolService
{
    private readonly string _configPath;
    private readonly string _iconFolder;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public WebToolService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "NexusIT");

        Directory.CreateDirectory(folder);

        _configPath = Path.Combine(folder, "web-tools.json");

        _iconFolder = Path.Combine(folder, "WebToolIcons");

        Directory.CreateDirectory(_iconFolder);
    }

    public List<WebTool> GetTools()
    {
        if (!File.Exists(_configPath))
            return CreateDefaultTools();

        try
        {
            string json = File.ReadAllText(_configPath);

            List<WebTool>? tools =
                JsonSerializer.Deserialize<List<WebTool>>(
                    json,
                    JsonOptions);

            return tools ?? CreateDefaultTools();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "The Web Tools configuration file is invalid.",
                ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                "Unable to read the Web Tools configuration file.",
                ex);
        }
    }

    public void SaveTools(List<WebTool> tools)
    {
        ArgumentNullException.ThrowIfNull(tools);

        try
        {
            string json = JsonSerializer.Serialize(
                tools,
                JsonOptions);

            File.WriteAllText(_configPath, json);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                "Unable to save the Web Tools configuration.",
                ex);
        }
    }

    public string SaveCustomIcon(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
            throw new ArgumentException(
                "An image file must be selected.",
                nameof(sourcePath));

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException(
                "The selected image could not be found.",
                sourcePath);

        string extension = Path.GetExtension(sourcePath).ToLowerInvariant();

        if (extension is not ".png" and not ".jpg" and not ".jpeg")
        {
            throw new InvalidOperationException(
                "Only PNG, JPG and JPEG images are supported.");
        }

        string fileName = Guid.NewGuid().ToString("N") + extension;
        string destination = Path.Combine(_iconFolder, fileName);

        File.Copy(sourcePath, destination, true);

        return fileName;
    }

    public string? GetCustomIconPath(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        // Only allow a filename, not an arbitrary path.
        if (!string.Equals(
                Path.GetFileName(fileName),
                fileName,
                StringComparison.Ordinal))
        {
            return null;
        }

        string path = Path.Combine(_iconFolder, fileName);

        return File.Exists(path) ? path : null;
    }

    public void DeleteCustomIcon(string? fileName)
    {
        string? path = GetCustomIconPath(fileName);

        if (path == null)
            return;

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // An unused icon file can be cleaned up later.
        }
        catch (UnauthorizedAccessException)
        {
            // Do not interrupt normal tool management.
        }
    }

    private static List<WebTool> CreateDefaultTools()
    {
        return new List<WebTool>
        {
            new()
            {
                Name = "Google Admin",
                Category = "Google",
                Description = "Google Workspace administration",
                Icon = "G"
            },
            new()
            {
                Name = "Aruba Central",
                Category = "Infrastructure",
                Description = "Wireless and network management",
                Icon = "NW"
            },
            new()
            {
                Name = "ServiceNow",
                Category = "IT Service Management",
                Description = "IT service management and tickets",
                Icon = "SN"
            },
            new()
            {
                Name = "Vivi",
                Category = "Classroom Technology",
                Description = "Classroom display and collaboration management",
                Icon = "V"
            },
            new()
            {
                Name = "Jamf School",
                Category = "Device Management",
                Description = "Apple device management",
                Icon = "J"
            },
            new()
            {
                Name = "Airlock",
                Category = "Security",
                Description = "Application control and endpoint security",
                Icon = "A"
            },
            new()
            {
                Name = "SolarWinds Platform",
                Category = "Infrastructure",
                Description = "Infrastructure monitoring",
                Icon = "SW"
            },
            new()
            {
                Name = "Zscaler ZIA",
                Category = "Security",
                Description = "Internet access and security management",
                Icon = "Z"
            },
            new()
            {
                Name = "Abnormal",
                Category = "Security",
                Description = "Email security management",
                Icon = "AB"
            },
            new()
            {
                Name = "Automox",
                Category = "Device Management",
                Description = "Endpoint patching and management",
                Icon = "AX"
            },
            new()
            {
                Name = "Keeper",
                Category = "Security",
                Description = "Password and secrets management",
                Icon = "K"
            }
        };
    }
}

public sealed class WebTool
{
    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string ChromeProfileDirectory { get; set; } = string.Empty;

    public string Icon { get; set; } = "🌐";

    public string CustomIconFileName { get; set; } = string.Empty;

    public bool IsFavorite { get; set; }

    public DateTime? LastLaunchedUtc { get; set; }
}
