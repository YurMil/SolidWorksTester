namespace SolidWorksTester.UI.Models
{
    /// <summary>Lifecycle of one part file in the task manager queue.</summary>
    public enum BatchTaskStatus
    {
        Pending,
        Running,
        Succeeded,
        Failed,
        Skipped,
        Cancelled
    }
}
