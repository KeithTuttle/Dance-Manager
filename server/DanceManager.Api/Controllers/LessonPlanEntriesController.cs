using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LessonPlanEntriesController : ControllerBase
{
    private readonly AppDbContext _db;

    public LessonPlanEntriesController(AppDbContext db) => _db = db;

    /// <summary>
    /// Returns lesson-plan entries for a class, newest-first by WeekOf.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<LessonPlanEntry>>> GetForClass([FromQuery] int classId) =>
        await _db.LessonPlanEntries
            .Where(e => e.ClassId == classId)
            .OrderByDescending(e => e.WeekOf)
            .ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LessonPlanEntry>> Get(int id)
    {
        var entry = await _db.LessonPlanEntries.FindAsync(id);
        return entry is null ? NotFound() : entry;
    }

    [HttpPost]
    public async Task<ActionResult<LessonPlanEntry>> Create(LessonPlanEntry entry)
    {
        _db.LessonPlanEntries.Add(entry);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = entry.Id }, entry);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, LessonPlanEntry input)
    {
        if (id != input.Id) return BadRequest();
        _db.Entry(input).State = EntityState.Modified;
        await _db.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await _db.LessonPlanEntries.FindAsync(id);
        if (entry is null) return NotFound();
        _db.LessonPlanEntries.Remove(entry);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
