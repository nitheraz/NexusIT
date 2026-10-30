
using System.IO;
using System.Text.Json;
using NexusIT.App.Models;

namespace NexusIT.App.Services.Rdp;

public class RdpStorageService
{
    private readonly string _directory;
    private readonly string _filePath;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public RdpStorageService()
    {
        _directory = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "NexusIT",
            "RDP");

        _filePath = Path.Combine(_directory, "connections.json");
    }

    public async Task<List<RdpConnection>> LoadAsync()
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            await using FileStream stream = File.OpenRead(_filePath);

            return await JsonSerializer.DeserializeAsync<List<RdpConnection>>(
                stream, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            throw new InvalidDataException(
                $"The RDP connection file contains invalid JSON: {_filePath}");
        }
    }

    public async Task SaveAsync(List<RdpConnection> connections)
    {
        Directory.CreateDirectory(_directory);

        string temporaryPath = _filePath + ".tmp";

        await using (FileStream stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(
                stream, connections, JsonOptions);
        }

        File.Move(temporaryPath, _filePath, overwrite: true);
    }

    public string GetStoragePath() => _filePath;
}
