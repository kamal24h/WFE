using System;
using System.Collections.Generic;
using System.Text;

namespace WFE.Models;


    public class WfeCommand
	{
		public string Id { get; set; }
		public string TransitionId { get; set; }
		public long SchemeId { get; set; }
		public string ActorId { get; set; }
		public string Activity { get; set; }
		public string Title { get; set; }
		public string CssClass { get; set; }
		public string Url { get; set; }
		public bool IsDynamic { get; set; }
		public List<long> InstanceIds { get; set; }
	}
