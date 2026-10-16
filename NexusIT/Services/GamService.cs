
using NexusIT.App.Models;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace NexusIT.App.Services;

public sealed class GamService
{
    private static readonly TimeSpan CommandTimeout =
        TimeSpan.FromMinutes(2);

    private static readonly TimeSpan AssetCacheLifetime =
        TimeSpan.FromHours(4);

    public async Task<GamResult> CheckGamHealthAsync()
    {
        return await RunGamAsync("version");
    }

    public async Task<(ChromebookInfo? Device, GamResult Result)>
        GetChromebookInfoAsync(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            return (null, Failure("A serial number is required."));
        }

        GamResult result = await RunGamAsync(
            "info", "cros", "cros_sn", serialNumber.Trim());

        if (!result.Success)
            return (null, result);

        ChromebookInfo? device = ParseChromebookInfo(result.Output ?? "");

        if (device is null)
            return (null, Failure(
                "GAM returned output, but the Chromebook details could not be parsed."));

        if (!string.IsNullOrWhiteSpace(device.SerialNumber) &&
            !device.SerialNumber.Equals(
                serialNumber.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return (null, Failure(
                "The returned Chromebook serial number did not match the requested serial number."));
        }

        return (device, result);
    }

    public async Task<(ChromebookInfo? Device, GamResult Result)>
        GetChromebookInfoByAssetIdAsync(
            string assetId,
            bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(assetId))
        {
            return (null, Failure("An asset ID is required."));
        }

        Dictionary<string, string>? assetMap = forceRefresh
            ? null
            : AssetIdCacheService.TryLoad(AssetCacheLifetime);

        if (assetMap is null)
        {
            GamResult result = await RunGamAsync(
                "print", "cros",
                "fields", "serialNumber,annotatedAssetId");

            if (!result.Success)
                return (null, result);

            assetMap = ParseAssetIdMap(result.Output ?? "");
            AssetIdCacheService.Save(assetMap);
        }

        string? serialNumber = assetMap
            .FirstOrDefault(pair => pair.Key.Equals(
                assetId.Trim(),
                StringComparison.OrdinalIgnoreCase))
            .Value;

        if (string.IsNullOrWhiteSpace(serialNumber))
        {
            return (null, Failure(
                $"No Chromebook was found with asset ID '{assetId}'."));
        }

        return await GetChromebookInfoAsync(serialNumber);
    }

    public async Task<GamResult> UpdateAnnotatedAssetIdAsync(
        string serialNumber,
        string assetId)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) ||
            string.IsNullOrWhiteSpace(assetId))
        {
            return Failure("A serial number and asset ID are required.");
        }

        GamResult result = await RunGamAsync(
            "update", "cros", "cros_sn", serialNumber.Trim(),
            "annotatedassetid", assetId.Trim());

        if (result.Success)
            AssetIdCacheService.Clear();

        return result;
    }

    public Task<GamResult> DisableChromebookAsync(string serialNumber)
    {
        return RunDeviceActionAsync(serialNumber, "disable");
    }

    public Task<GamResult> ReenableChromebookAsync(string serialNumber)
    {
        return RunDeviceActionAsync(serialNumber, "reenable");
    }

    public Task<GamResult> PowerwashChromebookAsync(string serialNumber)
    {
        return RunDeviceActionAsync(serialNumber, "powerwash");
    }

    public Task<GamResult> ClearChromebookProfilesAsync(string serialNumber)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return Task.FromResult(Failure("A serial number is required."));

        return RunGamAsync(
            "update", "cros", "cros_sn", serialNumber.Trim(),
            "clearprofiles");
    }

    public async Task<GamResult> MoveChromebookToOuAsync(
        string serialNumber,
        string orgUnitPath)
    {
        if (string.IsNullOrWhiteSpace(serialNumber) ||
            string.IsNullOrWhiteSpace(orgUnitPath))
        {
            return Failure("A serial number and OU path are required.");
        }

        GamResult result = await RunGamAsync(
            "update", "cros", "cros_sn", serialNumber.Trim(),
            "ou", orgUnitPath.Trim());

        if (result.Success)
            AssetIdCacheService.Clear();

        return result;
    }

    public async Task<(List<string> Items, GamResult Result)>
        GetAllOrgUnitPathsAsync()
    {
        GamResult result = await RunGamAsync("print", "orgs");

        if (!result.Success)
            return (new List<string>(), result);

        var paths = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (string line in (result.Output ?? "").Split(
                     new[] { "\r\n", "\n" },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string value = line.Trim();

            if (value.StartsWith("orgUnitPath:", StringComparison.OrdinalIgnoreCase))
                value = value["orgUnitPath:".Length..].Trim();
            else if (value.StartsWith("orgUnitPath,", StringComparison.OrdinalIgnoreCase))
                value = value["orgUnitPath,".Length..].Trim();
            else
                continue;

            if (!string.IsNullOrWhiteSpace(value) &&
                !value.Equals("orgUnitPath", StringComparison.OrdinalIgnoreCase))
            {
                paths.Add(value);
            }
        }

        return (paths.OrderBy(
            path => path,
            StringComparer.OrdinalIgnoreCase).ToList(), result);
    }

    public async Task<(List<string> Items, GamResult Result)>
        GetAllGroupEmailsAsync()
    {
        return await GetEmailListAsync("groups");
    }

    public async Task<(List<string> Items, GamResult Result)>
        GetAllUserEmailsAsync()
    {
        return await GetEmailListAsync("users");
    }

    public Task<GamResult> AddUserToGroupAsync(
        string userEmail,
        string groupEmail)
    {
        if (string.IsNullOrWhiteSpace(userEmail) ||
            string.IsNullOrWhiteSpace(groupEmail))
        {
            return Task.FromResult(
                Failure("Both user and group email addresses are required."));
        }

        return RunGamAsync(
            "update", "group", groupEmail.Trim(),
            "add", "member", userEmail.Trim());
    }

    private async Task<GamResult> RunDeviceActionAsync(
        string serialNumber,
        string action)
    {
        if (string.IsNullOrWhiteSpace(serialNumber))
            return Failure("A serial number is required.");

        return await RunGamAsync(
            "update", "cros", "cros_sn", serialNumber.Trim(),
            "action", action);
    }

    private async Task<(List<string> Items, GamResult Result)>
        GetEmailListAsync(string type)
    {
        GamResult result = await RunGamAsync("print", type);

        if (!result.Success)
            return (new List<string>(), result);

        var emails = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        foreach (string line in (result.Output ?? "").Split(
                     new[] { "\r\n", "\n" },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string value = line.Trim();

            if (value.Contains(','))
            {
                var fields = ParseCsvLine(value);

                if (fields.Count > 0)
                    value = fields[0].Trim();
            }

            if (value.Contains('@') &&
                !value.Equals("email", StringComparison.OrdinalIgnoreCase))
            {
                emails.Add(value);
            }
        }

        return (emails.OrderBy(
            email => email,
            StringComparer.OrdinalIgnoreCase).ToList(), result);
    }

    private static ChromebookInfo? ParseChromebookInfo(string output)
    {
        if (string.IsNullOrWhiteSpace(output))
            return null;

        var device = new ChromebookInfo
        {
            RawOutput = output
        };

        foreach (string line in output.Split(
                     new[] { "\r\n", "\n" },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = line.IndexOf(':');

            if (separator < 0)
                continue;

            string key = line[..separator].Trim();
            string value = line[(separator + 1)..].Trim();

            switch (key.ToLowerInvariant())
            {
                case "serialnumber":
                    device.SerialNumber = value;
                    break;
                case "annotatedassetid":
                    device.AnnotatedAssetId = value;
                    break;
                case "model":
                    device.Model = value;
                    break;
                case "macaddress":
                    device.MacAddress = FormatMacAddress(value);
                    break;
                case "orgunitid":
                    device.OrgUnitId = value;
                    break;
                case "orgunitpath":
                    device.OrgUnitPath = value;
                    break;
                case "annotateduser":
                case "recentuseremail":
                    device.RecentUserEmail = value;
                    break;
                case "lastsync":
                    device.LastSync = value;
                    break;
                case "status":
                    device.Status = value;
                    break;
                case "deviceid":
                    device.DeviceId = value;
                    break;
            }
        }

        return string.IsNullOrWhiteSpace(device.SerialNumber)
            ? null
            : device;
    }

    private static Dictionary<string, string> ParseAssetIdMap(string output)
    {
        var map = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        using var reader = new StringReader(output);

        string? headerLine = reader.ReadLine();

        if (string.IsNullOrWhiteSpace(headerLine))
            return map;

        List<string> headers = ParseCsvLine(headerLine);

        int serialIndex = headers.FindIndex(header =>
            header.Equals("serialNumber", StringComparison.OrdinalIgnoreCase));

        int assetIndex = headers.FindIndex(header =>
            header.Equals("annotatedAssetId", StringComparison.OrdinalIgnoreCase));

        if (serialIndex < 0 || assetIndex < 0)
            return map;

        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            List<string> fields = ParseCsvLine(line);

            if (fields.Count <= Math.Max(serialIndex, assetIndex))
                continue;

            string serial = fields[serialIndex].Trim();
            string asset = fields[assetIndex].Trim();

            if (!string.IsNullOrWhiteSpace(serial) &&
                !string.IsNullOrWhiteSpace(asset))
            {
                map[asset] = serial;
            }
        }

        return map;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var fields = new List<string>();
        var field = new StringBuilder();
        bool insideQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char current = line[i];

            if (current == '"')
            {
                if (insideQuotes &&
                    i + 1 < line.Length &&
                    line[i + 1] == '"')
                {
                    field.Append('"');
                    i++;
                }
                else
                {
                    insideQuotes = !insideQuotes;
                }
            }
            else if (current == ',' && !insideQuotes)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else
            {
                field.Append(current);
            }
        }

        fields.Add(field.ToString());
        return fields;
    }

    private static string FormatMacAddress(string value)
    {
        string hex = Regex.Replace(value, "[^0-9a-fA-F]", "");

        if (hex.Length != 12)
            return value;

        return string.Join(":",
            Enumerable.Range(0, 6)
                .Select(index => hex.Substring(index * 2, 2)))
            .ToUpperInvariant();
    }

    private static GamResult Failure(string message)
    {
        return new GamResult
        {
            Success = false,
            ExitCode = -1,
            Error = message,
            IssueType = GamIssueType.Unknown
        };
    }

    private static async Task<GamResult> RunGamAsync(
        params string[] arguments)
    {
        string? gamPath = await GamLocator.LocateGam();

        if (string.IsNullOrWhiteSpace(gamPath) ||
            !File.Exists(gamPath))
        {
            return new GamResult
            {
                Success = false,
                ExitCode = -1,
                Error = "The bundled GAM7 executable was not found.",
                IssueType = GamIssueType.GamNotFound
            };
        }

        using var process = new Process();

        process.StartInfo = new ProcessStartInfo
        {
            FileName = gamPath,
            WorkingDirectory = Path.GetDirectoryName(gamPath)!,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        foreach (string argument in arguments)
            process.StartInfo.ArgumentList.Add(argument);

        try
        {
            if (!process.Start())
                return Failure("GAM7 could not be started.");

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> errorTask = process.StandardError.ReadToEndAsync();

            using var timeout = new CancellationTokenSource(CommandTimeout);

            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // The process may already have exited.
                }

                return new GamResult
                {
                    Success = false,
                    ExitCode = -1,
                    Error = $"GAM7 timed out after {CommandTimeout.TotalMinutes:0} minutes.",
                    IssueType = GamIssueType.Timeout
                };
            }

            string output = await outputTask;
            string error = await errorTask;

            return new GamResult
            {
                Success = process.ExitCode == 0,
                ExitCode = process.ExitCode,
                Output = output,
                Error = error,
                IssueType = process.ExitCode == 0
                    ? GamIssueType.None
                    : GamIssueType.Unknown
            };
        }
        catch (Exception ex)
        {
            return new GamResult
            {
                Success = false,
                ExitCode = -1,
                Error = $"Failed to run GAM7: {ex.Message}",
                IssueType = GamIssueType.Unknown
            };
        }
    }
}
