using System.Collections.Generic;

namespace WFE.Models
{
    public class WfeBusinessProcess
    {        
        public long Id { get; set; }
        public string Name { get; set; }
        public string Descriptions { get; set; }
        
        public virtual ICollection<WfeScheme> WfeSchemes { get; set; }
    }
}
