
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NexusIT.App.Services;

public class ModuleService
{
    private readonly string _configPath;

    private readonly Dictionary<string, bool> _modules = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ["Chromebooks"] = false,
        ["Google Groups"] = false,
        ["Active Directory"] = true,
        ["MECM / SCCM"] = true,
        ["RDP"] = true,
        ["Admin Tools"] = true,
        ["Network & System Tools"] = true,
        ["Web Tools"] = true,
        ["Switch Diagnostics"] = true
    };

    public ModuleService()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexusIT");

        Directory.CreateDirectory(folder);

        _configPath = Path.Combine(folder, "modules.json");

        Load();
    }

    public IReadOnlyDictionary<string, bool> GetModules()
    {
        return _modules;
    }

    public bool IsEnabled(string moduleName)
    {
        return !_modules.TryGetValue(moduleName, out bool enabled) || enabled;
    }

    public void SetEnabled(string moduleName, bool enabled)
    {
        if (!_modules.ContainsKey(moduleName))
            return;

        _modules[moduleName] = enabled;
        Save();
    }

    private void Load()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                Save();
                return;
            }

            string json = File.ReadAllText(_configPath);

            var savedModules =
                JsonSerializer.Deserialize<Dictionary<string, bool>>(json);

            if (savedModules == null)
                return;

            foreach (var item in savedModules)
            {
                if (_modules.ContainsKey(item.Key))
                    _modules[item.Key] = item.Value;
            }
        }
        catch
        {
            // Keep default module settings if the configuration cannot be read.
        }
    }

    private void Save()
    {
        try
        {
            string json = JsonSerializer.Serialize(
                _modules,
                new JsonSerializerOptions
                {
                    WriteIndented = true
                });

            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Unable to save Nexus IT module settings.", ex);
        }
    }
}
