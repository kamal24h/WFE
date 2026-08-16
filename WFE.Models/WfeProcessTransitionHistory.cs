using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WFE.Models
{
    // Also absorbs what WfeApprovalHistory used to cover (AllowedTo/Commentary) via
    // Description - kept as one table instead of two overlapping audit trails.
    public class WfeProcessTransitionHistory
    {
        [Required]
        public long Id { get; set; }

        [Required]
        public long ProcessInstanceId { get; set; }

        public long? ExecutorActorId { get; set; }

        // Free text note (approval commentary, error detail, scheduler run id, etc.)
        public string Description { get; set; }

        [MaxLength(256)]
        public string ExecutorId { get; set; }

        [Required]
        [MaxLength(256)]
        public string ActorId { get; set; }

        [Required]
        [MaxLength(256)]
        public string FromActivity { get; set; }

        [Required]
        [MaxLength(256)]
        public string ToActivity { get; set; }

        [Required]
        [MaxLength(256)]
        public string FromState { get; set; }

        [Required]
        [MaxLength(256)]
        public string ToState { get; set; }

        // Name of the transition that fired (matches Transition/@Name in the schema) -
        // handy for querying "how often does transition X fire" without string-matching
        // From/To pairs.
        [MaxLength(256)]
        public string TransitionName { get; set; }

        [Required]
        public DateTime StartTransitionTime { get; set; } = DateTime.UtcNow;

        public virtual WfeActor ExecutorActor { get; set; }
        public virtual WfeProcessInstance ProcessInstance { get; set; }
    }
}
