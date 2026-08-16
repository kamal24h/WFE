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
    }
}
