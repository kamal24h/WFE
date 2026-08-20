using System;
using System.ComponentModel.DataAnnotations;

namespace WFE.Models
{
    public class WfeRuntime
    {
        [MaxLength(450)]
        public string RuntimeId { get; set; }
        public Guid Lock { get; set; }
        public int Status { get; set; }
        [MaxLength(450)]
        public string? RestorerId { get; set; }
        public DateTime? NextTimerTime { get; set; }
        public DateTime? NextServiceTimerTime { get; set; }
        public DateTime? LastAliveSignal { get; set; }
    }
}
