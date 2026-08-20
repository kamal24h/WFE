using System;
using System.ComponentModel.DataAnnotations;

namespace WFE.Models
{
    public class WfeProcessInstancePersistence
    {
        public long Id { get; set; }      
        public long ProcessId { get; set; }
        public string ParameterName { get; set; }
        public string Value { get; set; }
    }    
}
