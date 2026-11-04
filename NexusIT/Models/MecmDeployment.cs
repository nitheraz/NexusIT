
namespace NexusIT.App.Models;

public sealed class MecmDeployment
{
    public string DeploymentId { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string CollectionId { get; set; } = string.Empty;
    public string CollectionName { get; set; } = string.Empty;

    public string? SoftwareName { get; set; }
    public string? ApplicationName { get; set; }
    public string? DeploymentType { get; set; }
    public string? DeploymentIntent { get; set; }

    public int Targeted { get; set; }
    public int Success { get; set; }
    public int Errors { get; set; }
    public int InProgress { get; set; }
    public int Unknown { get; set; }

    public DateTime? CreationTime { get; set; }
    public DateTime? DeploymentTime { get; set; }
    public DateTime? LastSummarized { get; set; }

    public string Status
    {
        get
        {
            if (Targeted <= 0)
                return "No targets";

            if (Errors > 0)
                return "Errors";

            if (InProgress > 0)
                return "In progress";

            if (Success >= Targeted)
                return "Completed";

            return "Unknown";
        }
    }
}
