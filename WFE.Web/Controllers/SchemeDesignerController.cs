using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WFE.Core.Runtime;
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
        private readonly IProcessSchemeProvider _schemeProvider;

        public SchemeDesignerController(WfeDbContext db, IProcessSchemeProvider schemeProvider)
        {
            _db = db;
            _schemeProvider = schemeProvider;
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
        /// comments for why these are kept separate). This is the "official" publish flow
        /// (with supersede-previous semantics) - for rapid test/evaluation iteration where you
        /// don't want that bookkeeping, see IWorkflowRuntime.StartInstanceFromSchemeAsync /
        /// ProcessPacketFromSchemeAsync instead, which snapshot-and-start in one call.</summary>
        [HttpPost("{schemeId}/publish")]
        public async Task<ActionResult<long>> Publish(long schemeId, PublishSchemeRequest request)
        {
            if (request.SupersedePrevious)
            {
                var previous = await _db.WfeProcessSchemes
                    .Where(ps => ps.SchemeId == schemeId && !ps.IsObsolete)
                    .ToListAsync();
                foreach (var p in previous) p.IsObsolete = true;
                await _db.SaveChangesAsync();
            }

            try
            {
                var (processScheme, _) = await _schemeProvider.CreateSnapshotAsync(schemeId, request.TrackHistory);
                return Ok(processScheme.Id);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not found"))
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Schema XML failed validation.", detail = ex.Message });
            }
        }
    }
}
