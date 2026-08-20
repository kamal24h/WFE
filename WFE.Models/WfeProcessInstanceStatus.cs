using System;
using System.ComponentModel.DataAnnotations;

namespace WFE.Models
{
    public class WfeProcessInstanceStatus
    {
        [Required]
        public long Id { get; set; }      
        public int Status { get; set; }
        public Guid Lock { get; set; }
        [MaxLength(450)]
        public string RuntimeId { get; set; }
        public DateTime SetTime { get; set; }
    }    
}
