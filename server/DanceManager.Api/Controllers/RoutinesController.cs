using DanceManager.Api.Data;
using DanceManager.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DanceManager.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RoutinesController : ControllerBase
{
    private readonly AppDbContext _db;

    public RoutinesController(AppDbContext db) => _db = db;

    // GET /api/routines?classId=  -> routines in one class
    // GET /api/routines?studioId= -> routines across a studio (via the routine's class)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Routine>>> GetAll(
        [FromQuery] int? classId, [FromQuery] int? studioId)
    {
        var query = _db.Routines.AsQueryable();
        if (classId is not null)
            query = query.Where(r => r.ClassId == classId);
        if (studioId is not null)
            query = query.Where(r => r.Class != null && r.Class.StudioId == studioId);
        return await query.OrderBy(r => r.SongTitle).ToListAsync();
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<Routine>> Get(int id)
    {
        var routine = await _db.FindScopedAsync<Routine>(id);
        return routine is null ? NotFound() : routine;
    }

    [HttpPost]
    public async Task<ActionResult<Routine>> Create(Routine routine)
    {
        _db.Routines.Add(routine);
        await _db.SaveChangesAsync();
        return CreatedAtAction(nameof(Get), new { id = routine.Id }, routine);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, Routine input)
    {
        if (id != input.Id) return BadRequest();
        return await _db.UpdateScopedAsync(id, input) ? NoContent() : NotFound();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var routine = await _db.FindScopedAsync<Routine>(id);
        if (routine is null) return NotFound();
        _db.Routines.Remove(routine);
        await _db.SaveChangesAsync();
        return NoContent();
    }
}
