
using System;
using System.Management;
using NexusIT.App.Models;

namespace NexusIT.App.Services;

public sealed class MecmClientOperationService
{
    private readonly MecmConnectionService _connection;

    public MecmClientOperationService(
        MecmConnectionService connection)
    {
        _connection = connection ??
            throw new ArgumentNullException(nameof(connection));
    }

    public MecmOperationResult RunOperation(
        int resourceId,
        MecmClientOperationType operationType)
    {
        if (!_connection.IsConnected)
        {
            return MecmOperationResult.Fail(
                "Not connected to a MECM site.");
        }

        if (resourceId <= 0)
        {
            return MecmOperationResult.Fail(
                "A valid device Resource ID is required.");
        }

        try
        {
            ManagementScope scope =
                _connection.GetConnectedScope();

            using var clientOperationClass =
                new ManagementClass(
                    scope,
                    new ManagementPath("SMS_ClientOperation"),
                    null);

            using ManagementBaseObject input =
                clientOperationClass.GetMethodParameters(
                    "InitiateClientOperation");

            input["Type"] = (uint)operationType;

            // Direct targeting of a single resource.
            input["TargetCollectionID"] = string.Empty;

            input["RandomizationWindow"] = (uint)0;

            input["TargetResourceIDs"] = new uint[]
            {
                (uint)resourceId
            };

            using ManagementBaseObject output =
                clientOperationClass.InvokeMethod(
                    "InitiateClientOperation",
                    input,
                    null);

            if (output is null)
            {
                return MecmOperationResult.Fail(
                    "The SMS Provider returned no operation result.");
            }

            uint returnValue = Convert.ToUInt32(
                output["ReturnValue"] ?? 0);

            uint operationId = Convert.ToUInt32(
                output["OperationID"] ?? 0);

            if (returnValue != 0)
            {
                return MecmOperationResult.Fail(
                    "MECM rejected the client operation.",
                    $"Provider return value: {returnValue}");
            }

            string operationName = operationType.ToString();

            return MecmOperationResult.Ok(
                $"{operationName} was submitted to MECM. " +
                $"Operation ID: {operationId}");
        }
        catch (ManagementException ex)
        {
            return MecmOperationResult.Fail(
                "The SMS Provider could not start the client operation.",
                ex.Message);
        }
        catch (Exception ex)
        {
            return MecmOperationResult.Fail(
                "Could not submit the client operation.",
                ex.Message);
        }
    }
}
