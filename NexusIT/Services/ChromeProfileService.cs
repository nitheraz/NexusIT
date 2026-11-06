
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace NexusIT.App.Services;

public sealed class ChromeProfileService
{
    private readonly string _userDataPath;

    public ChromeProfileService()
    {
        _userDataPath = Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "Google",
            "Chrome",
            "User Data");
    }

    public bool IsChromeInstalled =>
        !string.IsNullOrWhiteSpace(GetChromeExecutable());

    public List<ChromeProfile> GetProfiles()
    {
        string localStatePath =
            Path.Combine(_userDataPath, "Local State");

        if (!File.Exists(localStatePath))
            return new List<ChromeProfile>();

        try
        {
            using JsonDocument document =
                JsonDocument.Parse(File.ReadAllText(localStatePath));

            if (!document.RootElement.TryGetProperty(
                    "profile", out JsonElement profileElement) ||
                !profileElement.TryGetProperty(
                    "info_cache", out JsonElement infoCache))
            {
                return new List<ChromeProfile>();
            }

            var profiles = new List<ChromeProfile>();

            foreach (JsonProperty profile in infoCache.EnumerateObject())
            {
                string directoryName = profile.Name;

                JsonElement details = profile.Value;

                string profileName = GetJsonString(
                    details, "name");

                string userName = GetJsonString(
                    details, "user_name");

                string displayName =
                    !string.IsNullOrWhiteSpace(profileName)
                        ? profileName
                        : !string.IsNullOrWhiteSpace(userName)
                            ? userName
                            : directoryName;

                profiles.Add(new ChromeProfile
                {
                    DirectoryName = directoryName,
                    DisplayName = displayName,
                    GoogleAccount = userName,
                    IsDefault = string.Equals(
                        directoryName,
                        "Default",
                        StringComparison.OrdinalIgnoreCase)
                });
            }

            return profiles
                .OrderByDescending(profile => profile.IsDefault)
                .ThenBy(profile => profile.DisplayName)
                .ToList();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                "Unable to read Chrome profile information.",
                ex);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                "Unable to access Chrome profile information.",
                ex);
        }
    }

    public void OpenWebsite(
        string url,
        string profileDirectory)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException(
                "A website URL is required.", nameof(url));

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException(
                "Enter a valid HTTP or HTTPS website URL.",
                nameof(url));
        }

        if (string.IsNullOrWhiteSpace(profileDirectory))
            throw new ArgumentException(
                "A Chrome profile must be selected.",
                nameof(profileDirectory));

        string chromePath = GetChromeExecutable()
            ?? throw new FileNotFoundException(
                "Google Chrome could not be found.");

        var startInfo = new ProcessStartInfo
        {
            FileName = chromePath,
            UseShellExecute = true
        };

        startInfo.ArgumentList.Add(
            $"--profile-directory={profileDirectory}");

        startInfo.ArgumentList.Add(uri.AbsoluteUri);

        Process.Start(startInfo);
    }

    private string? GetChromeExecutable()
    {
        string[] possiblePaths =
        {
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFilesX86),
                "Google", "Chrome", "Application", "chrome.exe"),

            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.ProgramFiles),
                "Google", "Chrome", "Application", "chrome.exe"),

            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Google", "Chrome", "Application", "chrome.exe")
        };

        return possiblePaths.FirstOrDefault(File.Exists);
    }

    private static string GetJsonString(
        JsonElement element,
        string propertyName)
    {
        if (element.TryGetProperty(
                propertyName, out JsonElement value) &&
            value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        return string.Empty;
    }
}

public sealed class ChromeProfile
{
    public string DirectoryName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string GoogleAccount { get; set; } = string.Empty;

    public bool IsDefault { get; set; }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(GoogleAccount)
            ? DisplayName
            : $"{DisplayName} ({GoogleAccount})";
    }
}
