
using System;
using System.Collections.Generic;
using System.Linq;
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
        string searchText = "",
        int limit = 200)
    {
        if (limit < 1 || limit > 2000)
            throw new ArgumentOutOfRangeException(nameof(limit));

        string query =
            "SELECT ResourceID, Name, UserName " +
            "FROM SMS_R_System";

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string safe = EscapeWql(searchText.Trim());

            query +=
                " WHERE Name LIKE '%" + safe + "%' " +
                "OR UserName LIKE '%" + safe + "%'";
        }

        return _connection.Query(query)
            .Take(limit)
            .Select(MapDevice)
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

    private static string EscapeWql(string value)
    {
        return value.Replace("'", "''");
    }

    private static MecmDevice MapDevice(
        Dictionary<string, object?> row)
    {
        return new MecmDevice
        {
            ResourceId = ToInt(row.GetValueOrDefault("ResourceID")),

            Name = ToText(row.GetValueOrDefault("Name")) ?? string.Empty,

            UserName = ToText(row.GetValueOrDefault("UserName")),

            OperatingSystem = ToText(
                row.GetValueOrDefault("OperatingSystemNameandVersion")),

            ClientVersion = ToText(
                row.GetValueOrDefault("ClientVersion")),

            ClientInstalled = ToBool(
                row.GetValueOrDefault("Client")),

            SiteCode = ToText(
                row.GetValueOrDefault("SiteCode")),

            Domain = ToText(
                row.GetValueOrDefault("Domain")),

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
