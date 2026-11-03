
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace NexusIT.App.Services;

public sealed class MecmConnectionService : IDisposable
{
    private ManagementScope? _scope;
    private bool _disposed;

    public bool IsConnected => _scope?.IsConnected == true;

    public string SiteServer { get; private set; } = string.Empty;
    public string SiteCode { get; private set; } = string.Empty;
    public string ConnectedUsername { get; private set; } = string.Empty;
    public string SiteName { get; private set; } = string.Empty;
    public string SiteVersion { get; private set; } = string.Empty;

    public void Connect(
        string siteServer,
        string siteCode,
        string username,
        string password)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(siteServer))
            throw new ArgumentException("Enter the MECM site server.");

        if (string.IsNullOrWhiteSpace(siteCode) ||
            siteCode.Trim().Length != 3)
            throw new ArgumentException(
                "The MECM site code must contain exactly three characters.");

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Enter the connection username.");

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Enter the connection password.");

        Disconnect();

        string server = siteServer.Trim();
        string code = siteCode.Trim().ToUpperInvariant();

        var options = new ConnectionOptions
        {
            Username = username.Trim(),
            Password = password,
            Impersonation = ImpersonationLevel.Impersonate,
            Authentication =
                System.Management.AuthenticationLevel.PacketPrivacy,
            EnablePrivileges = true,
            Timeout = TimeSpan.FromSeconds(30)
        };

        var scope = new ManagementScope(
            $@"\\{server}\root\SMS\site_{code}",
            options);

        try
        {
            scope.Connect();

            if (!scope.IsConnected)
                throw new InvalidOperationException(
                    "The MECM SMS Provider connection could not be established.");

            _scope = scope;

            SiteServer = server;
            SiteCode = code;
            ConnectedUsername = username.Trim();

            ReadSiteInformation();
        }
        catch
        {
            Disconnect();
            throw;
        }
    }

    public void ReadSiteInformation()
    {
        var results = Query(
            "SELECT SiteCode, SiteName, Version FROM SMS_Site");

        var site = results.FirstOrDefault();

        SiteName =
            site?.GetValueOrDefault("SiteName")?.ToString()
            ?? string.Empty;

        SiteVersion =
            site?.GetValueOrDefault("Version")?.ToString()
            ?? string.Empty;
    }

    public ManagementScope GetConnectedScope()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsConnected || _scope is null)
            throw new InvalidOperationException(
                "Not connected to MECM.");

        return _scope;
    }


    public List<Dictionary<string, object?>> Query(string wql)
    {
        if (string.IsNullOrWhiteSpace(wql))
            throw new ArgumentException(
                "A WQL query is required.",
                nameof(wql));

        ManagementScope scope = GetConnectedScope();

        var rows = new List<Dictionary<string, object?>>();

        try
        {
            using var searcher = new ManagementObjectSearcher(
                scope,
                new ObjectQuery(wql));

            using ManagementObjectCollection results = searcher.Get();

            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    var row =
                        new Dictionary<string, object?>(
                            StringComparer.OrdinalIgnoreCase);

                    foreach (PropertyData property in item.Properties)
                    {
                        row[property.Name] = property.Value;
                    }

                    rows.Add(row);
                }
            }

            return rows;
        }
        catch (ManagementException ex)
        {
            string errorDetails = string.Empty;

            if (ex.ErrorInformation is not null)
            {
                try
                {
                    errorDetails = string.Join(
                        Environment.NewLine,
                        ex.ErrorInformation.Properties
                            .Cast<PropertyData>()
                            .Select(p => $"{p.Name}: {p.Value}"));
                }
                catch
                {
                    errorDetails = ex.ErrorInformation.ToString() ?? "";
                }
            }

            throw new InvalidOperationException(
                $"MECM WMI query failed.{Environment.NewLine}" +
                $"Error: {ex.Message}{Environment.NewLine}" +
                $"WMI error code: {ex.ErrorCode}{Environment.NewLine}" +
                $"Query: {wql}{Environment.NewLine}" +
                $"Provider details: {errorDetails}",
                ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                "Access denied while querying the MECM SMS Provider. " +
                "Check the account's Configuration Manager permissions. " +
                ex.Message,
                ex);
        }
    }


    public void Disconnect()
    {
        _scope = null;

        SiteServer = string.Empty;
        SiteCode = string.Empty;
        ConnectedUsername = string.Empty;
        SiteName = string.Empty;
        SiteVersion = string.Empty;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Disconnect();
        _disposed = true;
    }
}
