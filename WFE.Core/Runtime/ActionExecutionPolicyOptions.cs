using System.Collections.Generic;

namespace WFE.Core.Runtime
{
    /// <summary>
    /// Global default is fail-fast (MaxRetries=0) to respect tight per-packet timing budgets.
    /// Override per action NameRef for the specific actions where a single quick retry is worth
    /// the latency cost (e.g. HTTPRequest tolerating one retry on a transient network blip).
    /// Registered as a singleton and populated from appsettings in WFE.Web's Program.cs.
    /// </summary>
    public class ActionExecutionPolicyOptions
    {
        public int DefaultMaxRetries { get; set; } = 0;
        public int DefaultRetryDelayMs { get; set; } = 0;

        public Dictionary<string, ActionRetryOverride> Overrides { get; set; }
            = new Dictionary<string, ActionRetryOverride>();

        public (int MaxRetries, int RetryDelayMs) ResolveFor(string actionName)
        {
            if (Overrides.TryGetValue(actionName, out var o))
                return (o.MaxRetries, o.RetryDelayMs);
            return (DefaultMaxRetries, DefaultRetryDelayMs);
        }
    }

    public class ActionRetryOverride
    {
        public int MaxRetries { get; set; }
        public int RetryDelayMs { get; set; }
    }
}
