using System;
using System.Collections.Generic;

namespace WFE.Models
{
    public partial class WfeScheme
    {
        public long Id { get; set; }
        public long BusinessProcessId { get; set; }
        public string Name { get; set; }
        public string Scheme { get; set; }
        public DateTime? CreateDate { get; set; }
        public string Tags { get; set; }
        public bool? Enabled { get; set; }
        public int Revision { get; set; }

        public virtual WfeBusinessProcess BusinessProcess { get; set; }
        // NOTE: removed the WfeProcessInstances collection that was here - instances point at
        // WfeProcessScheme (the immutable runtime snapshot), not directly at this design-time
        // WfeScheme (see WfeProcessScheme.cs for why). Left mapped, this nav had no matching FK
        // on WfeProcessInstance, so EF Core would have silently invented a second shadow FK
        // column (WfeSchemeId) alongside ProcessSchemeId - two competing "which scheme" pointers
        // on the same table. If you need "all instances ever run under this design", query
        // through WfeProcessScheme (WfeProcessScheme.SchemeId == this.Id) instead.
    }
}
