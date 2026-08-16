using System;
using System.Collections.Generic;

namespace WFE.Models
{
    public class WfeRole
    {

        public long Id { get; set; }
        public string Name { get; set; }
        public DateTime CreateDate { get; set; }
        public bool? Enable { get; set; }

        public virtual ICollection<WfeUserRole> WfeUserRoles { get; set; }
    }
}
