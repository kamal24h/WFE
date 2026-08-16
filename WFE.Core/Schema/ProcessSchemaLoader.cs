using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Xml.Serialization;

namespace WFE.Core.Schema
{
    /// <summary>
    /// Deserializes the designer-exported Process XML into <see cref="ProcessSchemaXml"/> and
    /// wraps it with fast lookups the runtime needs on every step (activity by name, outbound
    /// transitions by From-activity, command by name). Parsed schemas are cached in-memory,
    /// keyed by the caller-supplied cache key (typically WfeProcessScheme.Id), so a given
    /// scheme's XML is only parsed once per process lifetime.
    /// </summary>
    public class ProcessSchemaLoader
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(ProcessSchemaXml));

        private readonly ConcurrentDictionary<string, ResolvedProcessSchema> _cache =
            new ConcurrentDictionary<string, ResolvedProcessSchema>();

        public ResolvedProcessSchema GetOrParse(string cacheKey, string schemeXml)
        {
            if (string.IsNullOrWhiteSpace(schemeXml))
                throw new ArgumentException("Scheme XML is empty.", nameof(schemeXml));

            return _cache.GetOrAdd(cacheKey, _ => Parse(schemeXml, cacheKey));
        }

        public void Invalidate(string cacheKey) => _cache.TryRemove(cacheKey, out _);

        public static ResolvedProcessSchema Parse(string schemeXml, string cacheKey = null)
        {
            using var reader = new StringReader(schemeXml);
            var raw = (ProcessSchemaXml)Serializer.Deserialize(reader);
            return new ResolvedProcessSchema(raw, cacheKey);
        }
    }

    /// <summary>
    /// A parsed schema plus the indexes the execution engine needs. Never mutate the
    /// underlying <see cref="Raw"/> graph at runtime - instances only read from it.
    /// </summary>
    public class ResolvedProcessSchema
    {
        public ProcessSchemaXml Raw { get; }

        /// <summary>The key this schema was cached under (see ProcessSchemaLoader.GetOrParse) -
        /// stable for the lifetime of a published WfeProcessScheme, since those are immutable.
        /// Used by CodeActionCompiler to key its own compiled-delegate cache per schema.</summary>
        public string CacheKey { get; }

        public ResolvedProcessSchema(ProcessSchemaXml raw, string cacheKey)
        {
            Raw = raw ?? throw new ArgumentNullException(nameof(raw));
            CacheKey = cacheKey;

            ActivitiesByName = raw.Activities.ToDictionary(a => a.Name, StringComparer.Ordinal);

            // Preserve XML declaration order (NOT alphabetical) - the designer's transition
            // order is meaningful priority for Auto-transition resolution (first match wins).
            TransitionsByFromActivity = raw.Transitions
                .GroupBy(t => t.From, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

            CommandsByName = raw.Commands.ToDictionary(c => c.Name, StringComparer.Ordinal);

            var initial = raw.Activities.Where(a => a.IsInitialValue).ToList();
            if (initial.Count != 1)
                throw new InvalidOperationException(
                    $"Process '{raw.Name}' must have exactly one initial activity, found {initial.Count}.");
            InitialActivity = initial[0];

            foreach (var t in raw.Transitions)
            {
                if (!ActivitiesByName.ContainsKey(t.From))
                    throw new InvalidOperationException(
                        $"Transition '{t.Name}' references unknown From activity '{t.From}'.");
                if (!ActivitiesByName.ContainsKey(t.To))
                    throw new InvalidOperationException(
                        $"Transition '{t.Name}' references unknown To activity '{t.To}'.");
            }
        }

        public System.Collections.Generic.Dictionary<string, ActivityDefinitionXml> ActivitiesByName { get; }

        public System.Collections.Generic.Dictionary<string, System.Collections.Generic.List<TransitionDefinitionXml>>
            TransitionsByFromActivity { get; }

        public System.Collections.Generic.Dictionary<string, CommandDefinitionXml> CommandsByName { get; }

        public ActivityDefinitionXml InitialActivity { get; }

        public System.Collections.Generic.List<TransitionDefinitionXml> OutboundTransitions(string activityName) =>
            TransitionsByFromActivity.TryGetValue(activityName, out var list)
                ? list
                : new System.Collections.Generic.List<TransitionDefinitionXml>();
    }
}
