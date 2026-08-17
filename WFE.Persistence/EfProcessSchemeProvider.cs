using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WFE.Core.Schema;
using WFE.Models;

namespace WFE.Persistence
{
    public class EfProcessSchemeProvider : Core.Runtime.IProcessSchemeProvider
    {
        private readonly WfeDbContext _db;
        private readonly ProcessSchemaLoader _loader;

        public EfProcessSchemeProvider(WfeDbContext db, ProcessSchemaLoader loader)
        {
            _db = db;
            _loader = loader;
        }

        public async Task<(WfeProcessScheme Scheme, ResolvedProcessSchema Resolved)> GetAsync(
            long processSchemeId, CancellationToken cancellationToken = default)
        {
            var scheme = await _db.WfeProcessSchemes
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == processSchemeId, cancellationToken);

            if (scheme == null)
                throw new InvalidOperationException($"WfeProcessScheme {processSchemeId} not found.");

            if (scheme.IsObsolete)
                // Obsolete schemes can still be read (so already-running instances against them
                // keep working), but starting a NEW instance against one is almost always a
                // caller bug (stale scheme id cached somewhere) - surface it loudly rather than
                // silently running against a superseded definition.
                throw new InvalidOperationException(
                    $"WfeProcessScheme {processSchemeId} is marked obsolete - fetch the current published scheme instead.");

            // Cache key is the scheme id, not its content - fine as long as WfeProcessScheme
            // rows are truly immutable once created (per the "resolved runtime snapshot" design).
            // If you ever allow in-place edits to a published WfeProcessScheme, switch this to a
            // content hash or call _loader.Invalidate(...) on update.
            var resolved = _loader.GetOrParse(processSchemeId.ToString(), scheme.Scheme);
            return (scheme, resolved);
        }

        public async Task<(WfeProcessScheme Scheme, ResolvedProcessSchema Resolved)> CreateSnapshotAsync(
            long schemeId, bool trackHistory, CancellationToken cancellationToken = default)
        {
            var source = await _db.WfeSchemes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schemeId, cancellationToken);
            if (source == null)
                throw new InvalidOperationException($"WfeScheme {schemeId} not found.");

            // Validate before persisting - a bad edit shouldn't produce an unusable snapshot row.
            ProcessSchemaLoader.Parse(source.Scheme);

            var processScheme = new WfeProcessScheme
            {
                SchemeId = source.Id,
                Scheme = source.Scheme,
                IsObsolete = false,
                RootSchemeCode = source.Name,
                TrackHistory = trackHistory
            };
            _db.WfeProcessSchemes.Add(processScheme);
            await _db.SaveChangesAsync(cancellationToken);

            var resolved = _loader.GetOrParse(processScheme.Id.ToString(), processScheme.Scheme);
            return (processScheme, resolved);
        }
    }
}
