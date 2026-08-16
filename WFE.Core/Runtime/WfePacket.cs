using System;
using System.Threading;
using System.Threading.Tasks;

namespace WFE.Core.Runtime
{
    /// <summary>
    /// One sensor reading: Tag is the sensor's unique id, Value is its reading at Timestamp.
    /// This is intentionally just Tag/Value/Timestamp - anything else about the packet should
    /// arrive as additional Metadata entries, which land as instance parameters exactly like
    /// Tag/Value do, so rules and expressions can reference them uniformly.
    /// </summary>
    public class WfePacket
    {
        public string Tag { get; set; }
        public string Value { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public System.Collections.Generic.IDictionary<string, string> Metadata { get; set; }
    }

    /// <summary>
    /// Entry point for the ingestion side of the pipeline: one packet in, the process
    /// scheme runs it to completion (or to a Command-waiting activity) and either discards
    /// the ephemeral instance or persists it, per WfeProcessScheme.TrackHistory.
    /// </summary>
    public interface IPacketProcessor
    {
        Task ProcessAsync(long processSchemeId, WfePacket packet, CancellationToken cancellationToken = default);
    }
}
