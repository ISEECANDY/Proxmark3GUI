namespace Proxmark3GUI.Models;

/// <summary>
/// Status of an attack job in the scheduler queue.
/// </summary>
public enum AttackStatus
{
    /// <summary>Job is waiting in the queue.</summary>
    Pending,

    /// <summary>Job is currently executing.</summary>
    Running,

    /// <summary>Job completed successfully.</summary>
    Completed,

    /// <summary>Job was cancelled by the user.</summary>
    Cancelled,

    /// <summary>Job failed with an error.</summary>
    Failed,
}

/// <summary>
/// A scheduled attack job that can be queued, run, and tracked.
/// </summary>
public class AttackJob
{
    /// <summary>Unique identifier for this job.</summary>
    public int Id { get; set; }

    /// <summary>Human-readable job name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Type of attack (e.g. "nested", "darkside", "mfck").</summary>
    public string AttackType { get; set; } = string.Empty;

    /// <summary>The proxmark3 command string to execute.</summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>Target keys to recover (hex strings or descriptions).</summary>
    public List<string> TargetKeys { get; set; } = [];

    /// <summary>Number of times the attack should be repeated.</summary>
    public int RepeatCount { get; set; } = 1;

    /// <summary>True when the job is actively running.</summary>
    public bool IsRunning { get; set; }

    /// <summary>Progress value between 0 and 100.</summary>
    public double Progress { get; set; }

    /// <summary>Current status of the job.</summary>
    public AttackStatus Status { get; set; }

    /// <summary>Result message or recovered data after completion.</summary>
    public string? Result { get; set; }
}
