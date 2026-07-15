using DanceManager.Api.Data;
using DanceManager.Api.Models;
using DanceManager.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShowProgramController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ShowOrderPdfService _pdf;

    public ShowProgramController(AppDbContext db, ShowOrderPdfService pdf)
    {
        _db = db;
        _pdf = pdf;
    }

    /// <summary>One reorder instruction: a number's id and the section it now sits in.</summary>
    public record ReorderItem(int Id, int? SectionId);

    /// <summary>
    /// Returns the recital program ordered by OrderPosition, optionally scoped to a
    /// studio. Scoping is by the entry's own StudioId (set on every add and
    /// backfilled from routine→class), so standalone quick-add numbers are included.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShowProgram>>> GetAll([FromQuery] int? studioId)
    {
        // No nav includes: the client resolves routine/class names via its own
        // /routines + /classes maps, and serializing Routine.Class.Routines would
        // create an object cycle when two routines of a class share the program.
        var query = _db.ShowPrograms.AsQueryable();

        if (studioId is not null)
            query = query.Where(p => p.StudioId == studioId);

        return await query.OrderBy(p => p.OrderPosition).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ShowProgram>> Get(int id)
    {
        var entry = await _db.FindScopedAsync<ShowProgram>(id);
        return entry is null ? NotFound() : entry;
    }

    [HttpPost]
    public async Task<ActionResult<ShowProgram>> Create(ShowProgram entry)
    {
        _db.ShowPrograms.Add(entry);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entry.Id }, entry);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ShowProgram input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    /// <summary>
    /// Persists the running order. Accepts an ordered list of {id, sectionId}; each
    /// entry's OrderPosition is set to its index and its SectionId updated (so this
    /// one call handles both reordering and moving numbers between sections).
    /// </summary>
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] List<ReorderItem> items)
    {
        if (items is null) return BadRequest();

        var ids = items.Select(i => i.Id).ToList();
        var entries = await _db.ShowPrograms
            .Where(p => ids.Contains(p.Id))
            .ToListAsync();

        for (var i = 0; i < items.Count; i++)
        {
            var entry = entries.FirstOrDefault(e => e.Id == items[i].Id);
            if (entry is null) continue;
            entry.OrderPosition = i;
            entry.SectionId = items[i].SectionId;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _db.FindScopedAsync<ShowProgram>(id);
        if (entry is null) return NotFound();
        _db.ShowPrograms.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    // GET /api/showprogram/pdf?studioId= — printable show order grouped by section.
    [HttpGet("pdf")]
    public async Task<IActionResult> Pdf([FromQuery] int studioId)
    {
        var studioName = await _db.Studios
            .Where(s => s.Id == studioId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync() ?? string.Empty;

        var program = await _db.ShowPrograms
            .Include(p => p.Routine)!.ThenInclude(r => r!.Class)
            .Where(p => p.StudioId == studioId)
            .OrderBy(p => p.OrderPosition)
            .ToListAsync();

        var sections = await _db.ShowSections
            .Where(s => s.StudioId == studioId)
            .OrderBy(s => s.OrderIndex).ThenBy(s => s.Id)
            .ToListAsync();

        // Participating students per class (for the roster + back-to-back detection).
        var participations = await _db.RecitalParticipations
            .Where(p => p.IsParticipating)
            .ToListAsync();
        var participatingIds = participations.Select(p => p.StudentId).Distinct().ToList();
        var students = await _db.Students
            .Where(s => participatingIds.Contains(s.Id))
            .ToListAsync();

        var data = new ShowOrderData
        {
            StudioName = studioName,
            Sections = sections,
            Program = program,
            Participations = participations,
            Students = students,
        };

        var bytes = _pdf.Build(data);
        return File(bytes, "application/pdf", "show-order.pdf");
    }
}
