using System;

namespace WFE.Models
{  
    public class WfeProcessTimer
    {
        public long Id { get; set; }
        public long ProcessId { get; set; }
        public long? RootProcessId { get; set; }
        public string Name { get; set; }
        public DateTime NextExecutionDateTime { get; set; }
        public bool Ignore { get; set; }
    }
}
