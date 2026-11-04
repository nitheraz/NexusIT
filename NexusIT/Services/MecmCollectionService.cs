
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using NexusIT.App.Models;

namespace NexusIT.App.Services;

public sealed class MecmCollectionService
{
    private readonly MecmConnectionService _connection;

    public MecmCollectionService(MecmConnectionService connection)
    {
        _connection = connection
            ?? throw new ArgumentNullException(nameof(connection));
    }

    public List<MecmCollection> SearchDeviceCollections(
        string searchText = "",
        int limit = 0)
    {
        if (limit < 0)
            throw new ArgumentOutOfRangeException(nameof(limit));

        string query = "SELECT * FROM SMS_Collection WHERE CollectionType = 2";

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            string safe = EscapeWql(searchText.Trim());
            query += " AND Name LIKE '%" + safe + "%'";
        }

        IEnumerable<MecmCollection> results = _connection.Query(query)
            .Select(MapCollection)
            .Where(collection =>
                !string.IsNullOrWhiteSpace(collection.CollectionId) &&
                collection.CollectionType == "2")
            .OrderBy(collection => collection.Name);

        // A limit of zero means return all matching collections.
        if (limit > 0)
            results = results.Take(limit);

        return results.ToList();
    }

    public List<MecmCollection> GetDeviceCollections(int limit = 0)
    {
        return SearchDeviceCollections(string.Empty, limit);
    }
    public List<MecmCollectionMember> GetCollectionMembers(
        string collectionId,
        int limit = 2000)
    {
        ValidateCollectionId(collectionId);

        if (limit < 1)
            limit = 2000;

        string query =
            "SELECT * FROM SMS_FullCollectionMembership " +
            "WHERE CollectionID = '" + collectionId.Trim() + "'";

        return _connection.Query(query)
            .Select(MapMember)
            .Where(member => member.ResourceId > 0)
            .OrderBy(member => member.Name)
            .Take(limit)
            .ToList();
    }

    public void AddDeviceToCollection(
        string collectionId,
        int resourceId,
        string deviceName)
    {
        ValidateCollectionId(collectionId);

        if (resourceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(resourceId));

        if (string.IsNullOrWhiteSpace(deviceName))
        {
            throw new ArgumentException(
                "A device name is required.",
                nameof(deviceName));
        }

        ManagementScope scope = _connection.GetConnectedScope();

        using ManagementObject collection =
            GetEditableDeviceCollection(scope, collectionId);

        ManagementBaseObject[] rules = GetCollectionRules(collection);

        if (rules.Any(rule =>
                IsDirectRuleForResource(rule, resourceId)))
        {
            throw new InvalidOperationException(
                $"{deviceName} already has a direct membership rule " +
                "in this collection.");
        }

        using var ruleClass = new ManagementClass(
            scope,
            new ManagementPath("SMS_CollectionRuleDirect"),
            null);

        using ManagementBaseObject? directRule =
            ruleClass.CreateInstance();

        if (directRule == null)
        {
            throw new InvalidOperationException(
                "Unable to create a direct membership rule.");
        }

        directRule["ResourceClassName"] = "SMS_R_System";
        directRule["ResourceID"] = resourceId;
        directRule["RuleName"] = deviceName;

        InvokeCollectionMethod(
            collection,
            "AddMembershipRule",
            directRule,
            "add the device to the collection");

        RequestCollectionRefresh(collection);
    }

    public void RemoveDeviceFromCollection(
        string collectionId,
        int resourceId,
        string deviceName)
    {
        ValidateCollectionId(collectionId);

        if (resourceId <= 0)
            throw new ArgumentOutOfRangeException(nameof(resourceId));

        ManagementScope scope = _connection.GetConnectedScope();

        using ManagementObject collection =
            GetEditableDeviceCollection(scope, collectionId);

        ManagementBaseObject[] rules = GetCollectionRules(collection);

        ManagementBaseObject? matchingRule = rules.FirstOrDefault(
            rule => IsDirectRuleForResource(rule, resourceId));

        if (matchingRule == null)
        {
            throw new InvalidOperationException(
                $"{deviceName} does not have a direct membership rule " +
                "in this collection. If it is included by a query rule, " +
                "its membership must be changed through that query.");
        }

        InvokeCollectionMethod(
            collection,
            "DeleteMembershipRule",
            matchingRule,
            "remove the device from the collection");

        RequestCollectionRefresh(collection);
    }

    private ManagementObject GetEditableDeviceCollection(
        ManagementScope scope,
        string collectionId)
    {
        string safeId = collectionId.Trim();

        if (safeId.StartsWith(
            "SMS",
            StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Built-in MECM collections cannot be modified here.");
        }

        var path = new ManagementPath(
            $"SMS_Collection.CollectionID='{safeId}'");

        var collection = new ManagementObject(scope, path, null);

        try
        {
            collection.Get();

            object? typeValue =
                collection.GetPropertyValue("CollectionType");

            if (Convert.ToInt32(typeValue) != 2)
            {
                throw new InvalidOperationException(
                    "Only device collections can be modified.");
            }

            return collection;
        }
        catch
        {
            collection.Dispose();
            throw;
        }
    }

    private static void InvokeCollectionMethod(
        ManagementObject collection,
        string methodName,
        ManagementBaseObject rule,
        string operation)
    {
        using ManagementBaseObject input =
            collection.GetMethodParameters(methodName)
            ?? throw new InvalidOperationException(
                $"Unable to create {methodName} parameters.");

        input["collectionRule"] = rule;

        using ManagementBaseObject? output =
            collection.InvokeMethod(methodName, input, null);

        CheckMethodResult(output, operation);
    }

    private static ManagementBaseObject[] GetCollectionRules(
        ManagementObject collection)
    {
        object? value = collection.GetPropertyValue("CollectionRules");

        if (value is not Array rules)
            return Array.Empty<ManagementBaseObject>();

        return rules
            .Cast<object?>()
            .OfType<ManagementBaseObject>()
            .ToArray();
    }

    private static bool IsDirectRuleForResource(
        ManagementBaseObject rule,
        int resourceId)
    {
        string? className =
            Convert.ToString(rule.GetPropertyValue("__CLASS"));

        if (!string.Equals(
            className,
            "SMS_CollectionRuleDirect",
            StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        object? ruleResourceId =
            rule.GetPropertyValue("ResourceID");

        return int.TryParse(
            ruleResourceId?.ToString(),
            out int existingId) &&
            existingId == resourceId;
    }

    private static void RequestCollectionRefresh(
        ManagementObject collection)
    {
        object? result = collection.InvokeMethod(
            "RequestRefresh",
            Array.Empty<object>());

        if (result is ManagementBaseObject output)
        {
            using (output)
            {
                CheckMethodResult(
                    output,
                    "request a collection refresh");
            }
        }
    }

    private static void CheckMethodResult(
        ManagementBaseObject? output,
        string operation)
    {
        if (output == null)
        {
            throw new InvalidOperationException(
                $"MECM did not return a result when attempting to {operation}.");
        }

        int returnValue = Convert.ToInt32(
            output.GetPropertyValue("ReturnValue"));

        if (returnValue != 0)
        {
            throw new InvalidOperationException(
                $"MECM returned error code {returnValue} " +
                $"when attempting to {operation}.");
        }
    }

    private static void ValidateCollectionId(string collectionId)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
        {
            throw new ArgumentException(
                "A collection ID is required.",
                nameof(collectionId));
        }

        string safeId = collectionId.Trim();

        if (safeId.Any(c =>
                !char.IsLetterOrDigit(c) && c != '_' && c != '-'))
        {
            throw new ArgumentException(
                "The collection ID contains unsupported characters.",
                nameof(collectionId));
        }
    }

    private static string EscapeWql(string value)
    {
        return value.Replace("'", "''");
    }

    private static MecmCollection MapCollection(
        Dictionary<string, object?> row)
    {
        return new MecmCollection
        {
            CollectionId = GetText(row, "CollectionID") ?? string.Empty,
            Name = GetText(row, "Name") ?? string.Empty,
            Comment = GetText(row, "Comment"),
            CollectionType = GetText(row, "CollectionType"),
            LimitToCollectionId = GetText(row, "LimitToCollectionID"),
            MemberCount = GetInt(row, "MemberCount")
        };
    }

    private static MecmCollectionMember MapMember(
        Dictionary<string, object?> row)
    {
        return new MecmCollectionMember
        {
            ResourceId = GetInt(row, "ResourceID"),
            Name = GetText(row, "Name") ?? string.Empty,
            UserName =
                GetText(row, "UserName") ??
                GetText(row, "LastLogonUserName")
        };
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
}