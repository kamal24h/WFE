using System.Collections.Generic;
using WFE.Client.Services;

namespace WFE.Client.Models
{
    public class DashboardViewModel
    {
        public IReadOnlyList<PacketLogEntry> RecentEntries { get; set; }
        public string RabbitMqEndpoint { get; set; }
        public string RabbitMqQueueName { get; set; }
        public bool RabbitMqAutoConnect { get; set; }
        public bool RabbitMqIsRunning { get; set; }
        public bool TestAutoAdvancerEnabled { get; set; }
        public string WfeBaseUrl { get; set; }
        public long WfeSchemeId { get; set; }
    }
}
