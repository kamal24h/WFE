using System;
using System.Collections.Generic;
using System.Linq;

namespace WFE.Client.Services
{
    public class PacketLogEntry
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>"RabbitMQ" or "Manual" (the test form).</summary>
        public string Source { get; set; }

        public string Topic { get; set; }
        public string DeviceId { get; set; }
        public string Tag { get; set; }
        public string Value { get; set; }
        public WfeIngestResult Result { get; set; }
    }

    /// <summary>Deliberately in-memory only (not persisted) - this is a throwaway evaluation
    /// harness, not a system of record. Restarting the client clears the log; that's fine.</summary>
    public class PacketActivityLog
    {
        // Sized to comfortably hold one full 500-record batch plus some history, so a batch
        // doesn't immediately evict itself from the dashboard while you're still looking at it.
        private const int MaxEntries = 1000;
        private readonly object _lock = new();
        private readonly LinkedList<PacketLogEntry> _entries = new();

        public void Add(PacketLogEntry entry)
        {
            lock (_lock)
            {
                _entries.AddFirst(entry);
                while (_entries.Count > MaxEntries)
                    _entries.RemoveLast();
            }
        }

        public IReadOnlyList<PacketLogEntry> GetRecent(int count = 50)
        {
            lock (_lock)
            {
                return _entries.Take(count).ToList();
            }
        }
    }
}
