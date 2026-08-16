using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WFE.Models
{
    // New table (Phase 1 addition): backs SetParameter / RemoveParameter / CheckParameter
    // and @ParameterName expressions used throughout the schema samples.
    public class WfeProcessInstanceParameter
    {
        [Required]
        public long Id { get; set; }

        [Required]
        public long ProcessInstanceId { get; set; }

        [Required]
        [MaxLength(256)]
        public string Name { get; set; }

        // Stored as text; callers parse to int/decimal/DateTime/etc. as needed.
        // (Loop counters, HTTP response bodies, etc. all flow through here as strings.)
        public string Value { get; set; }

        // ActionParameter payloads use "ForRootProcess": true to target the top-level
        // process instance's parameters instead of the current (sub)instance's - relevant
        // once Phase 3 (subprocesses) lands. Kept here now so the column exists from day 1.
        [Required]
        public bool ForRootProcess { get; set; }

        public virtual WfeProcessInstance ProcessInstance { get; set; }
    }
}

// --- Suggested addition to WfeProcessInstance.cs (not modified here to avoid clobbering your file) ---
// public virtual ICollection<WfeProcessInstanceParameter> Parameters { get; set; }
//   = new HashSet<WfeProcessInstanceParameter>();
