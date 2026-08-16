using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WFE.Core.Schema;
using WFE.Models;
using WFE.Persistence;
using WFE.Web.Contracts;

namespace WFE.Web.Controllers
{
    [ApiController]
    [Route("api/schemes")]
    public class SchemeDesignerController : ControllerBase
    {
        private readonly WfeDbContext _db;

        public SchemeDesignerController(WfeDbContext db)
        {
            _db = db;
        }

        /// <summary>Saves a design-time version. Validated eagerly (structure + exactly one
        /// initial activity + no dangling transition references) so a bad export from the
        /// designer fails here, not when someone tries to publish/run it.</summary>
        [HttpPost]
        public async Task<ActionResult<long>> Save(SaveSchemeRequest request)
        {
            try
            {
                ProcessSchemaLoader.Parse(request.SchemeXml);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Schema XML failed validation.", detail = ex.Message });
            }

            var scheme = new WfeScheme
            {
                BusinessProcessId = request.BusinessProcessId,
                Name = request.Name,
                Scheme = request.SchemeXml,
                Tags = request.Tags,
                CreateDate = DateTime.UtcNow,
                Enabled = true
            };
            _db.WfeSchemes.Add(scheme);
            await _db.SaveChangesAsync();

            return Ok(scheme.Id);
        }

        [HttpGet("{schemeId}")]
        public async Task<ActionResult<WfeScheme>> Get(long schemeId)
        {
            var scheme = await _db.WfeSchemes.AsNoTracking().FirstOrDefaultAsync(s => s.Id == schemeId);
            return scheme == null ? NotFound() : Ok(scheme);
        }

        /// <summary>Resolves a design-time WfeScheme into an immutable WfeProcessScheme
        /// snapshot that instances actually run against (see the WfeProcessScheme model
        /// comments for why these are kept separate). Phase 1: no subprocess inlining yet, so
        /// this is currently a straight copy of the XML plus bookkeeping fields.</summary>
        [HttpPost("{schemeId}/publish")]
        public async Task<ActionResult<long>> Publish(long schemeId, PublishSchemeRequest request)
        {
            var scheme = await _db.WfeSchemes.FirstOrDefaultAsync(s => s.Id == schemeId);
            if (scheme == null) return NotFound();

            try
            {
                ProcessSchemaLoader.Parse(scheme.Scheme);
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Schema XML failed validation.", detail = ex.Message });
            }

            if (request.SupersedePrevious)
            {
                var previous = await _db.WfeProcessSchemes
                    .Where(ps => ps.SchemeId == schemeId && !ps.IsObsolete)
                    .ToListAsync();
                foreach (var p in previous) p.IsObsolete = true;
            }

            var processScheme = new WfeProcessScheme
            {
                SchemeId = scheme.Id,
                Scheme = scheme.Scheme,
                IsObsolete = false,
                RootSchemeCode = scheme.Name,
                TrackHistory = request.TrackHistory
            };
            _db.WfeProcessSchemes.Add(processScheme);
            await _db.SaveChangesAsync();

            return Ok(processScheme.Id);
        }
    }
}
