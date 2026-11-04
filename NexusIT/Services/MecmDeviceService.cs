
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using NexusIT.App.Models;

namespace NexusIT.App.Services;

public sealed class MecmDeviceService
{
    private readonly MecmConnectionService _connection;

    public MecmDeviceService(MecmConnectionService connection)
    {
        _connection = connection ??
            throw new ArgumentNullException(nameof(connection));
    }

    public List<MecmDevice> SearchDevices(
        string searchText,
        int limit = 200)
    {
        if (string.IsNullOrWhiteSpace(searchText))
            return new List<MecmDevice>();

        if (limit < 1 || limit > 2000)
            throw new ArgumentOutOfRangeException(nameof(limit));

        string safe = EscapeWql(searchText.Trim());

        string query =
            "SELECT ResourceID, Name, LastLogonUserName, " +
            "OperatingSystemNameandVersion, ClientVersion, Client, " +
            "ResourceDomainORWorkgroup, SMSUniqueIdentifier, " +
            "MACAddresses, IPAddresses " +
            "FROM SMS_R_System " +
            "WHERE Name LIKE '%" + safe + "%'";

        return _connection.Query(query)
            .Take(limit)
            .Select(MapDevice)
            .Where(device => device.ResourceId > 0)
            .OrderBy(device => device.Name)
            .ToList();
    }

    public MecmDevice? GetDevice(int resourceId)
    {
        if (resourceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(resourceId));

        string query =
            "SELECT * FROM SMS_R_System " +
            $"WHERE ResourceID = {resourceId}";

        return _connection.Query(query)
            .Select(MapDevice)
            .FirstOrDefault();
    }

    public void DeleteDevice(int resourceId)
    {
        if (resourceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(resourceId));

        // Verify the device exists before attempting deletion.
        MecmDevice? device = GetDevice(resourceId);

        if (device == null)
        {
            throw new InvalidOperationException(
                $"MECM device with Resource ID {resourceId} was not found.");
        }

        ManagementScope scope = _connection.GetConnectedScope();

        var path = new ManagementPath(
            $"SMS_R_System.ResourceID={resourceId}");

        using var managementObject =
            new ManagementObject(scope, path, null);

        managementObject.Get();
        managementObject.Delete();
    }

    private static string EscapeWql(string value)
    {
        return value.Replace("'", "''");
    }

    private static MecmDevice MapDevice(
        Dictionary<string, object?> row)
    {
        return new MecmDevice
        {
            ResourceId = ToInt(
                row.GetValueOrDefault("ResourceID")),

            Name = ToText(
                row.GetValueOrDefault("Name")) ?? string.Empty,

            UserName = ToText(
                row.GetValueOrDefault("LastLogonUserName")),

            OperatingSystem = ToText(
                row.GetValueOrDefault("OperatingSystemNameandVersion")),

            ClientVersion = ToText(
                row.GetValueOrDefault("ClientVersion")),

            ClientInstalled = ToBool(
                row.GetValueOrDefault("Client")),

            Domain = ToText(
                row.GetValueOrDefault("ResourceDomainORWorkgroup")),

            SmsUniqueIdentifier = ToText(
                row.GetValueOrDefault("SMSUniqueIdentifier")),

            LastLogonUser = ToText(
                row.GetValueOrDefault("LastLogonUserName")),

            MacAddress = ToArrayText(
                row.GetValueOrDefault("MACAddresses")),

            IpAddress = ToArrayText(
                row.GetValueOrDefault("IPAddresses"))
        };
    }

    private static int ToInt(object? value)
    {
        return int.TryParse(value?.ToString(), out int result)
            ? result
            : 0;
    }

    private static bool ToBool(object? value)
    {
        if (value is bool boolean)
            return boolean;

        return value?.ToString() == "1";
    }

    private static string? ToText(object? value)
    {
        return value?.ToString();
    }

    private static string? ToArrayText(object? value)
    {
        if (value is string[] values)
            return string.Join(", ", values);

        if (value is Array array)
        {
            return string.Join(
                ", ",
                array.Cast<object?>()
                    .Select(x => x?.ToString())
                    .Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        return ToText(value);
    }
}
