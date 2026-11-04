
namespace NexusIT.App.Models;

public enum MecmClientOperationType : uint
{
    FullScan = 1,
    QuickScan = 2,
    DownloadDefinition = 3,
    EvaluateSoftwareUpdates = 4,
    RequestMachinePolicy = 8,
    RequestUserPolicy = 9
}

public sealed class MecmClientOperation
{
    public MecmClientOperationType OperationType { get; set; }

    public string DisplayName => OperationType switch
    {
        MecmClientOperationType.FullScan =>
            "Full Malware Scan",

        MecmClientOperationType.QuickScan =>
            "Quick Malware Scan",

        MecmClientOperationType.DownloadDefinition =>
            "Download Malware Definitions",

        MecmClientOperationType.EvaluateSoftwareUpdates =>
            "Evaluate Software Updates",

        MecmClientOperationType.RequestMachinePolicy =>
            "Machine Policy Retrieval",

        MecmClientOperationType.RequestUserPolicy =>
            "User Policy Retrieval",

        _ => OperationType.ToString()
    };
}
