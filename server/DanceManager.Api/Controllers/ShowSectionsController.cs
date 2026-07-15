using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

/// <summary>Named sections (acts) of the recital running order.</summary>
[ApiController]
[Route("api/[controller]")]
public class ShowSectionsController : ControllerBase
{
    private readonly AppDbContext _db;

    public ShowSectionsController(AppDbContext db) => _db = db;

    // GET /api/showsections?studioId=
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ShowSection>>> GetAll([FromQuery] int? studioId)
    {
        var query = _db.ShowSections.AsQueryable();
        if (studioId is not null)
            query = query.Where(s => s.StudioId == studioId);
        return await query.OrderBy(s => s.OrderIndex).ThenBy(s => s.Id).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ShowSection>> Get(int id)
    {
        var section = await _db.FindScopedAsync<ShowSection>(id);
        return section is null ? NotFound() : section;
    }

    [HttpPost]
    public async Task<ActionResult<ShowSection>> Create(ShowSection section)
    {
        _db.ShowSections.Add(section);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = section.Id }, section);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ShowSection input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    // PUT /api/showsections/reorder — body: ordered section ids. Sets OrderIndex = index.
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder([FromBody] int[] orderedIds)
    {
        if (orderedIds is null) return BadRequest();

        var sections = await _db.ShowSections
            .Where(s => orderedIds.Contains(s.Id))
            .ToListAsync();

        for (var i = 0; i < orderedIds.Length; i++)
        {
            var section = sections.FirstOrDefault(s => s.Id == orderedIds[i]);
            if (section is not null) section.OrderIndex = i;
        }

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/showsections/{id} — the section's numbers fall back to "Unassigned" (FK SetNull).
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var section = await _db.FindScopedAsync<ShowSection>(id);
        if (section is null) return NotFound();
        _db.ShowSections.Remove(section);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
