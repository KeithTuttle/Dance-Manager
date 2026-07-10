using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ShowProgramController : ControllerBase
{
    private readonly AppDbContext _db;

    public ShowProgramController(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns the recital program ordered by OrderPosition. Optionally filtered
    /// to a single studio (via the routine's class).
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShowProgram>>> GetAll([FromQuery] int? studioId)
    {
        var query = _db.ShowPrograms
            .Include(p => p.Routine)!
                .ThenInclude(r => r!.Class)
            .AsQueryable();

        if (studioId is not null)
            query = query.Where(p => p.Routine != null && p.Routine.Class != null && p.Routine.Class.StudioId == studioId);

        return await query.OrderBy(p => p.OrderPosition).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ShowProgram>> Get(int id)
    {
        var entry = await _db.ShowPrograms.FindAsync(id);
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
        _db.Entry(input).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>
    /// Persists a new show order. Accepts an ordered array of ShowProgram ids;
    /// each id's OrderPosition is set to its index in the array.
    /// </summary>
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] int[] orderedIds)
    {
        if (orderedIds is null) return BadRequest();

        var entries = await _db.ShowPrograms
            .Where(p => orderedIds.Contains(p.Id))
            .ToListAsync();

        for (var i = 0; i < orderedIds.Length; i++)
        {
            var entry = entries.FirstOrDefault(e => e.Id == orderedIds[i]);
            if (entry is not null) entry.OrderPosition = i;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _db.ShowPrograms.FindAsync(id);
        if (entry is null) return NotFound();
        _db.ShowPrograms.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
