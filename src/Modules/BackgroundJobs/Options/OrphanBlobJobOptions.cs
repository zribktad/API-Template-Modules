using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace BackgroundJobs.Options;

/// <summary>
///     Configuration for the recurring orphan-blob reaper that removes staging crash residue and
///     zero-refcount committed blobs from the FileStorage module.
/// </summary>
public sealed class OrphanBlobJobOptions
{
    [Description("Enables execution of the orphan-blob recurring job.")]
    public bool Enabled { get; set; } = true;

    [Description("Six-part (with seconds) CRON expression scheduling the orphan-blob sweep.")]
    [Required]
    [MinLength(1)]
    public string Cron { get; set; } = "0 0 * * * ?";
}
