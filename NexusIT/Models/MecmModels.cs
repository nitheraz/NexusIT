
namespace NexusIT.App.Models;

public sealed class MecmDevice
{
    public int ResourceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? UserName { get; set; }

    public string? OperatingSystem { get; set; }

    public string? ClientVersion { get; set; }

    public bool ClientInstalled { get; set; }

    public string? SiteCode { get; set; }

    public string? Domain { get; set; }

    public string? SmsUniqueIdentifier { get; set; }

    public string? LastLogonUser { get; set; }

    public string? MacAddress { get; set; }

    public string? IpAddress { get; set; }
}

public sealed class MecmCollection
{
    public string CollectionId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public string? CollectionType { get; set; }

    public string? LimitToCollectionId { get; set; }

    public int MemberCount { get; set; }
}

public sealed class MecmCollectionMember
{
    public int ResourceId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? UserName { get; set; }
}

public sealed class MecmOperationResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public DateTimeOffset Timestamp { get; init; } =
        DateTimeOffset.Now;

    public static MecmOperationResult Ok(string message)
    {
        return new MecmOperationResult
        {
            Success = true,
            Message = message
        };
    }

    public static MecmOperationResult Fail(
        string message,
        string? detail = null)
    {
        return new MecmOperationResult
        {
            Success = false,
            Message = message,
            Detail = detail
        };
    }
}

public sealed class MecmActivityEntry
{
    public DateTimeOffset Timestamp { get; set; } =
        DateTimeOffset.Now;

    public string Action { get; set; } = string.Empty;

    public string Target { get; set; } = string.Empty;

    public string Result { get; set; } = string.Empty;

    public string? Details { get; set; }

    public string? Account { get; set; }
}
