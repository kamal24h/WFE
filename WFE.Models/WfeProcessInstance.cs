using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WFE.Models
{
    [Table("WfeProcessInstance")]
    public class WfeProcessInstance
    {
        public WfeProcessInstance()
        {
            WfeProcessTransitionsHistory = new HashSet<WfeProcessTransitionHistory>();
            Parameters = new HashSet<WfeProcessInstanceParameter>();
            ChildInstances = new HashSet<WfeProcessInstance>();
        }

        [Required]
        public long Id { get; set; }

        // Points at the immutable runtime snapshot (see WfeProcessScheme), not the design-time
        // WfeScheme - fixes the ambiguity from the original model.
        [Required]
        public long ProcessSchemeId { get; set; }

        [Required]
        public string Activity { get; set; }
        public string PreviousActivity { get; set; }
        public string State { get; set; }
        public string PreviousState { get; set; }

        // "Running" (mid auto-transition loop, transient - shouldn't normally be observed at
        // rest) | "Waiting" (parked at a non-final activity, needs a Command/Schedule to
        // proceed) | "Completed" (reached a final activity) | "Faulted" (an action threw and
        // the instance was halted - see FaultReason).
        [Required]
        [MaxLength(32)]
        public string Status { get; set; } = "Running";

        public string FaultReason { get; set; }

        public DateTime CreationDateTime { get; set; } = DateTime.UtcNow;

        // Set only when the instance reaches a final activity (Status == "Completed") or
        // faults - gives you duration for free (CompletionDateTime - CreationDateTime).
        public DateTime? CompletionDateTime { get; set; }

        // EF Core optimistic concurrency token - guards against two concurrent
        // ExecuteCommand calls (or a command racing an in-flight auto-transition)
        // corrupting instance state; the loser gets a DbUpdateConcurrencyException.
        [Timestamp]
        public byte[] RowVersion { get; set; }

        // Optional free-form correlation id (e.g. the originating sensor/device id) so you can
        // find "every instance for tag X" without joining through Parameters. Not required -
        // the packet's Tag/Value still land in Parameters as the source of truth.
        [MaxLength(256)]
        public string CorrelationId { get; set; }

        // --- Phase 3: parallel/subprocess linkage ---
        // Null for a normal (non-subprocess) instance. Set by StartChildInstanceAsync when this
        // instance was spawned by a Fork/Start transition on another instance.
        public long? ParentInstanceId { get; set; }

        // The top-level ancestor across however many fork levels deep this instance is -
        // equals ParentInstanceId's own RootInstanceId (or ParentInstanceId itself if the
        // parent has no root, i.e. the parent IS the root). Null for a non-subprocess instance.
        public long? RootInstanceId { get; set; }

        // Name of the Fork/Start transition (on the PARENT instance) that spawned this
        // instance - lets CheckAllSubprocessesCompleted-style logic group siblings, and is
        // useful for debugging "which fork point produced this subprocess".
        [MaxLength(256)]
        public string ForkTransitionName { get; set; }

        // --- Schedule trigger support ---
        // Set whenever the instance parks in "Waiting" at an activity with an outbound
        // Schedule-triggered transition; null otherwise. ScheduleWorker polls
        // WHERE Status='Waiting' AND NextScheduledCheckTime <= now - see IProcessInstanceStore.
        public DateTime? NextScheduledCheckTime { get; set; }

        public virtual WfeProcessInstance ParentInstance { get; set; }
        public virtual ICollection<WfeProcessInstance> ChildInstances { get; set; }

        public virtual WfeProcessScheme ProcessScheme { get; set; }
        public virtual ICollection<WfeProcessInstanceParameter> Parameters { get; set; }
        public virtual ICollection<WfeProcessTransitionHistory> WfeProcessTransitionsHistory { get; set; }
    }
}
