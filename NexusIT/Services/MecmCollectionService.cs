
using System;
using System.Collections.Generic;
using System.Linq;
using NexusIT.App.Models;

namespace NexusIT.App.Services;

public sealed class MecmCollectionService
{
    private readonly MecmConnectionService _connection;

    public MecmCollectionService(
        MecmConnectionService connection)
    {
        _connection = connection ??
            throw new ArgumentNullException(nameof(connection));
    }

    public List<MecmCollection> GetDeviceCollections(
        string searchText = "",
        int limit = 500)
    {
        if (limit < 1 || limit > 5000)
            throw new ArgumentOutOfRangeException(nameof(limit));

        string where = string.IsNullOrWhiteSpace(searchText)
            ? string.Empty
            : " WHERE Name LIKE '%" +
              searchText.Trim().Replace("'", "''") +
              "%'";

        string query =
            "SELECT CollectionID, Name, Comment, " +
            "CollectionType, LimitToCollectionID, MemberCount " +
            "FROM SMS_Collection" +
            where;

        return _connection.Query(query)
            .Take(limit)
            .Select(row => new MecmCollection
            {
                CollectionId =
                    row.GetValueOrDefault("CollectionID")?.ToString()
                    ?? string.Empty,

                Name =
                    row.GetValueOrDefault("Name")?.ToString()
                    ?? string.Empty,

                Comment =
                    row.GetValueOrDefault("Comment")?.ToString(),

                CollectionType =
                    row.GetValueOrDefault("CollectionType")?.ToString(),

                LimitToCollectionId =
                    row.GetValueOrDefault("LimitToCollectionID")?.ToString(),

                MemberCount = int.TryParse(
                    row.GetValueOrDefault("MemberCount")?.ToString(),
                    out int count)
                    ? count
                    : 0
            })
            .ToList();
    }

    public List<MecmCollectionMember> GetCollectionMembers(
        string collectionId,
        int limit = 2000)
    {
        if (string.IsNullOrWhiteSpace(collectionId))
            throw new ArgumentException(
                "A collection ID is required.",
                nameof(collectionId));

        if (collectionId.Contains('\'') ||
            collectionId.Contains('\\'))
            throw new ArgumentException(
                "Invalid collection ID.",
                nameof(collectionId));

        if (limit < 1 || limit > 10000)
            throw new ArgumentOutOfRangeException(nameof(limit));

        string query =
            "SELECT ResourceID, Name, UserName " +
            "FROM SMS_FullCollectionMembership " +
            $"WHERE CollectionID = '{collectionId}'";

        return _connection.Query(query)
            .Take(limit)
            .Select(row => new MecmCollectionMember
            {
                ResourceId = int.TryParse(
                    row.GetValueOrDefault("ResourceID")?.ToString(),
                    out int id)
                    ? id
                    : 0,

                Name =
                    row.GetValueOrDefault("Name")?.ToString()
                    ?? string.Empty,

                UserName =
                    row.GetValueOrDefault("UserName")?.ToString()
            })
            .ToList();
    }
}
