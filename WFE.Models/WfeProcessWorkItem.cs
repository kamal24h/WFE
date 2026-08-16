using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

#nullable disable

namespace WFE.Models
{
    // Durable queue backing "AnotherThread" subprocess spawns - WFE.Runtime.Scheduling.
    // SubprocessWorker polls this table and creates the actual child WfeProcessInstance.
    // Persisted (not in-memory) so a spawn survives an app restart, and so
    // CheckAllSubprocessesCompleted has a stable "how many were ever spawned" count the moment
    // the parent enqueues them - before any worker has picked them up.
    public class WfeProcessWorkItem
    {
        public long Id { get; set; }
        public long ProcessSchemeId { get; set; }
        public long ParentInstanceId { get; set; }
        public long RootInstanceId { get; set; }

        [MaxLength(256)]
        public string StartActivity { get; set; }

        [MaxLength(256)]
        public string ForkTransitionName { get; set; }

        [MaxLength(256)]
        public string ActorId { get; set; }

        // JSON-serialized Dictionary<string,string> snapshot of the parent's parameters at
        // enqueue time (the "CopyAll" startup parameter copy strategy - the only one implemented).
        public string ParametersJson { get; set; }

        // "Pending" | "Processing" | "Completed" | "Faulted" - this is the WORK ITEM's own
        // processing status, distinct from the spawned child instance's business Status. A
        // work item can be "Completed" (the spawn attempt succeeded) while the child instance
        // itself ends up "Waiting" or even "Faulted" as a business outcome.
        [Required]
        [MaxLength(32)]
        public string Status { get; set; } = "Pending";

        public string Error { get; set; }

        public DateTime CreatedDateTime { get; set; } = DateTime.UtcNow;
        public DateTime? ClaimedDateTime { get; set; }
        public DateTime? CompletedDateTime { get; set; }

        // Optimistic concurrency so multiple worker instances/replicas can safely race to
        // claim the same batch without double-spawning a subprocess.
        [Timestamp]
        public byte[] RowVersion { get; set; }

        public virtual WfeProcessInstance ParentInstance { get; set; }
    }
}
