
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NexusIT.App.Models;

namespace NexusIT.App.Services;

public sealed class MecmActivityLogService
{
    private readonly string _filePath;
    private readonly object _sync = new();

    public MecmActivityLogService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "NexusIT",
            "Logs",
            "mecm-activity.json");
    }

    public void Record(
        string action,
        string target,
        string result,
        string? details = null,
        string? account = null)
    {
        lock (_sync)
        {
            var entries = ReadInternal();

            entries.Add(new MecmActivityEntry
            {
                Timestamp = DateTimeOffset.Now,
                Action = action,
                Target = target,
                Result = result,
                Details = details,
                Account = account
            });

            string? directory =
                Path.GetDirectoryName(_filePath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                _filePath,
                JsonSerializer.Serialize(
                    entries,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    }));
        }
    }

    public List<MecmActivityEntry> GetRecent(int count = 500)
    {
        if (count < 1)
            return [];

        lock (_sync)
        {
            return ReadInternal()
                .OrderByDescending(x => x.Timestamp)
                .Take(count)
                .ToList();
        }
    }

    private List<MecmActivityEntry> ReadInternal()
    {
        if (!File.Exists(_filePath))
            return [];

        try
        {
            return JsonSerializer.Deserialize<
                List<MecmActivityEntry>>(
                    File.ReadAllText(_filePath)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}
