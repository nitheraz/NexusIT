
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using NexusIT.App.Models;

namespace NexusIT.App.Services;

public sealed class MecmDeploymentService
{
    private readonly MecmConnectionService _connection;

    public MecmDeploymentService(MecmConnectionService connection)
    {
        _connection = connection
            ?? throw new ArgumentNullException(nameof(connection));
    }

    public List<MecmDeployment> GetDeployments(int limit = 500)
    {
        if (limit < 1)
            limit = 500;

        const string query = "SELECT * FROM SMS_DeploymentSummary";

        return _connection.Query(query)
            .Select(MapDeployment)
            .Where(deployment =>
                !string.IsNullOrWhiteSpace(deployment.DeploymentId))
            .OrderByDescending(deployment => deployment.CreationTime)
            .Take(limit)
            .ToList();
    }

    public List<MecmDeployment> SearchDeployments(
        string searchText,
        int limit = 500)
    {
        List<MecmDeployment> deployments = GetDeployments(limit);

        if (string.IsNullOrWhiteSpace(searchText))
            return deployments;

        string search = searchText.Trim();

        return deployments
            .Where(deployment =>
                Contains(deployment.DeploymentName, search) ||
                Contains(deployment.SoftwareName, search) ||
                Contains(deployment.ApplicationName, search) ||
                Contains(deployment.CollectionName, search) ||
                Contains(deployment.CollectionId, search) ||
                Contains(deployment.DeploymentId, search))
            .ToList();
    }

    private static MecmDeployment MapDeployment(
        Dictionary<string, object?> row)
    {
        return new MecmDeployment
        {
            DeploymentId = GetText(row, "DeploymentID") ?? string.Empty,

            DeploymentName =
                GetText(row, "SoftwareName") ??
                GetText(row, "ApplicationName") ??
                GetText(row, "ModelName") ??
                "Unnamed deployment",

            CollectionId =
                GetText(row, "CollectionID") ?? string.Empty,

            CollectionName =
                GetText(row, "CollectionName") ?? string.Empty,

            SoftwareName = GetText(row, "SoftwareName"),

            ApplicationName = GetText(row, "ApplicationName"),

            DeploymentType = GetText(row, "FeatureType"),

            DeploymentIntent = GetText(row, "DeploymentIntent"),

            Targeted = GetInt(row, "NumberTargeted"),

            Success = GetInt(row, "NumberSuccess"),

            Errors = GetInt(row, "NumberErrors"),

            InProgress = GetInt(row, "NumberInProgress"),

            Unknown = GetInt(row, "NumberUnknown"),

            CreationTime = GetDate(row, "CreationTime"),

            DeploymentTime = GetDate(row, "DeploymentTime"),

            LastSummarized = GetDate(row, "SummarizationTime")
        };
    }

    private static bool Contains(string? value, string search)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Contains(
                   search,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static string? GetText(
        Dictionary<string, object?> row,
        string property)
    {
        if (!row.TryGetValue(property, out object? value) ||
            value == null ||
            value == DBNull.Value)
        {
            return null;
        }

        return Convert.ToString(value);
    }

    private static int GetInt(
        Dictionary<string, object?> row,
        string property)
    {
        if (!row.TryGetValue(property, out object? value) ||
            value == null ||
            value == DBNull.Value)
        {
            return 0;
        }

        try
        {
            return Convert.ToInt32(value);
        }
        catch
        {
            return 0;
        }
    }

    private static DateTime? GetDate(
        Dictionary<string, object?> row,
        string property)
    {
        if (!row.TryGetValue(property, out object? value) ||
            value == null ||
            value == DBNull.Value)
        {
            return null;
        }

        try
        {
            if (value is DateTime dateTime)
                return dateTime;

            string dateValue = Convert.ToString(value) ?? string.Empty;

            if (string.IsNullOrWhiteSpace(dateValue))
                return null;

            return ManagementDateTimeConverter.ToDateTime(dateValue);
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (ManagementException)
        {
            return null;
        }
    }
}
